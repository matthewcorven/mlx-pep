#!/usr/bin/env python3
import datetime as dt
import http.cookiejar
import json
import pathlib
import re
import time
from typing import Any, Dict, Iterable, List, Optional, Tuple
from urllib import error, parse, request


TERMINAL_SSE_TYPES = {"done", "upload_done", "upload_skipped", "error"}
ASSISTANT_SETTING_CANDIDATES = (
    "assistant_model_id",
    "draft_model_id",
    "speculative_model_id",
    "mtp_assistant_model_id",
    "vlm_mtp_draft_model",
    "specprefill_draft_model",
    "dflash_draft_model",
)


class OMLXHarness:
    def __init__(self, base_url: str):
        self.base_url = base_url.rstrip("/")
        self.cookies = http.cookiejar.CookieJar()
        self.opener = request.build_opener(request.HTTPCookieProcessor(self.cookies))

    def _url(self, path: str) -> str:
        return f"{self.base_url}{path}"

    def _json_request(self, method: str, path: str, body=None, timeout: int = 120):
        headers = {"Accept": "application/json"}
        data = None
        if body is not None:
            headers["Content-Type"] = "application/json"
            data = json.dumps(body).encode("utf-8")
        req = request.Request(self._url(path), data=data, headers=headers, method=method)
        try:
            with self.opener.open(req, timeout=timeout) as resp:
                raw = resp.read().decode("utf-8", "replace")
                return json.loads(raw) if raw else {}
        except error.HTTPError as exc:
            raw = exc.read().decode("utf-8", "replace")
            raise RuntimeError(f"{method} {path} -> {exc.code}: {raw}") from exc

    def login_admin(self, api_key: str, remember: bool = False):
        return self._json_request("POST", "/admin/api/login", {"api_key": api_key, "remember": remember})

    def list_models(self):
        return self._json_request("GET", "/admin/api/models")

    def get_profile_fields(self):
        return self._json_request("GET", "/admin/api/profile-fields")

    def get_generation_config(self, model_id: str):
        model_id = parse.quote(model_id, safe="")
        return self._json_request("GET", f"/admin/api/models/{model_id}/generation_config")

    def update_model_settings(self, model_id: str, settings: dict):
        model_id = parse.quote(model_id, safe="")
        return self._json_request("PUT", f"/admin/api/models/{model_id}/settings", settings, timeout=300)

    def start_benchmark(self, model_id: str, prompt_lengths, generation_length: int, batch_sizes):
        return self._json_request(
            "POST",
            "/admin/api/bench/start",
            {
                "model_id": model_id,
                "prompt_lengths": prompt_lengths,
                "generation_length": generation_length,
                "batch_sizes": batch_sizes,
            },
            timeout=300,
        )

    def get_benchmark_results(self, bench_id: str):
        bench_id = parse.quote(bench_id, safe="")
        return self._json_request("GET", f"/admin/api/bench/{bench_id}/results", timeout=300)

    def get_active_benchmark(self):
        return self._json_request("GET", "/admin/api/bench/active", timeout=120)

    def cancel_active_benchmark_if_running(self, wait_for_clear: bool = True, timeout: int = 60, poll_interval: float = 1.0):
        active = self.get_active_benchmark()
        if not active or not active.get("running"):
            return active
        bench_id = active.get("bench_id")
        if not bench_id:
            return active

        cancel_response = self.cancel_benchmark(bench_id)
        if not wait_for_clear:
            return cancel_response

        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            status = self.get_active_benchmark()
            if not status or not status.get("running"):
                return cancel_response
            if poll_interval > 0:
                time.sleep(poll_interval)
        return cancel_response

    def cancel_benchmark(self, bench_id: str):
        bench_id = parse.quote(bench_id, safe="")
        return self._json_request("POST", f"/admin/api/bench/{bench_id}/cancel", timeout=120)

    def stream_benchmark(self, bench_id: str, timeout: int = 3600):
        bench_id = parse.quote(bench_id, safe="")
        req = request.Request(
            self._url(f"/admin/api/bench/{bench_id}/stream"),
            headers={"Accept": "text/event-stream"},
            method="GET",
        )
        try:
            with self.opener.open(req, timeout=timeout) as resp:
                data_lines = []
                for raw in resp:
                    line = raw.decode("utf-8", "replace").rstrip("\r\n")
                    if not line:
                        if data_lines:
                            payload = "".join(data_lines)
                            data_lines.clear()
                            event = json.loads(payload)
                            yield event
                            if event.get("type") in TERMINAL_SSE_TYPES:
                                return
                        continue
                    if line.startswith(":"):
                        continue
                    if line.startswith("data:"):
                        data_lines.append(line[5:].lstrip())
        except error.HTTPError as exc:
            raw = exc.read().decode("utf-8", "replace")
            raise RuntimeError(f"GET /admin/api/bench/{bench_id}/stream -> {exc.code}: {raw}") from exc


