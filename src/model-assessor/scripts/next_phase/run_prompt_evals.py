#!/usr/bin/env python3
import argparse
import datetime as dt
import hashlib
import json
import os
import pathlib
import re
import signal
import sys
from typing import Any
from urllib import error, request


SCRIPT_DIR = pathlib.Path(__file__).resolve().parent
MODEL_ASSESSOR_ROOT = pathlib.Path(__file__).resolve().parents[2]
for candidate in (MODEL_ASSESSOR_ROOT, SCRIPT_DIR):
    candidate_str = str(candidate)
    if candidate_str not in sys.path:
        sys.path.insert(0, candidate_str)

from runner_lib import (  # noqa: E402
    OMLXHarness,
    build_profile_settings,
    detect_assistant_setting_field,
    ensure_vlm_mtp_assistant_configuration,
    list_profile_field_names,
    merge_settings,
    pick_model,
)

SCHEMA_VERSION = "1.0"
DEFAULT_BASE_URL = "http://127.0.0.1:8000"
ALLOWED_WORKLOADS = {
    "short_code_research_tools",
    "long_code_research_tools",
    "short_coding",
    "long_coding",
    "deep_research",
}
ALLOWED_IMPORTANCE = {"must_have", "strong", "nice_to_have"}
ALLOWED_SEVERITY = {"critical", "warning"}
ALLOWED_DETECTABLE = {"automatic", "manual"}
TERMINAL_STATUSES = {"success", "partial", "failed"}
DEFAULT_CONTEXT_FILE_CHAR_LIMIT = 12000
DEFAULT_COMPLETION_TIMEOUT_SECONDS = 300


class CompletionTimeoutError(TimeoutError):
    pass


class WallClockTimeout:
    def __init__(self, seconds: int):
        self.seconds = seconds
        self.previous_handler = None
        self.enabled = os.name == "posix" and self.seconds > 0

    def _handle_timeout(self, _signum, _frame):
        raise CompletionTimeoutError(f"completion request exceeded {self.seconds}s wall-clock timeout")

    def __enter__(self):
        if not self.enabled:
            return self
        self.previous_handler = signal.getsignal(signal.SIGALRM)
        signal.signal(signal.SIGALRM, self._handle_timeout)
        signal.setitimer(signal.ITIMER_REAL, self.seconds)
        return self

    def __exit__(self, exc_type, exc, tb):
        if not self.enabled:
            return False
        signal.setitimer(signal.ITIMER_REAL, 0)
        signal.signal(signal.SIGALRM, self.previous_handler)
        return False