def save_json(path: pathlib.Path, value: Any):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def load_json(path: pathlib.Path):
    return json.loads(path.read_text(encoding="utf-8"))


def slugify_model_id(model_id: str) -> str:
    slug = re.sub(r"[^a-zA-Z0-9]+", "-", model_id).strip("-").lower()
    return slug or "model"


def build_run_id(model_id: str, suite_or_profile: str, now: Optional[dt.datetime] = None) -> str:
    timestamp = (now or dt.datetime.now()).strftime("%Y%m%d-%H%M%S")
    return f"{timestamp}-{slugify_model_id(model_id)}-{suite_or_profile}"


def relative_to_repo(path: pathlib.Path, repo_root: pathlib.Path) -> str:
    return str(path.relative_to(repo_root))


def resolve_profile(profiles_doc: dict, profile_id: str) -> dict:
    for profile in profiles_doc.get("profiles", []):
        if profile.get("id") == profile_id:
            return profile
    raise ValueError(f"Profile not found: {profile_id}")


def resolve_smoke_profiles(smoke_doc: dict) -> List[str]:
    return list(smoke_doc.get("profiles", []))


def merge_settings(current_settings: dict, overrides: dict) -> dict:
    merged = dict(current_settings or {})
    merged.update(overrides or {})
    return merged


def pick_model(models_doc: dict, model_id: str) -> dict:
    for model in models_doc.get("models", []):
        if model.get("id") == model_id:
            return model
    raise ValueError(f"Model not found in /admin/api/models: {model_id}")


def list_profile_field_names(profile_fields_doc: dict) -> List[str]:
    if isinstance(profile_fields_doc, dict):
        if isinstance(profile_fields_doc.get("fields"), list):
            fields = profile_fields_doc["fields"]
        else:
            fields = []
    elif isinstance(profile_fields_doc, list):
        fields = profile_fields_doc
    else:
        fields = []

    names: List[str] = []
    for item in fields:
        if isinstance(item, dict):
            name = item.get("name") or item.get("id")
            if isinstance(name, str):
                names.append(name)
        elif isinstance(item, str):
            names.append(item)
    return names


def detect_assistant_setting_field(current_settings: dict, profile_field_names: Iterable[str]) -> Optional[str]:
    field_name_set = set(profile_field_names)
    for candidate in ASSISTANT_SETTING_CANDIDATES:
        if candidate in current_settings or candidate in field_name_set:
            return candidate
    return None


def ensure_vlm_mtp_assistant_configuration(
    profile_id: str,
    settings_overrides: dict,
    current_settings: dict,
    profile_field_names: Iterable[str],
    assistant_model_id: Optional[str],
) -> Optional[str]:
    assistant_field = detect_assistant_setting_field(current_settings, profile_field_names)
    if not settings_overrides.get("vlm_mtp_enabled"):
        return assistant_field

    if not assistant_field:
        raise ValueError(
            f"Profile {profile_id} enables VLM MTP but the target model does not advertise a draft-model setting field."
        )

    if not assistant_model_id:
        raise ValueError(
            f"Profile {profile_id} enables VLM MTP and requires --assistant-model-id or a topology assistant_model_id. "
            f"Clean-start runs cannot inherit {assistant_field} from existing server state."
        )

    return assistant_field


def build_profile_settings(profile: dict, mtp_mode: str, selected_model: Optional[dict] = None) -> Tuple[dict, bool]:
    settings = dict(profile.get("settings") or {})
    selected_settings = dict((selected_model or {}).get("settings") or {})

    has_vlm_mtp = "vlm_mtp_enabled" in settings or "vlm_mtp_enabled" in selected_settings
    has_native_mtp = "mtp_enabled" in settings or "mtp_enabled" in selected_settings
    native_mtp_supported = bool((selected_model or {}).get("mtp_compatible"))

    if mtp_mode == "on":
        if has_vlm_mtp:
            settings["vlm_mtp_enabled"] = True
        if has_native_mtp:
            settings["mtp_enabled"] = native_mtp_supported and not has_vlm_mtp
    elif mtp_mode == "off":
        if has_vlm_mtp:
            settings["vlm_mtp_enabled"] = False
        if has_native_mtp:
            settings["mtp_enabled"] = False
    elif mtp_mode == "profile":
        if has_vlm_mtp and "vlm_mtp_enabled" not in settings:
            settings["vlm_mtp_enabled"] = bool(selected_settings.get("vlm_mtp_enabled"))
        if has_native_mtp and "mtp_enabled" not in settings:
            settings["mtp_enabled"] = bool(selected_settings.get("mtp_enabled"))
    else:
        raise ValueError(f"Unsupported mtp mode: {mtp_mode}")

    if has_native_mtp and has_vlm_mtp and not native_mtp_supported:
        settings["mtp_enabled"] = False

    mtp_enabled = bool(settings.get("vlm_mtp_enabled") or settings.get("mtp_enabled"))
    return settings, mtp_enabled


def benchmark_payload_for_profile(profile: dict) -> dict:
    benchmark = dict(profile.get("benchmark") or {})
    return {
        "prompt_lengths": benchmark.get("prompt_lengths", [1024]),
        "generation_length": benchmark.get("generation_length", 128),
        "batch_sizes": benchmark.get("batch_sizes", []),
    }


def record_probe_not_attempted(
    run_id: str,
    model_id: str,
    assistant_model_id: Optional[str],
    candidate_sources: List[dict],
    fallback_action: str,
    evidence_paths: List[str],
    failure_reason: Optional[str],
) -> dict:
    return {
        "schema_version": "1.0",
        "run_id": run_id,
        "model_id": model_id,
        "assistant_model_id": assistant_model_id,
        "candidate_sources": candidate_sources,
        "omlx_inventory_check": "not_attempted" if not assistant_model_id else "not_found",
        "probe_attempted": False,
        "probe_status": "not_attempted" if not assistant_model_id else "unsupported",
        "failure_reason": failure_reason,
        "fallback_action": fallback_action,
        "evidence_paths": evidence_paths,
    }


def _seed_base_url_parts(base_url_seed: str) -> Tuple[str, str, int]:
    parsed = parse.urlparse(base_url_seed)
    scheme = parsed.scheme or "http"
    host = parsed.hostname or "127.0.0.1"
    if parsed.port is not None:
        port = parsed.port
    elif scheme == "https":
        port = 443
    else:
        port = 8000
    return scheme, host, port


def build_seed_base_url(base_url_seed: str, port: int) -> str:
    scheme, host, _ = _seed_base_url_parts(base_url_seed)
    host_value = f"[{host}]" if ":" in host and not host.startswith("[") else host
    return f"{scheme}://{host_value}:{port}"