def load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: pathlib.Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def slugify(value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    return slug or "evaluation"


def build_evaluation_run_id(model_id: str, profile_id: str, now: dt.datetime | None = None) -> str:
    current = now or dt.datetime.now()
    return f"{current.strftime('%Y%m%d-%H%M%S')}-{slugify(model_id)}-{slugify(profile_id)}"


def relative_to_repo(path: pathlib.Path, repo_root: pathlib.Path) -> str:
    return path.resolve().relative_to(repo_root.resolve()).as_posix()


def sha256_for_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")


def iter_fixture_files(fixture_root: pathlib.Path) -> list[pathlib.Path]:
    files: list[pathlib.Path] = []
    for path in sorted(fixture_root.rglob("*")):
        if path.is_file():
            files.append(path)
    return files


def compute_fixture_size_bytes(fixture_root: pathlib.Path) -> int:
    total = 0
    for file_path in iter_fixture_files(fixture_root):
        if file_path.name == "fixture_manifest.json":
            continue
        total += file_path.stat().st_size
    return total


def compute_fixture_hash(
    fixture_root: pathlib.Path,
    cases_doc: dict[str, Any],
    fixture_manifest: dict[str, Any],
    prompt_templates: dict[str, Any],
    benchmark_profiles: dict[str, Any],
    repo_root: pathlib.Path,
) -> str:
    digest = hashlib.sha256()
    for file_path in iter_fixture_files(fixture_root):
        digest.update(relative_to_repo(file_path, repo_root).encode("utf-8"))
        digest.update(b"\0")
        digest.update(file_path.read_bytes())
        digest.update(b"\0")
    digest.update(canonical_json_bytes(cases_doc))
    digest.update(b"\0")
    digest.update(canonical_json_bytes(fixture_manifest))
    digest.update(b"\0")
    digest.update(canonical_json_bytes(prompt_templates))
    digest.update(b"\0")
    digest.update(canonical_json_bytes(benchmark_profiles))
    return digest.hexdigest()


def render_prompt(template: str, placeholder_values: dict[str, Any]) -> str:
    rendered = template
    for key, value in placeholder_values.items():
        rendered = rendered.replace("{" + key + "}", str(value))
    return rendered


def read_prompt_context_files(
    case: dict[str, Any],
    repo_root: pathlib.Path,
    fixture_root: pathlib.Path,
) -> tuple[list[str], list[dict[str, Any]]]:
    context_paths = case.get("prompt_context_paths") or []
    if not context_paths:
        return [], []

    char_limit = case.get("prompt_context_char_limit")
    if not isinstance(char_limit, int) or char_limit <= 0:
        char_limit = DEFAULT_CONTEXT_FILE_CHAR_LIMIT

    prompt_blocks: list[str] = []
    context_metadata: list[dict[str, Any]] = []
    for relative_path in context_paths:
        path = repo_root / relative_path
        raw_text = path.read_text(encoding="utf-8")
        truncated = len(raw_text) > char_limit
        rendered_text = raw_text[:char_limit]
        if truncated:
            rendered_text = rendered_text.rstrip() + "\n[truncated]\n"
        prompt_blocks.append(
            f"### {relative_path}\n```text\n{rendered_text.rstrip()}\n```"
        )
        context_metadata.append(
            {
                "path": relative_path,
                "truncated": truncated,
                "source_chars": len(raw_text),
                "rendered_chars": len(rendered_text),
            }
        )

    intro = case.get("prompt_context_intro")
    if not isinstance(intro, str) or not intro.strip():
        intro = "Use the following local evidence as primary context for the task."

    return [intro, *prompt_blocks], context_metadata


def required_placeholders(template: str) -> set[str]:
    return set(re.findall(r"\{([A-Z0-9_]+)\}", template))


def detect_fact(text: str, fact: str) -> bool:
    return fact.casefold() in text.casefold()


def detect_forbidden_claim(text: str, claim: str) -> bool:
    return claim.casefold() in text.casefold()


def build_automatic_checks(expected_answer: dict[str, Any], output_text: str) -> dict[str, Any]:
    required = []
    required_hits = 0
    for fact in expected_answer.get("required_facts", []):
        found = detect_fact(output_text, fact["fact"])
        required.append({
            "fact": fact["fact"],
            "importance": fact["importance"],
            "found": found,
        })
        if found:
            required_hits += 1

    forbidden = []
    forbidden_hits = 0
    for claim in expected_answer.get("forbidden_claims", []):
        found = detect_forbidden_claim(output_text, claim["claim"])
        forbidden.append({
            "claim": claim["claim"],
            "severity": claim["severity"],
            "found": found,
        })
        if found:
            forbidden_hits += 1

    automatic_signals = []
    manual_signals = []
    for signal in expected_answer.get("quality_signals", []):
        target = automatic_signals if signal["detectable"] == "automatic" else manual_signals
        target.append(signal)

    return {
        "required_fact_checks": required,
        "forbidden_claim_checks": forbidden,
        "summary": {
            "required_fact_hits": required_hits,
            "required_fact_total": len(required),
            "forbidden_claim_hits": forbidden_hits,
            "forbidden_claim_total": len(forbidden),
            "automatic_quality_signal_count": len(automatic_signals),
            "manual_quality_signal_count": len(manual_signals),
        },
    }


class PublicInferenceClient:
    def __init__(self, base_url: str, api_key: str, completion_timeout_seconds: int = DEFAULT_COMPLETION_TIMEOUT_SECONDS):
        self.base_url = base_url.rstrip("/")
        self.api_key = api_key
        self.completion_timeout_seconds = completion_timeout_seconds

    def generate(self, model_id: str, prompt: str, settings: dict[str, Any]) -> dict[str, Any]:
        payload = {
            "model": model_id,
            "prompt": prompt,
            "stream": False,
            "max_tokens": settings.get("max_tokens", 1024),
            "temperature": settings.get("temperature", 0.2),
            "top_p": settings.get("top_p", 0.95),
        }
        req = request.Request(
            self.base_url + "/v1/completions",
            data=json.dumps(payload).encode("utf-8"),
            headers={
                "Authorization": f"Bearer {self.api_key}",
                "Content-Type": "application/json",
                "Accept": "application/json",
            },
            method="POST",
        )
        try:
            with WallClockTimeout(self.completion_timeout_seconds):
                with request.urlopen(req, timeout=self.completion_timeout_seconds) as response:
                    raw = response.read().decode("utf-8", "replace")
                    return json.loads(raw)
        except CompletionTimeoutError as exc:
            raise RuntimeError(str(exc)) from exc
        except error.HTTPError as exc:
            raw = exc.read().decode("utf-8", "replace")
            raise RuntimeError(f"POST /v1/completions -> {exc.code}: {raw}") from exc


def validate_fixture_manifest(
    fixture_manifest: dict[str, Any],
    fixture_root: pathlib.Path,
    repo_root: pathlib.Path,
) -> list[str]:
    errors: list[str] = []
    version = fixture_manifest.get("fixture_version")
    if not isinstance(version, str) or not version.strip():
        errors.append("fixture_manifest.fixture_version must be a non-empty string")

    files = fixture_manifest.get("fixture_files")
    if not isinstance(files, list) or not files:
        errors.append("fixture_manifest.fixture_files must be a non-empty array")
    else:
        for relative_path in files:
            if not isinstance(relative_path, str):
                errors.append("fixture_manifest.fixture_files entries must be strings")
                continue
            candidate = repo_root / relative_path
            if not candidate.is_file():
                errors.append(f"fixture file listed in manifest is missing: {relative_path}")
            else:
                try:
                    candidate.relative_to(fixture_root)
                except ValueError:
                    errors.append(f"fixture file is outside fixture root: {relative_path}")

    change_notes = fixture_manifest.get("change_notes")
    if not isinstance(change_notes, list) or not change_notes:
        errors.append("fixture_manifest.change_notes must be a non-empty array")

    size_bytes = fixture_manifest.get("fixture_size_bytes")
    if not isinstance(size_bytes, int) or size_bytes < 0:
        errors.append("fixture_manifest.fixture_size_bytes must be a non-negative integer")
    else:
        actual_size_bytes = compute_fixture_size_bytes(fixture_root)
        if size_bytes != actual_size_bytes:
            errors.append(
                "fixture_manifest.fixture_size_bytes must match the current fixture tree size: "
                f"expected {actual_size_bytes}, found {size_bytes}"
            )
        if actual_size_bytes >= 5 * 1024 * 1024:
            errors.append("fixture tree must remain under 5 MB")

    return errors


def validate_cases(
    cases_doc: dict[str, Any],
    prompt_templates_doc: dict[str, Any],
    benchmark_profiles_doc: dict[str, Any],
    fixture_root: pathlib.Path,
    repo_root: pathlib.Path,
    require_all_workloads: bool,
) -> tuple[list[str], list[dict[str, Any]]]:
    errors: list[str] = []
    cases = cases_doc.get("cases")
    if not isinstance(cases, list) or not cases:
        return ["config/evaluation_cases.json must contain a non-empty cases array"], []

    templates = prompt_templates_doc.get("templates", {})
    profiles_by_id = {
        profile.get("id"): profile
        for profile in benchmark_profiles_doc.get("profiles", [])
        if isinstance(profile, dict) and profile.get("id")
    }

    seen_case_ids: set[str] = set()
    case_records: list[dict[str, Any]] = []
    workloads_seen: set[str] = set()

    for case in cases:
        if not isinstance(case, dict):
            errors.append("Each evaluation case must be an object")
            continue

        case_id = case.get("case_id")
        if not isinstance(case_id, str) or not case_id.strip():
            errors.append("Each case requires a non-empty case_id")
            continue
        if case_id in seen_case_ids:
            errors.append(f"Duplicate case_id: {case_id}")
            continue
        seen_case_ids.add(case_id)

        workload = case.get("workload")
        if workload not in ALLOWED_WORKLOADS:
            errors.append(f"Case {case_id} has invalid workload: {workload}")
            continue
        workloads_seen.add(workload)

        template_id = case.get("prompt_template_id")
        if template_id not in templates:
            errors.append(f"Case {case_id} references missing prompt_template_id: {template_id}")
            continue

        placeholder_values = case.get("placeholder_values")
        if not isinstance(placeholder_values, dict):
            errors.append(f"Case {case_id} placeholder_values must be an object")
            continue

        expected_placeholders = required_placeholders(templates[template_id]["prompt"])
        missing_placeholders = sorted(expected_placeholders - set(placeholder_values.keys()))
        if missing_placeholders:
            errors.append(
                f"Case {case_id} is missing placeholders for template {template_id}: {', '.join(missing_placeholders)}"
            )

        profile_id = case.get("profile_id")
        if not isinstance(profile_id, str) or profile_id not in profiles_by_id:
            errors.append(f"Case {case_id} references missing profile_id: {profile_id}")
            continue
        profile_workload = profiles_by_id[profile_id].get("workload")
        if profile_workload != workload:
            errors.append(
                f"Case {case_id} workload {workload} does not match profile {profile_id} workload {profile_workload}"
            )

        fixture_paths = case.get("fixture_paths")
        if not isinstance(fixture_paths, list) or not fixture_paths:
            errors.append(f"Case {case_id} fixture_paths must be a non-empty array")
        else:
            for relative_path in fixture_paths:
                if not isinstance(relative_path, str):
                    errors.append(f"Case {case_id} fixture path entries must be strings")
                    continue
                candidate = repo_root / relative_path
                if not candidate.exists():
                    errors.append(f"Case {case_id} references missing fixture path: {relative_path}")
                else:
                    try:
                        candidate.resolve().relative_to(fixture_root.resolve())
                    except ValueError:
                        errors.append(f"Case {case_id} fixture path outside fixture root: {relative_path}")

        prompt_context_paths = case.get("prompt_context_paths")
        if prompt_context_paths is not None:
            if not isinstance(prompt_context_paths, list) or not prompt_context_paths:
                errors.append(f"Case {case_id} prompt_context_paths must be a non-empty array when provided")
            else:
                for relative_path in prompt_context_paths:
                    if not isinstance(relative_path, str):
                        errors.append(f"Case {case_id} prompt_context_paths entries must be strings")
                        continue
                    candidate = repo_root / relative_path
                    if not candidate.exists():
                        errors.append(f"Case {case_id} references missing prompt context path: {relative_path}")
                    else:
                        try:
                            candidate.resolve().relative_to(fixture_root.resolve())
                        except ValueError:
                            errors.append(f"Case {case_id} prompt context path outside fixture root: {relative_path}")

        expected_answer = case.get("expected_answer")
        if not isinstance(expected_answer, dict):
            errors.append(f"Case {case_id} expected_answer must be an object")
            continue

        required_facts = expected_answer.get("required_facts")
        if not isinstance(required_facts, list) or not required_facts:
            errors.append(f"Case {case_id} required_facts must be a non-empty array")
        else:
            for fact in required_facts:
                if not isinstance(fact, dict):
                    errors.append(f"Case {case_id} required_facts entries must be objects")
                    continue
                if not isinstance(fact.get("fact"), str) or not fact["fact"].strip():
                    errors.append(f"Case {case_id} required_facts fact must be a non-empty string")
                if fact.get("importance") not in ALLOWED_IMPORTANCE:
                    errors.append(f"Case {case_id} required_facts importance must be one of {sorted(ALLOWED_IMPORTANCE)}")

        forbidden_claims = expected_answer.get("forbidden_claims")
        if not isinstance(forbidden_claims, list):
            errors.append(f"Case {case_id} forbidden_claims must be an array")
        else:
            for claim in forbidden_claims:
                if not isinstance(claim, dict):
                    errors.append(f"Case {case_id} forbidden_claims entries must be objects")
                    continue
                if not isinstance(claim.get("claim"), str) or not claim["claim"].strip():
                    errors.append(f"Case {case_id} forbidden_claim claim must be a non-empty string")
                if claim.get("severity") not in ALLOWED_SEVERITY:
                    errors.append(f"Case {case_id} forbidden_claim severity must be one of {sorted(ALLOWED_SEVERITY)}")

        quality_signals = expected_answer.get("quality_signals")
        if not isinstance(quality_signals, list):
            errors.append(f"Case {case_id} quality_signals must be an array")
        else:
            for signal in quality_signals:
                if not isinstance(signal, dict):
                    errors.append(f"Case {case_id} quality_signals entries must be objects")
                    continue
                if not isinstance(signal.get("signal"), str) or not signal["signal"].strip():
                    errors.append(f"Case {case_id} quality_signals signal must be a non-empty string")
                if signal.get("detectable") not in ALLOWED_DETECTABLE:
                    errors.append(f"Case {case_id} quality_signals detectable must be one of {sorted(ALLOWED_DETECTABLE)}")
                if not isinstance(signal.get("description"), str) or not signal["description"].strip():
                    errors.append(f"Case {case_id} quality_signals description must be a non-empty string")

        manual_review_notes = expected_answer.get("manual_review_notes")
        if manual_review_notes is not None and not isinstance(manual_review_notes, str):
            errors.append(f"Case {case_id} manual_review_notes must be a string or null")

        case_records.append(case)

    missing_workloads = sorted(ALLOWED_WORKLOADS - workloads_seen)
    if require_all_workloads and missing_workloads:
        errors.append("Missing at least one case for workloads: " + ", ".join(missing_workloads))

    return errors, case_records


def build_case_prompt_record(
    case: dict[str, Any],
    prompt_templates_doc: dict[str, Any],
    benchmark_profiles_doc: dict[str, Any],
    fixture_hash: str,
    fixture_version: str,
    model_id: str,
    assistant_model_id: str | None,
    applied_settings: dict[str, Any],
    mtp_enabled: bool,
    repo_root: pathlib.Path,
) -> dict[str, Any]:
    templates = prompt_templates_doc["templates"]
    profile_lookup = {
        profile["id"]: profile
        for profile in benchmark_profiles_doc["profiles"]
    }
    template = templates[case["prompt_template_id"]]["prompt"]
    rendered_prompt = render_prompt(template, case["placeholder_values"])
    prompt_context_blocks, prompt_context_metadata = read_prompt_context_files(case, repo_root, repo_root / "fixtures")
    if prompt_context_blocks:
        rendered_prompt = rendered_prompt.rstrip() + "\n\n" + "\n\n".join(prompt_context_blocks)
    profile = profile_lookup[case["profile_id"]]
    settings = dict(applied_settings)

    return {
        "schema_version": SCHEMA_VERSION,
        "case_id": case["case_id"],
        "model_id": model_id,
        "assistant_model_id": assistant_model_id,
        "profile_id": case["profile_id"],
        "workload": case["workload"],
        "mtp_enabled": mtp_enabled,
        "fixture_version": fixture_version,
        "fixture_hash": fixture_hash,
        "prompt_template_id": case["prompt_template_id"],
        "placeholder_values": case["placeholder_values"],
        "fixture_paths": case["fixture_paths"],
        "prompt_context_paths": case.get("prompt_context_paths") or [],
        "prompt_context_metadata": prompt_context_metadata,
        "settings": settings,
        "prompt_text": rendered_prompt,
        "expected_answer": case["expected_answer"],
        "source_paths": [
            relative_to_repo(repo_root / "config/evaluation_cases.json", repo_root),
            relative_to_repo(repo_root / "config/prompt_templates.json", repo_root),
        ] + case["fixture_paths"] + list(case.get("prompt_context_paths") or []),
    }


def list_cases(case_records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    return [
        {
            "case_id": case["case_id"],
            "workload": case["workload"],
            "profile_id": case["profile_id"],
            "prompt_template_id": case["prompt_template_id"],
            "fixture_paths": case["fixture_paths"],
        }
        for case in case_records
    ]


def validate_only_output(
    fixture_manifest: dict[str, Any],
    fixture_hash: str,
    case_records: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "schema_version": SCHEMA_VERSION,
        "fixture_version": fixture_manifest["fixture_version"],
        "fixture_hash": fixture_hash,
        "fixture_size_bytes": fixture_manifest["fixture_size_bytes"],
        "case_count": len(case_records),
        "status": "success",
    }


def write_case_artifacts(
    outdir: pathlib.Path,
    evaluation_run_id: str,
    model_id: str,
    assistant_model_id: str | None,
    profile_id: str,
    fixture_version: str,
    fixture_hash: str,
    case_prompt_record: dict[str, Any],
    output_text: str | None,
    output_payload: dict[str, Any],
    repo_root: pathlib.Path,
) -> pathlib.Path:
    case_dir = outdir / case_prompt_record["case_id"]
    raw_dir = case_dir / "raw"
    derived_dir = case_dir / "derived"
    raw_dir.mkdir(parents=True, exist_ok=True)
    derived_dir.mkdir(parents=True, exist_ok=True)

    raw_output_doc = {
        "schema_version": SCHEMA_VERSION,
        "evaluation_run_id": evaluation_run_id,
        "run_id": None,
        "normalization_id": None,
        "recommendation_id": None,
        "created_at": dt.datetime.now(dt.timezone.utc).isoformat(),
        "model_id": model_id,
        "assistant_model_id": assistant_model_id,
        "profile_id": profile_id,
        "workload": case_prompt_record["workload"],
        "mtp_enabled": case_prompt_record["mtp_enabled"],
        "fixture_version": fixture_version,
        "fixture_hash": fixture_hash,
        "case_id": case_prompt_record["case_id"],
        "prompt_text": case_prompt_record["prompt_text"],
        "settings": case_prompt_record["settings"],
        "output_text": output_text,
        "provider_payload": output_payload,
        "source_paths": case_prompt_record["source_paths"],
    }
    save_json(raw_dir / "model_output.json", raw_output_doc)

    scored_doc = {
        "schema_version": SCHEMA_VERSION,
        "evaluation_run_id": evaluation_run_id,
        "run_id": None,
        "normalization_id": None,
        "recommendation_id": None,
        "created_at": dt.datetime.now(dt.timezone.utc).isoformat(),
        "model_id": model_id,
        "assistant_model_id": assistant_model_id,
        "profile_id": profile_id,
        "workload": case_prompt_record["workload"],
        "mtp_enabled": case_prompt_record["mtp_enabled"],
        "case_id": case_prompt_record["case_id"],
        "fixture_version": fixture_version,
        "fixture_hash": fixture_hash,
        "expected_answer": case_prompt_record["expected_answer"],
        "automatic_checks": build_automatic_checks(case_prompt_record["expected_answer"], output_text or ""),
        "manual_review_notes": case_prompt_record["expected_answer"].get("manual_review_notes"),
        "source_paths": [
            relative_to_repo(raw_dir / "model_output.json", repo_root),
            *case_prompt_record["source_paths"],
        ],
    }
    save_json(derived_dir / "scoring.json", scored_doc)
    return case_dir / "case_result.json"


def build_case_result_index(
    case_prompt_record: dict[str, Any],
    repo_root: pathlib.Path,
    case_result_path: pathlib.Path,
) -> dict[str, Any]:
    return {
        "schema_version": SCHEMA_VERSION,
        "case_id": case_prompt_record["case_id"],
        "workload": case_prompt_record["workload"],
        "profile_id": case_prompt_record["profile_id"],
        "prompt_template_id": case_prompt_record["prompt_template_id"],
        "mtp_enabled": case_prompt_record["mtp_enabled"],
        "raw_output_path": relative_to_repo(case_result_path.parent / "raw/model_output.json", repo_root),
        "scoring_path": relative_to_repo(case_result_path.parent / "derived/scoring.json", repo_root),
        "source_paths": case_prompt_record["source_paths"],
    }


def extract_completion_text(response_doc: dict[str, Any]) -> str:
    choices = response_doc.get("choices")
    if isinstance(choices, list) and choices:
        first = choices[0]
        if isinstance(first, dict) and isinstance(first.get("text"), str):
            return first["text"]
    raise RuntimeError("Unsupported response payload from /v1/completions: missing choices[0].text")


def prepare_live_profile_settings(
    outdir: pathlib.Path,
    base_url: str,
    api_key: str,
    model_id: str,
    assistant_model_id: str | None,
    profile_doc: dict[str, Any],
    repo_root: pathlib.Path,
) -> tuple[dict[str, Any], bool, str | None, list[str], list[str]]:
    client = OMLXHarness(base_url)
    admin_dir = outdir / "admin"
    source_paths: list[str] = []
    warnings: list[str] = []

    login = client.login_admin(api_key)
    save_json(admin_dir / "01_login.json", login)
    source_paths.append(relative_to_repo(admin_dir / "01_login.json", repo_root))

    profile_fields = client.get_profile_fields()
    save_json(admin_dir / "02_profile_fields.json", profile_fields)
    source_paths.append(relative_to_repo(admin_dir / "02_profile_fields.json", repo_root))
    profile_field_names = list_profile_field_names(profile_fields)

    models_doc = client.list_models()
    save_json(admin_dir / "03_models.json", models_doc)
    source_paths.append(relative_to_repo(admin_dir / "03_models.json", repo_root))

    selected_model = pick_model(models_doc, model_id)
    save_json(admin_dir / "04_selected_model.json", selected_model)
    source_paths.append(relative_to_repo(admin_dir / "04_selected_model.json", repo_root))

    settings_overrides, mtp_enabled = build_profile_settings(profile_doc, "profile", selected_model=selected_model)
    assistant_field = ensure_vlm_mtp_assistant_configuration(
        profile_id=profile_doc.get("id") or "unknown-profile",
        settings_overrides=settings_overrides,
        current_settings=selected_model.get("settings") or {},
        profile_field_names=profile_field_names,
        assistant_model_id=assistant_model_id,
    )
    effective_assistant_model_id = assistant_model_id
    if assistant_field:
        settings_overrides[assistant_field] = None

    if assistant_model_id:
        try:
            assistant_model = pick_model(models_doc, assistant_model_id)
        except ValueError:
            if settings_overrides.get("vlm_mtp_enabled"):
                raise ValueError(f"assistant model not found in oMLX inventory: {assistant_model_id}")
            effective_assistant_model_id = None
            warnings.append(f"assistant model not found in oMLX inventory: {assistant_model_id}")
        else:
            save_json(admin_dir / "05_assistant_model.json", assistant_model)
            source_paths.append(relative_to_repo(admin_dir / "05_assistant_model.json", repo_root))
            if not assistant_field:
                effective_assistant_model_id = None
                warnings.append("selected model does not advertise an assistant draft-model field; continuing target-only")
            else:
                settings_overrides[assistant_field] = assistant_model_id

    merged_settings = merge_settings(selected_model.get("settings") or {}, settings_overrides)
    save_json(admin_dir / "06_settings_request.json", merged_settings)
    source_paths.append(relative_to_repo(admin_dir / "06_settings_request.json", repo_root))

    settings_response = client.update_model_settings(model_id, merged_settings)
    save_json(admin_dir / "07_settings_response.json", settings_response)
    source_paths.append(relative_to_repo(admin_dir / "07_settings_response.json", repo_root))

    return merged_settings, mtp_enabled, effective_assistant_model_id, source_paths, warnings


def main() -> int:
    parser = argparse.ArgumentParser(description="Run deterministic prompt-quality evaluations against synthetic fixtures")
    parser.add_argument("--model-id", required=True)
    parser.add_argument("--assistant-model-id", default=None)
    parser.add_argument("--profile-id", required=True)
    parser.add_argument("--cases", default="config/evaluation_cases.json")
    parser.add_argument("--fixture-root", default="fixtures/synthetic_repo")
    parser.add_argument("--results-dir", default="results/evaluations")
    parser.add_argument("--prompt-templates", default="config/prompt_templates.json")
    parser.add_argument("--benchmark-profiles", default="config/benchmark_profiles.json")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument("--api-key", default=None)
    parser.add_argument("--max-tokens-override", type=int, default=None)
    parser.add_argument("--skip-workload-coverage-check", action="store_true")
    parser.add_argument("--list-cases", action="store_true")
    parser.add_argument("--validate-only", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--case-id", action="append", default=[])
    parser.add_argument("--completion-timeout", type=int, default=DEFAULT_COMPLETION_TIMEOUT_SECONDS)
    args = parser.parse_args()

    repo_root = pathlib.Path(__file__).resolve().parents[2]
    cases_path = (repo_root / args.cases).resolve()
    fixture_root = (repo_root / args.fixture_root).resolve()
    results_dir = (repo_root / args.results_dir).resolve()
    prompt_templates_path = (repo_root / args.prompt_templates).resolve()
    benchmark_profiles_path = (repo_root / args.benchmark_profiles).resolve()
    fixture_manifest_path = fixture_root / "fixture_manifest.json"

    if not fixture_root.exists():
        raise SystemExit(f"Fixture root does not exist: {fixture_root}")
    if not fixture_manifest_path.exists():
        raise SystemExit(f"Fixture manifest is missing: {fixture_manifest_path}")

    cases_doc = load_json(cases_path)
    prompt_templates_doc = load_json(prompt_templates_path)
    benchmark_profiles_doc = load_json(benchmark_profiles_path)
    fixture_manifest = load_json(fixture_manifest_path)

    fixture_manifest_errors = validate_fixture_manifest(fixture_manifest, fixture_root, repo_root)
    case_errors, case_records = validate_cases(
        cases_doc,
        prompt_templates_doc,
        benchmark_profiles_doc,
        fixture_root,
        repo_root,
        require_all_workloads=not args.skip_workload_coverage_check,
    )

    fixture_hash = compute_fixture_hash(
        fixture_root,
        cases_doc,
        fixture_manifest,
        prompt_templates_doc,
        benchmark_profiles_doc,
        repo_root,
    )

    filtered_cases = case_records
    if args.case_id:
        selected = set(args.case_id)
        filtered_cases = [case for case in case_records if case["case_id"] in selected]
        missing_ids = sorted(selected - {case["case_id"] for case in filtered_cases})
        if missing_ids:
            case_errors.append("Requested case_id values not found: " + ", ".join(missing_ids))

    filtered_cases = [case for case in filtered_cases if case.get("profile_id") == args.profile_id]
    if not filtered_cases:
        case_errors.append(f"No evaluation cases found for profile_id={args.profile_id}")

    all_errors = fixture_manifest_errors + case_errors
    if args.list_cases:
        print(json.dumps({
            "schema_version": SCHEMA_VERSION,
            "fixture_version": fixture_manifest.get("fixture_version"),
            "fixture_hash": fixture_hash,
            "cases": list_cases(filtered_cases),
            "errors": all_errors,
        }, indent=2))
        return 0 if not all_errors else 1

    if args.validate_only:
        payload = validate_only_output(fixture_manifest, fixture_hash, filtered_cases)
        payload["errors"] = all_errors
        print(json.dumps(payload, indent=2))
        return 0 if not all_errors else 1

    if all_errors:
        print(json.dumps({"status": "failed", "errors": all_errors}, indent=2), file=sys.stderr)
        return 1

    profile_lookup = {
        profile["id"]: profile
        for profile in benchmark_profiles_doc["profiles"]
    }
    if args.profile_id not in profile_lookup:
        print(json.dumps({"status": "failed", "errors": [f"Unknown profile_id: {args.profile_id}"]}, indent=2), file=sys.stderr)
        return 1

    evaluation_run_id = build_evaluation_run_id(args.model_id, args.profile_id)
    outdir = results_dir / evaluation_run_id
    outdir.mkdir(parents=True, exist_ok=True)

    case_result_paths: list[str] = []
    execution_errors: list[str] = []
    generated_case_ids: list[str] = []
    profile_source_paths: list[str] = []
    effective_assistant_model_id = args.assistant_model_id

    api_key = args.api_key or os.environ.get("OMLX_API_KEY")
    client = None
    applied_settings = dict((profile_lookup[args.profile_id].get("settings") or {}))
    mtp_enabled = bool(applied_settings.get("mtp_enabled") or applied_settings.get("vlm_mtp_enabled"))
    if not args.dry_run:
        if not api_key:
            print(json.dumps({
                "status": "failed",
                "errors": ["OMLX_API_KEY is required unless --dry-run is set"],
            }, indent=2), file=sys.stderr)
            return 1
        try:
            applied_settings, mtp_enabled, effective_assistant_model_id, profile_source_paths, profile_warnings = prepare_live_profile_settings(
                outdir=outdir,
                base_url=args.base_url,
                api_key=api_key,
                model_id=args.model_id,
                assistant_model_id=args.assistant_model_id,
                profile_doc=profile_lookup[args.profile_id],
                repo_root=repo_root,
            )
            execution_errors.extend(profile_warnings)
        except Exception as exc:
            print(json.dumps({
                "status": "failed",
                "errors": [f"Failed to apply live evaluation settings: {exc}"],
            }, indent=2), file=sys.stderr)
            return 1
        client = PublicInferenceClient(args.base_url, api_key, completion_timeout_seconds=args.completion_timeout)

    if args.max_tokens_override is not None:
        applied_settings["max_tokens"] = args.max_tokens_override

    for case in filtered_cases:
        case_prompt_record = build_case_prompt_record(
            case=case,
            prompt_templates_doc=prompt_templates_doc,
            benchmark_profiles_doc=benchmark_profiles_doc,
            fixture_hash=fixture_hash,
            fixture_version=fixture_manifest["fixture_version"],
            model_id=args.model_id,
            assistant_model_id=effective_assistant_model_id,
            applied_settings=applied_settings,
            mtp_enabled=mtp_enabled,
            repo_root=repo_root,
        )
        case_prompt_record["source_paths"].extend(profile_source_paths)

        output_text = None
        provider_payload: dict[str, Any] = {"mode": "dry-run"}
        if client is not None:
            try:
                provider_payload = client.generate(
                    model_id=args.model_id,
                    prompt=case_prompt_record["prompt_text"],
                    settings=case_prompt_record["settings"],
                )
                output_text = extract_completion_text(provider_payload)
            except Exception as exc:
                execution_errors.append(f"Case {case['case_id']} failed: {exc}")
                provider_payload = {"error": str(exc)}

        case_result_path = write_case_artifacts(
            outdir=outdir,
            evaluation_run_id=evaluation_run_id,
            model_id=args.model_id,
            assistant_model_id=args.assistant_model_id,
            profile_id=args.profile_id,
            fixture_version=fixture_manifest["fixture_version"],
            fixture_hash=fixture_hash,
            case_prompt_record=case_prompt_record,
            output_text=output_text,
            output_payload=provider_payload,
            repo_root=repo_root,
        )
        case_result_index = build_case_result_index(case_prompt_record, repo_root, case_result_path)
        save_json(case_result_path, case_result_index)
        case_result_paths.append(relative_to_repo(case_result_path, repo_root))
        generated_case_ids.append(case["case_id"])

    status = "success"
    if execution_errors and len(execution_errors) == len(filtered_cases):
        status = "failed"
    elif execution_errors:
        status = "partial"
    elif args.dry_run:
        status = "success"

    if status not in TERMINAL_STATUSES:
        raise RuntimeError(f"Unexpected terminal status: {status}")

    manifest = {
        "schema_version": SCHEMA_VERSION,
        "run_id": None,
        "evaluation_run_id": evaluation_run_id,
        "normalization_id": None,
        "recommendation_id": None,
        "created_at": dt.datetime.now(dt.timezone.utc).isoformat(),
        "fixture_version": fixture_manifest["fixture_version"],
        "fixture_hash": fixture_hash,
        "model_id": args.model_id,
        "assistant_model_id": effective_assistant_model_id,
        "profile_id": args.profile_id,
        "workload": profile_lookup[args.profile_id]["workload"],
        "mtp_enabled": mtp_enabled,
        "case_result_paths": case_result_paths,
        "generated_case_ids": generated_case_ids,
        "status": status,
        "source_paths": [
            relative_to_repo(cases_path, repo_root),
            relative_to_repo(fixture_manifest_path, repo_root),
            relative_to_repo(prompt_templates_path, repo_root),
            relative_to_repo(benchmark_profiles_path, repo_root),
            *profile_source_paths,
        ],
        "errors": execution_errors,
    }
    save_json(outdir / "evaluation_manifest.json", manifest)
    print(json.dumps(manifest, indent=2))
    return 0 if status != "failed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