def build_instance_topology(
    recommendations: List[Dict[str, Any]],
    base_url_seed: str = "http://127.0.0.1:8000",
) -> Dict[str, Any]:
    grouped: Dict[str, List[Dict[str, Any]]] = {}
    for recommendation in recommendations:
        workload = recommendation.get("workload") or "unknown"
        grouped.setdefault(workload, []).append(recommendation)

    _, _, base_port = _seed_base_url_parts(base_url_seed)
    instances: List[Dict[str, Any]] = []
    workload_to_instance: Dict[str, str] = {}
    for workload, entries in sorted(grouped.items()):
        unique_signatures: Dict[Tuple[bool, str, str], Dict[str, Any]] = {}
        for entry in sorted(entries, key=lambda item: (item.get("rank") or 9999, item.get("profile_id") or "")):
            signature = (
                bool(entry.get("mtp_recommended")),
                entry.get("assistant_model_id") or "none",
                entry.get("profile_id") or "unknown",
            )
            unique_signatures.setdefault(signature, entry)

        for entry in unique_signatures.values():
            port = base_port + len(instances)
            instance_id = f"instance-{len(instances) + 1}"
            if workload not in workload_to_instance:
                workload_to_instance[workload] = instance_id
            instances.append(
                {
                    "instance_id": instance_id,
                    "port": port,
                    "base_url": build_seed_base_url(base_url_seed, port),
                    "workload": workload,
                    "profile_id": entry.get("profile_id"),
                    "mtp_enabled": bool(entry.get("mtp_recommended")),
                    "assistant_model_id": entry.get("assistant_model_id"),
                    "reason": "Distinct workload settings require a dedicated hosted instance on its own port.",
                }
            )

    instance_mode = "multi" if len(instances) > 1 else "single"
    if instance_mode == "single":
        summary = "Single hosted instance is sufficient for this workload recommendation."
    else:
        summary = (
            f"Multi-instance topology required: {len(instances)} hosted instance(s) on separate ports "
            f"must be managed by the operator for workload-specific recommendations."
        )

    return {
        "instance_mode": instance_mode,
        "instance_count": len(instances),
        "instances": instances,
        "workload_to_instance": workload_to_instance,
        "instance_topology_summary": summary,
    }


def match_topology_instance(
    topology: Dict[str, Any],
    workload: Optional[str],
    profile_id: Optional[str] = None,
    mtp_enabled: Optional[bool] = None,
    assistant_model_id: Optional[str] = None,
) -> Optional[Dict[str, Any]]:
    instances = list(topology.get("instances") or [])
    if not instances:
        return None

    def filter_instances(candidates: List[Dict[str, Any]], key: str, expected: Optional[Any]) -> List[Dict[str, Any]]:
        if expected is None:
            return candidates
        return [item for item in candidates if item.get(key) == expected]

    candidates = instances
    candidates = filter_instances(candidates, "workload", workload)
    candidates = filter_instances(candidates, "profile_id", profile_id)
    candidates = filter_instances(candidates, "mtp_enabled", mtp_enabled)

    if assistant_model_id is not None:
        exact_assistant = [item for item in candidates if item.get("assistant_model_id") == assistant_model_id]
        if exact_assistant:
            candidates = exact_assistant
    elif candidates:
        assistantless = [item for item in candidates if item.get("assistant_model_id") in (None, "")]
        if assistantless:
            candidates = assistantless

    if len(candidates) == 1:
        return candidates[0]

    workload_map = topology.get("workload_to_instance") or {}
    mapped_instance_id = workload_map.get(workload)
    if mapped_instance_id:
        mapped = next((item for item in candidates if item.get("instance_id") == mapped_instance_id), None)
        if mapped is not None:
            return mapped

    if candidates:
        return sorted(candidates, key=lambda item: str(item.get("instance_id") or ""))[0]
    return None
