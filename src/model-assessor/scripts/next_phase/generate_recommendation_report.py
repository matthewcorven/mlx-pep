#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import pathlib
import re
import statistics
from typing import Any, Iterable

from scripts.next_phase.runner_lib import build_instance_topology


SCHEMA_VERSION = "1.0"
BENCHMARK_METRICS = {
    "ttft_ms": "lower",
    "tpot_ms": "lower",
    "generation_tps": "higher",
    "prefill_tps": "higher",
    "end_to_end_latency_s": "lower",
    "total_throughput_tps": "higher",
    "peak_memory_bytes": "lower",
}
SHORT_WORKLOADS = {"short_code_research_tools", "short_coding"}
RESEARCH_WORKLOADS = {"short_code_research_tools", "long_code_research_tools", "deep_research"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Normalize benchmark and prompt-quality evidence into recommendation manifests and summaries"
    )
    parser.add_argument("--model-id", required=True)
    parser.add_argument("--assistant-model-id", default=None)
    parser.add_argument("--run-id", action="append", dest="run_ids")
    parser.add_argument("--evaluation-run-id", action="append", dest="evaluation_run_ids")
    parser.add_argument("--runs-dir", default="results/runs")
    parser.add_argument("--evaluations-dir", default="results/evaluations")
    parser.add_argument("--normalized-dir", default="results/normalized")
    parser.add_argument("--recommendations-dir", default="results/recommendations")
    parser.add_argument("--summaries-dir", default="results/summaries")
    parser.add_argument("--profiles-json", default="config/benchmark_profiles.json")
    return parser.parse_args()


def load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: pathlib.Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def save_text(path: pathlib.Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.rstrip() + "\n", encoding="utf-8")


def slugify(value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")
    return slug or "model"


def build_artifact_id(model_id: str, suffix: str, now: dt.datetime) -> str:
    return f"{now.strftime('%Y%m%d-%H%M%S')}-{slugify(model_id)}-{suffix}"


def now_iso(now: dt.datetime) -> str:
    return now.isoformat()


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")


def relative_to_repo(path: pathlib.Path, repo_root: pathlib.Path) -> str:
    return path.resolve().relative_to(repo_root.resolve()).as_posix()


def dedupe_sorted(values: Iterable[str]) -> list[str]:
    return sorted({value for value in values if value})


def as_float(value: Any) -> float | None:
    if isinstance(value, (int, float)):
        return float(value)
    return None


def as_int(value: Any) -> int | None:
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value
    return None


def average(values: list[float]) -> float | None:
    if not values:
        return None
    return round(statistics.fmean(values), 4)


def metric_summary(values: list[float]) -> dict[str, Any]:
    if not values:
        return {"available": False, "mean": None, "min": None, "max": None, "count": 0}
    return {
        "available": True,
        "mean": round(statistics.fmean(values), 4),
        "min": round(min(values), 4),
        "max": round(max(values), 4),
        "count": len(values),
    }


def hash_settings(settings: dict[str, Any] | None) -> str | None:
    if settings is None:
        return None
    return hashlib.sha256(canonical_json_bytes(settings)).hexdigest()


def discover_manifest_paths(root: pathlib.Path, manifest_name: str) -> list[pathlib.Path]:
    if not root.exists():
        return []
    return sorted(path for path in root.glob(f"*/{manifest_name}") if path.is_file())


def build_candidate_id(
    workload: str | None,
    profile_id: str | None,
    mtp_enabled: bool | None,
    assistant_model_id: str | None,
    settings_hash: str | None,
) -> str:
    assistant_slug = slugify(assistant_model_id or "no-assistant")
    profile_slug = slugify(profile_id or "unknown-profile")
    workload_slug = slugify(workload or "unknown-workload")
    mtp_slug = "mtp-on" if mtp_enabled else "mtp-off"
    hash_slug = (settings_hash or "no-settings")[:12]
    return f"{workload_slug}-{profile_slug}-{mtp_slug}-{assistant_slug}-{hash_slug}"


def build_empty_candidate(
    model_id: str,
    assistant_model_id: str | None,
    workload: str | None,
    profile_id: str | None,
    mtp_enabled: bool | None,
    settings: dict[str, Any] | None,
    settings_hash: str | None,
) -> dict[str, Any]:
    return {
        "candidate_id": build_candidate_id(workload, profile_id, mtp_enabled, assistant_model_id, settings_hash),
        "schema_version": SCHEMA_VERSION,
        "run_id": None,
        "evaluation_run_id": None,
        "normalization_id": None,
        "recommendation_id": None,
        "created_at": None,
        "model_id": model_id,
        "assistant_model_id": assistant_model_id,
        "profile_id": profile_id,
        "workload": workload,
        "mtp_enabled": mtp_enabled,
        "settings": settings,
        "settings_hash": settings_hash,
        "source_run_ids": set(),
        "source_evaluation_run_ids": set(),
        "source_paths": set(),
        "missing_evidence": set(),
        "benchmark_rows": [],
        "evaluation_cases": [],
        "assistant_probe_observations": [],
        "benchmark_statuses": set(),
    }


def get_or_create_candidate(
    candidate_index: dict[tuple[Any, ...], dict[str, Any]],
    model_id: str,
    assistant_model_id: str | None,
    workload: str | None,
    profile_id: str | None,
    mtp_enabled: bool | None,
    settings: dict[str, Any] | None,
    settings_hash: str | None,
) -> dict[str, Any]:
    key = (model_id, assistant_model_id, workload, profile_id, mtp_enabled)
    candidate = candidate_index.get(key)
    if candidate is None:
        candidate = build_empty_candidate(
            model_id=model_id,
            assistant_model_id=assistant_model_id,
            workload=workload,
            profile_id=profile_id,
            mtp_enabled=mtp_enabled,
            settings=settings,
            settings_hash=settings_hash,
        )
        candidate_index[key] = candidate
    elif candidate.get("settings") is None and settings is not None:
        candidate["settings"] = settings
    elif candidate.get("settings_hash") is None and settings_hash is not None:
        candidate["settings_hash"] = settings_hash
    elif candidate.get("settings_hash") != settings_hash and settings_hash is not None:
        candidate["missing_evidence"].add("Observed multiple settings variants for the same profile identity; review source_paths before treating the comparison as exact.")
    return candidate


def load_relative_json(repo_root: pathlib.Path, relative_path: str) -> tuple[dict[str, Any] | None, pathlib.Path | None, str | None]:
    path = repo_root / relative_path
    if not path.is_file():
        return None, None, f"missing source artifact: {relative_path}"
    try:
        return load_json(path), path, None
    except json.JSONDecodeError as exc:
        return None, path, f"invalid JSON in {relative_path}: {exc}"


def normalize_benchmark_row(row: dict[str, Any]) -> dict[str, Any]:
    return {
        "test_type": row.get("test_type") or "unknown",
        "prompt_tokens": as_int(row.get("prompt_tokens")),
        "completion_tokens": as_int(row.get("completion_tokens")),
        "prompt_length": as_int(row.get("pp")) or as_int(row.get("prompt_tokens")),
        "generation_length": as_int(row.get("tg")) or as_int(row.get("completion_tokens")),
        "ttft_ms": as_float(row.get("ttft_ms")),
        "tpot_ms": as_float(row.get("tpot_ms")),
        "generation_tps": as_float(row.get("gen_tps")),
        "prefill_tps": as_float(row.get("processing_tps")),
        "end_to_end_latency_s": as_float(row.get("e2e_latency_s")),
        "total_throughput_tps": as_float(row.get("total_throughput")),
        "peak_memory_bytes": as_float(row.get("peak_memory_bytes")),
        "cached_tokens": as_int(row.get("cached_tokens")),
    }


def summarize_benchmark_rows(rows: list[dict[str, Any]]) -> dict[str, Any]:
    summary = {
        "available": bool(rows),
        "row_count": len(rows),
        "test_types": sorted({row["test_type"] for row in rows if row.get("test_type")}),
        "prompt_lengths": sorted({row["prompt_length"] for row in rows if row.get("prompt_length") is not None}),
        "generation_lengths": sorted({row["generation_length"] for row in rows if row.get("generation_length") is not None}),
        "metrics": {},
        "missing_metrics": [],
    }
    for metric_name in BENCHMARK_METRICS:
        values = [row[metric_name] for row in rows if row.get(metric_name) is not None]
        summary["metrics"][metric_name] = metric_summary(values)
        if not values:
            summary["missing_metrics"].append(metric_name)
    return summary


def summarize_benchmark(rows: list[dict[str, Any]]) -> dict[str, Any]:
    by_test_type: dict[str, list[dict[str, Any]]] = {}
    for row in rows:
        by_test_type.setdefault(row["test_type"], []).append(row)
    return {
        "available": bool(rows),
        "overall": summarize_benchmark_rows(rows),
        "by_test_type": {test_type: summarize_benchmark_rows(group) for test_type, group in sorted(by_test_type.items())},
    }


def choose_benchmark_view(candidate: dict[str, Any], workload: str | None) -> dict[str, Any]:
    benchmark = candidate["benchmark"]
    by_test_type = benchmark.get("by_test_type") or {}
    if workload in SHORT_WORKLOADS and "single" in by_test_type:
        return by_test_type["single"]
    if workload not in SHORT_WORKLOADS and benchmark.get("overall", {}).get("available"):
        return benchmark["overall"]
    if "single" in by_test_type:
        return by_test_type["single"]
    return benchmark.get("overall") or {"available": False, "metrics": {}}


def extract_evaluation_case_summary(scoring_doc: dict[str, Any], scoring_path: str) -> dict[str, Any]:
    checks = scoring_doc.get("automatic_checks") or {}
    required_summary = checks.get("summary") or {}
    forbidden_checks = checks.get("forbidden_claim_checks") or []
    critical_hits = 0
    warning_hits = 0
    for item in forbidden_checks:
        if not item.get("found"):
            continue
        if item.get("severity") == "critical":
            critical_hits += 1
        else:
            warning_hits += 1
    required_hits = int(required_summary.get("required_fact_hits") or 0)
    required_total = int(required_summary.get("required_fact_total") or 0)
    ratio = round(required_hits / required_total, 4) if required_total else None
    return {
        "case_id": scoring_doc.get("case_id"),
        "required_fact_hits": required_hits,
        "required_fact_total": required_total,
        "required_fact_ratio": ratio,
        "critical_forbidden_hits": critical_hits,
        "warning_forbidden_hits": warning_hits,
        "automatic_quality_signal_count": int(required_summary.get("automatic_quality_signal_count") or 0),
        "manual_quality_signal_count": int(required_summary.get("manual_quality_signal_count") or 0),
        "manual_review_notes": scoring_doc.get("manual_review_notes"),
        "source_path": scoring_path,
    }


def summarize_evaluation_cases(cases: list[dict[str, Any]]) -> dict[str, Any]:
    required_hits = sum(case["required_fact_hits"] for case in cases)
    required_total = sum(case["required_fact_total"] for case in cases)
    critical_hits = sum(case["critical_forbidden_hits"] for case in cases)
    warning_hits = sum(case["warning_forbidden_hits"] for case in cases)
    automatic_signals = sum(case["automatic_quality_signal_count"] for case in cases)
    manual_signals = sum(case["manual_quality_signal_count"] for case in cases)
    return {
        "available": bool(cases),
        "case_count": len(cases),
        "required_fact_hits": required_hits,
        "required_fact_total": required_total,
        "required_fact_ratio": round(required_hits / required_total, 4) if required_total else None,
        "critical_forbidden_hits": critical_hits,
        "warning_forbidden_hits": warning_hits,
        "automatic_quality_signal_count": automatic_signals,
        "manual_quality_signal_count": manual_signals,
        "manual_review_pending": manual_signals > 0,
        "case_summaries": cases,
    }


def benchmark_sort_key(candidate: dict[str, Any], workload: str | None) -> tuple[Any, ...]:
    summary = choose_benchmark_view(candidate, workload)
    metrics = summary.get("metrics") or {}
    if not summary.get("available"):
        return (1,)

    def lower(metric_name: str) -> float:
        metric = metrics.get(metric_name) or {}
        if metric.get("mean") is None:
            return float("inf")
        return float(metric["mean"])

    def higher(metric_name: str) -> float:
        metric = metrics.get(metric_name) or {}
        if metric.get("mean") is None:
            return float("inf")
        return -float(metric["mean"])

    if workload in SHORT_WORKLOADS:
        return (
            0,
            lower("end_to_end_latency_s"),
            lower("ttft_ms"),
            lower("tpot_ms"),
            higher("total_throughput_tps"),
            higher("generation_tps"),
            lower("peak_memory_bytes"),
        )

    return (
        0,
        higher("total_throughput_tps"),
        higher("prefill_tps"),
        lower("end_to_end_latency_s"),
        lower("ttft_ms"),
        higher("generation_tps"),
        lower("peak_memory_bytes"),
    )


def evaluation_sort_key(candidate: dict[str, Any]) -> tuple[Any, ...]:
    evaluation = candidate["evaluation"]
    if not evaluation.get("available"):
        return (1,)
    ratio = evaluation.get("required_fact_ratio")
    if ratio is None:
        ratio = -1.0
    return (
        0,
        int(evaluation.get("critical_forbidden_hits") or 0),
        int(evaluation.get("warning_forbidden_hits") or 0),
        -float(ratio),
        -int(evaluation.get("case_count") or 0),
    )


def choose_evidence_order(workload: str | None, candidates: list[dict[str, Any]]) -> tuple[str, str]:
    if workload in RESEARCH_WORKLOADS:
        primary = "evaluation"
        secondary = "benchmark"
    else:
        primary = "benchmark"
        secondary = "evaluation"

    if not any(candidate[primary].get("available") for candidate in candidates):
        primary, secondary = secondary, primary
    return primary, secondary


def candidate_sort_key(candidate: dict[str, Any], workload: str | None, primary: str, secondary: str) -> tuple[Any, ...]:
    primary_available = 0 if candidate[primary].get("available") else 1
    secondary_available = 0 if candidate[secondary].get("available") else 1
    primary_key = evaluation_sort_key(candidate) if primary == "evaluation" else benchmark_sort_key(candidate, workload)
    secondary_key = evaluation_sort_key(candidate) if secondary == "evaluation" else benchmark_sort_key(candidate, workload)
    return (
        primary_available,
        primary_key,
        secondary_available,
        secondary_key,
        candidate.get("profile_id") or "",
        candidate.get("assistant_model_id") or "",
        candidate.get("settings_hash") or "",
    )


def infer_confidence(
    candidate: dict[str, Any],
    workload: str | None,
    ranked_candidates: list[dict[str, Any]],
    primary: str,
    secondary: str,
) -> str:
    primary_available = candidate[primary].get("available")
    secondary_available = candidate[secondary].get("available")
    if not primary_available and not secondary_available:
        return "insufficient_evidence"
    if not primary_available:
        return "insufficient_evidence"
    if not secondary_available:
        return "low"
    if len(ranked_candidates) == 1:
        return "medium"

    leader = ranked_candidates[0]
    if candidate["candidate_id"] != leader["candidate_id"]:
        return "low"

    runner_up = ranked_candidates[1]
    if leader.get("assistant_model_id") != runner_up.get("assistant_model_id"):
        return "low"

    leader_secondary = candidate_sort_key(leader, workload, secondary, primary)
    runner_secondary = candidate_sort_key(runner_up, workload, secondary, primary)
    if leader_secondary != runner_secondary:
        return "medium"
    return "low"


def format_metric(metric: dict[str, Any], suffix: str) -> str:
    if not metric.get("available"):
        return "missing"
    value = metric.get("mean")
    if value is None:
        return "missing"
    if suffix == "GiB":
        gib = float(value) / (1024 ** 3)
        return f"{gib:.2f} {suffix}"
    return f"{float(value):.3f} {suffix}"


def build_speed_summary(candidate: dict[str, Any], workload: str | None) -> str:
    benchmark = candidate.get("benchmark") or {}
    if not benchmark.get("available"):
        return "No benchmark evidence available."
    summary = choose_benchmark_view(candidate, workload)
    metrics = summary.get("metrics") or {}
    row_count = summary.get("row_count") or 0
    parts = [f"{row_count} benchmark result(s)"]
    parts.append(f"mean E2E {format_metric(metrics.get('end_to_end_latency_s') or {}, 's')}")
    parts.append(f"TTFT {format_metric(metrics.get('ttft_ms') or {}, 'ms')}")
    parts.append(f"generation TPS {format_metric(metrics.get('generation_tps') or {}, 'tok/s')}")
    parts.append(f"prefill TPS {format_metric(metrics.get('prefill_tps') or {}, 'tok/s')}")
    parts.append(f"total throughput {format_metric(metrics.get('total_throughput_tps') or {}, 'tok/s')}")
    if (metrics.get("peak_memory_bytes") or {}).get("available"):
        parts.append(f"peak memory {format_metric(metrics.get('peak_memory_bytes') or {}, 'GiB')}")
    return "; ".join(parts) + "."


def build_quality_summary(candidate: dict[str, Any]) -> str:
    evaluation = candidate.get("evaluation") or {}
    if not evaluation.get("available"):
        return "No prompt-quality evaluation evidence available."
    case_count = int(evaluation.get("case_count") or 0)
    required_hits = int(evaluation.get("required_fact_hits") or 0)
    required_total = int(evaluation.get("required_fact_total") or 0)
    critical_hits = int(evaluation.get("critical_forbidden_hits") or 0)
    warning_hits = int(evaluation.get("warning_forbidden_hits") or 0)
    ratio = evaluation.get("required_fact_ratio")
    if ratio is None:
        ratio_text = "missing"
    else:
        ratio_text = f"{ratio * 100:.1f}%"
    sentence = (
        f"Automatic checks across {case_count} case(s): required facts {required_hits}/"
        f"{required_total} ({ratio_text}), critical forbidden hits {critical_hits}, "
        f"warning forbidden hits {warning_hits}."
    )
    if evaluation.get("manual_review_pending"):
        sentence += " Manual quality signals remain pending review."
    return sentence


def validate_report_evidence(candidates: list[dict[str, Any]]) -> None:
    if not candidates:
        raise ValueError(
            "Recommendation report cannot be generated because no candidate evidence was found. "
            "Sequence requirement: run benchmark/probe collection first, then prompt-quality evaluation, then generate the recommendation report."
        )

    workloads: dict[str, dict[str, bool]] = {}
    for candidate in candidates:
        workload = candidate.get("workload") or "unknown"
        summary = workloads.setdefault(workload, {"benchmark": False, "evaluation": False})
        benchmark = bool((candidate.get("benchmark") or {}).get("available"))
        evaluation = bool((candidate.get("evaluation") or {}).get("available"))
        summary["benchmark"] = summary["benchmark"] or benchmark
        summary["evaluation"] = summary["evaluation"] or evaluation

    missing: list[str] = []
    for workload in sorted(workloads):
        summary = workloads[workload]
        if not summary["benchmark"]:
            missing.append(f"workload '{workload}' is missing benchmark evidence")
        if not summary["evaluation"]:
            missing.append(f"workload '{workload}' is missing prompt-quality evaluation evidence")

    if missing:
        raise ValueError(
            "Recommendation report cannot be generated because required evidence is incomplete: "
            + "; ".join(missing)
            + ". Sequence requirement: run benchmark/probe collection first, then prompt-quality evaluation, then generate the recommendation report."
        )


def build_candidate_caveats(
    candidate: dict[str, Any],
    ranked_candidates: list[dict[str, Any]],
    primary: str,
    secondary: str,
) -> tuple[list[str], list[str]]:
    tradeoffs: list[str] = []
    caveats: list[str] = []
    primary_evidence = candidate.get(primary) or {}
    secondary_evidence = candidate.get(secondary) or {}
    evaluation = candidate.get("evaluation") or {}
    if not primary_evidence.get("available"):
        caveats.append(f"Primary evidence type for this workload is missing: {primary}.")
    if not secondary_evidence.get("available"):
        caveats.append(f"Secondary evidence type for this workload is missing: {secondary}.")
    if evaluation.get("manual_review_pending"):
        caveats.append("Prompt-quality scoring currently reflects automatic checks only; manual signals are still pending.")
    for missing in sorted(candidate.get("missing_evidence", [])):
        caveats.append(missing)
    if candidate.get("assistant_model_id"):
        tradeoffs.append(f"This candidate includes assistant-model evidence for {candidate['assistant_model_id']}.")
    if any(other.get("assistant_model_id") != candidate.get("assistant_model_id") for other in ranked_candidates):
        caveats.append("Compared candidates do not all share the same assistant-model state.")
    benchmark = candidate.get("benchmark") or {}
    evaluation = candidate.get("evaluation") or {}
    if benchmark.get("available") and not evaluation.get("available"):
        tradeoffs.append("Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.")
    if evaluation.get("available") and not benchmark.get("available"):
        tradeoffs.append("Ranking leans on prompt-quality evidence because no benchmark result exists for this profile yet.")
    return tradeoffs, caveats


def build_assistant_summary(observations: list[dict[str, Any]]) -> dict[str, Any]:
    statuses = dedupe_sorted(observation.get("probe_status") for observation in observations)
    failure_reasons = dedupe_sorted(observation.get("failure_reason") for observation in observations)
    evidence_paths: list[str] = []
    for observation in observations:
        evidence_paths.extend(observation.get("evidence_paths") or [])
    return {
        "observation_count": len(observations),
        "probe_statuses": statuses,
        "failure_reasons": failure_reasons,
        "supported_count": sum(1 for observation in observations if observation.get("probe_status") == "supported"),
        "unsupported_count": sum(1 for observation in observations if observation.get("probe_status") == "unsupported"),
        "not_attempted_count": sum(1 for observation in observations if observation.get("probe_status") == "not_attempted"),
        "evidence_paths": dedupe_sorted(evidence_paths),
    }


def finalize_candidate(candidate: dict[str, Any], created_at: str) -> dict[str, Any]:
    benchmark = summarize_benchmark(candidate.pop("benchmark_rows"))
    evaluation = summarize_evaluation_cases(candidate.pop("evaluation_cases"))
    candidate["benchmark"] = benchmark
    candidate["evaluation"] = evaluation
    candidate["assistant_summary"] = build_assistant_summary(candidate.pop("assistant_probe_observations"))
    candidate["source_run_ids"] = sorted(candidate["source_run_ids"])
    candidate["source_evaluation_run_ids"] = sorted(candidate["source_evaluation_run_ids"])
    candidate["source_paths"] = sorted(candidate["source_paths"])
    candidate["missing_evidence"] = sorted(candidate["missing_evidence"])
    candidate["benchmark_statuses"] = sorted(candidate["benchmark_statuses"])
    candidate["created_at"] = created_at
    return candidate


def parse_profile_artifacts(relative_paths: list[str]) -> dict[str, str]:
    mapping: dict[str, str] = {}
    for relative_path in relative_paths:
        parts = pathlib.Path(relative_path).parts
        if len(parts) >= 5:
            mapping[parts[-2]] = relative_path
    return mapping


def collect_run_evidence(
    repo_root: pathlib.Path,
    manifest_path: pathlib.Path,
    args: argparse.Namespace,
    profiles_by_id: dict[str, dict[str, Any]],
    candidate_index: dict[tuple[Any, ...], dict[str, Any]],
    global_missing: set[str],
    assistant_observations: list[dict[str, Any]],
    selected_run_ids: set[str],
    source_paths: set[str],
) -> None:
    manifest = load_json(manifest_path)
    run_id = manifest.get("run_id")
    if not isinstance(run_id, str):
        global_missing.add(f"run manifest missing run_id: {relative_to_repo(manifest_path, repo_root)}")
        return
    if args.run_ids and run_id not in set(args.run_ids):
        return
    if manifest.get("model_id") != args.model_id:
        return

    artifact_paths = manifest.get("artifact_paths") or {}
    assistant_probe_doc = None
    assistant_probe_path = artifact_paths.get("assistant_probe")
    if isinstance(assistant_probe_path, str):
        source_paths.add(assistant_probe_path)
        assistant_probe_doc, _, error_message = load_relative_json(repo_root, assistant_probe_path)
        if error_message:
            global_missing.add(f"run {run_id}: {error_message}")
        elif assistant_probe_doc is not None:
            assistant_observations.append(assistant_probe_doc)
    elif manifest.get("assistant_model_id") is not None:
        global_missing.add(f"run {run_id}: assistant probe artifact missing")

    effective_assistant_model_id = None
    if assistant_probe_doc and assistant_probe_doc.get("probe_status") == "supported":
        effective_assistant_model_id = manifest.get("assistant_model_id")

    if args.assistant_model_id and effective_assistant_model_id != args.assistant_model_id:
        return

    selected_run_ids.add(run_id)
    source_paths.add(relative_to_repo(manifest_path, repo_root))

    settings_paths = parse_profile_artifacts(artifact_paths.get("settings_requests") or [])
    benchmark_paths = parse_profile_artifacts(artifact_paths.get("benchmark_results") or [])
    profile_ids = dedupe_sorted(list(manifest.get("profile_ids") or []) + list(settings_paths.keys()) + list(benchmark_paths.keys()))
    if not profile_ids:
        global_missing.add(f"run {run_id}: no profile evidence was emitted")
        return

    for profile_id in profile_ids:
        profile_doc = profiles_by_id.get(profile_id) or {}
        workload = profile_doc.get("workload")
        settings_doc = None
        settings_path = settings_paths.get(profile_id)
        if settings_path:
            source_paths.add(settings_path)
            settings_doc, _, error_message = load_relative_json(repo_root, settings_path)
            if error_message:
                global_missing.add(f"run {run_id}: {error_message}")
        if settings_doc is None:
            settings_doc = profile_doc.get("settings")

        mtp_enabled = None
        if isinstance(settings_doc, dict):
            mtp_enabled = bool(settings_doc.get("mtp_enabled") or settings_doc.get("vlm_mtp_enabled"))

        settings_hash = hash_settings(settings_doc if isinstance(settings_doc, dict) else None)
        candidate = get_or_create_candidate(
            candidate_index=candidate_index,
            model_id=args.model_id,
            assistant_model_id=effective_assistant_model_id,
            workload=workload,
            profile_id=profile_id,
            mtp_enabled=mtp_enabled,
            settings=settings_doc if isinstance(settings_doc, dict) else None,
            settings_hash=settings_hash,
        )
        candidate["source_run_ids"].add(run_id)
        candidate["source_paths"].add(relative_to_repo(manifest_path, repo_root))
        if settings_path:
            candidate["source_paths"].add(settings_path)
        if assistant_probe_doc:
            candidate["assistant_probe_observations"].append(assistant_probe_doc)
            if isinstance(assistant_probe_path, str):
                candidate["source_paths"].add(assistant_probe_path)

        benchmark_path = benchmark_paths.get(profile_id)
        if not benchmark_path:
            candidate["missing_evidence"].add("Benchmark result artifact missing.")
            continue

        candidate["source_paths"].add(benchmark_path)
        source_paths.add(benchmark_path)
        benchmark_doc, _, error_message = load_relative_json(repo_root, benchmark_path)
        if error_message:
            candidate["missing_evidence"].add(error_message)
            global_missing.add(f"run {run_id}: {error_message}")
            continue
        assert benchmark_doc is not None
        benchmark_status = benchmark_doc.get("status")
        if isinstance(benchmark_status, str):
            candidate["benchmark_statuses"].add(benchmark_status)
        rows = [normalize_benchmark_row(row) for row in benchmark_doc.get("results") or [] if isinstance(row, dict)]
        if not rows:
            candidate["missing_evidence"].add("Benchmark results file contains no comparable rows.")
            continue
        candidate["benchmark_rows"].extend(rows)


def collect_evaluation_evidence(
    repo_root: pathlib.Path,
    manifest_path: pathlib.Path,
    args: argparse.Namespace,
    profiles_by_id: dict[str, dict[str, Any]],
    candidate_index: dict[tuple[Any, ...], dict[str, Any]],
    global_missing: set[str],
    selected_evaluation_run_ids: set[str],
    source_paths: set[str],
) -> None:
    manifest = load_json(manifest_path)
    evaluation_run_id = manifest.get("evaluation_run_id")
    if not isinstance(evaluation_run_id, str):
        global_missing.add(f"evaluation manifest missing evaluation_run_id: {relative_to_repo(manifest_path, repo_root)}")
        return
    if args.evaluation_run_ids and evaluation_run_id not in set(args.evaluation_run_ids):
        return
    if manifest.get("model_id") != args.model_id:
        return
    if args.assistant_model_id and manifest.get("assistant_model_id") != args.assistant_model_id:
        return

    selected_evaluation_run_ids.add(evaluation_run_id)
    source_paths.add(relative_to_repo(manifest_path, repo_root))

    profile_id = manifest.get("profile_id")
    profile_doc = profiles_by_id.get(profile_id) or {}
    settings_doc = profile_doc.get("settings") if isinstance(profile_doc, dict) else None
    settings_hash = hash_settings(settings_doc if isinstance(settings_doc, dict) else None)
    candidate = get_or_create_candidate(
        candidate_index=candidate_index,
        model_id=args.model_id,
        assistant_model_id=manifest.get("assistant_model_id"),
        workload=manifest.get("workload") or profile_doc.get("workload"),
        profile_id=profile_id,
        mtp_enabled=manifest.get("mtp_enabled"),
        settings=settings_doc if isinstance(settings_doc, dict) else None,
        settings_hash=settings_hash,
    )
    candidate["source_evaluation_run_ids"].add(evaluation_run_id)
    candidate["source_paths"].add(relative_to_repo(manifest_path, repo_root))

    case_result_paths = manifest.get("case_result_paths") or []
    if not case_result_paths:
        candidate["missing_evidence"].add("Evaluation manifest contains no case_result_paths.")
        global_missing.add(f"evaluation {evaluation_run_id}: no case_result_paths emitted")
        return

    for case_result_path in case_result_paths:
        candidate["source_paths"].add(case_result_path)
        source_paths.add(case_result_path)
        case_result_doc, _, error_message = load_relative_json(repo_root, case_result_path)
        if error_message:
            candidate["missing_evidence"].add(error_message)
            global_missing.add(f"evaluation {evaluation_run_id}: {error_message}")
            continue
        assert case_result_doc is not None
        scoring_path = case_result_doc.get("scoring_path")
        if not isinstance(scoring_path, str):
            candidate["missing_evidence"].add("Case result missing scoring_path.")
            continue
        candidate["source_paths"].add(scoring_path)
        source_paths.add(scoring_path)
        scoring_doc, _, error_message = load_relative_json(repo_root, scoring_path)
        if error_message:
            candidate["missing_evidence"].add(error_message)
            global_missing.add(f"evaluation {evaluation_run_id}: {error_message}")
            continue
        assert scoring_doc is not None
        candidate["evaluation_cases"].append(extract_evaluation_case_summary(scoring_doc, scoring_path))


def build_comparison_groups(candidates: list[dict[str, Any]]) -> list[dict[str, Any]]:
    grouped: dict[str, list[dict[str, Any]]] = {}
    for candidate in candidates:
        workload = candidate.get("workload") or "unknown"
        grouped.setdefault(workload, []).append(candidate)

    comparison_groups: list[dict[str, Any]] = []
    for workload, group_candidates in sorted(grouped.items()):
        primary, secondary = choose_evidence_order(workload, group_candidates)
        rankable_candidates = [
            candidate
            for candidate in group_candidates
            if candidate["benchmark"].get("available") or candidate["evaluation"].get("available")
        ]
        ranked_input = rankable_candidates or group_candidates
        ranked_candidates = sorted(
            ranked_input,
            key=lambda candidate: candidate_sort_key(candidate, workload, primary, secondary),
        )
        comparison_groups.append(
            {
                "workload": workload,
                "primary_evidence_type": primary,
                "secondary_evidence_type": secondary,
                "candidate_ids": [candidate["candidate_id"] for candidate in ranked_candidates],
                "source_paths": dedupe_sorted(
                    source_path for candidate in ranked_candidates for source_path in candidate["source_paths"]
                ),
                "missing_evidence": dedupe_sorted(
                    missing for candidate in ranked_candidates for missing in candidate["missing_evidence"]
                ),
            }
        )
    return comparison_groups


def build_recommendation_manifest(
    recommendation_id: str,
    created_at: str,
    model_id: str,
    assistant_model_id: str | None,
    candidates: list[dict[str, Any]],
    comparison_groups: list[dict[str, Any]],
    source_run_ids: list[str],
    source_evaluation_run_ids: list[str],
    source_paths: list[str],
    missing_evidence: list[str],
) -> dict[str, Any]:
    candidate_lookup = {candidate["candidate_id"]: candidate for candidate in candidates}
    recommendations: list[dict[str, Any]] = []
    for group in comparison_groups:
        workload = group["workload"]
        primary = group["primary_evidence_type"]
        secondary = group["secondary_evidence_type"]
        ranked_candidates = [candidate_lookup[candidate_id] for candidate_id in group["candidate_ids"]]
        for index, candidate in enumerate(ranked_candidates, start=1):
            confidence = infer_confidence(candidate, workload, ranked_candidates, primary, secondary)
            tradeoffs, caveats = build_candidate_caveats(candidate, ranked_candidates, primary, secondary)
            recommendations.append(
                {
                    "workload": workload,
                    "rank": index,
                    "profile_id": candidate.get("profile_id"),
                    "mtp_recommended": bool(candidate.get("mtp_enabled")),
                    "assistant_recommended": bool(candidate.get("assistant_model_id")) and candidate.get("assistant_summary", {}).get("supported_count", 0) > 0,
                    "assistant_model_id": candidate.get("assistant_model_id"),
                    "confidence": confidence,
                    "speed_summary": build_speed_summary(candidate, workload),
                    "quality_summary": build_quality_summary(candidate),
                    "tradeoffs": tradeoffs,
                    "caveats": caveats,
                    "source_paths": candidate["source_paths"],
                }
            )

    topology = build_instance_topology(recommendations)

    return {
        "schema_version": SCHEMA_VERSION,
        "recommendation_id": recommendation_id,
        "created_at": created_at,
        "run_id": None,
        "evaluation_run_id": None,
        "normalization_id": None,
        "model_id": model_id,
        "assistant_model_id": assistant_model_id,
        "source_run_ids": source_run_ids,
        "source_evaluation_run_ids": source_evaluation_run_ids,
        "source_paths": source_paths,
        "recommendations": recommendations,
        "instance_topology": topology,
        "missing_evidence": missing_evidence,
    }


def build_normalized_manifest(
    normalization_id: str,
    recommendation_id: str,
    created_at: str,
    model_id: str,
    assistant_model_id: str | None,
    candidates: list[dict[str, Any]],
    comparison_groups: list[dict[str, Any]],
    source_run_ids: list[str],
    source_evaluation_run_ids: list[str],
    source_paths: list[str],
    missing_evidence: list[str],
) -> dict[str, Any]:
    return {
        "schema_version": SCHEMA_VERSION,
        "normalization_id": normalization_id,
        "recommendation_id": recommendation_id,
        "created_at": created_at,
        "run_id": None,
        "evaluation_run_id": None,
        "model_id": model_id,
        "assistant_model_id": assistant_model_id,
        "source_run_ids": source_run_ids,
        "source_evaluation_run_ids": source_evaluation_run_ids,
        "source_paths": source_paths,
        "candidates": candidates,
        "comparison_groups": comparison_groups,
        "missing_evidence": missing_evidence,
    }


def build_summary_markdown(
    recommendation_manifest: dict[str, Any],
    normalized_manifest: dict[str, Any],
    comparison_groups: list[dict[str, Any]],
) -> str:
    lines = [
        "# Generated Recommendation Summary",
        "",
        f"Date: {recommendation_manifest['created_at']}",
        "",
        f"Model: `{recommendation_manifest['model_id']}`",
        "",
        f"Assistant model filter: `{recommendation_manifest.get('assistant_model_id') or 'none'}`",
        "",
        "## Source Note",
        "",
        "This report was generated deterministically from stored benchmark and prompt-quality artifacts. ",
        "It preserves missing evidence explicitly and does not infer unsupported thresholds or client settings.",
        "",
        "## Ranked Recommendations",
        "",
        "| Workload | Rank | Profile | MTP | Assistant | Confidence |",
        "| --- | ---: | --- | --- | --- | --- |",
    ]

    for recommendation in recommendation_manifest["recommendations"]:
        assistant_label = recommendation.get("assistant_model_id") or "none"
        mtp_label = "on" if recommendation.get("mtp_recommended") else "off"
        lines.append(
            f"| `{recommendation['workload']}` | {recommendation['rank']} | `{recommendation['profile_id']}` | `{mtp_label}` | `{assistant_label}` | `{recommendation['confidence']}` |"
        )

    candidate_lookup = {candidate["candidate_id"]: candidate for candidate in normalized_manifest["candidates"]}
    for group in comparison_groups:
        workload = group["workload"]
        lines.extend(["", f"### `{workload}`", ""])
        for rank, candidate_id in enumerate(group["candidate_ids"], start=1):
            candidate = candidate_lookup[candidate_id]
            recommendation = next(
                item
                for item in recommendation_manifest["recommendations"]
                if item["workload"] == workload and item["rank"] == rank and item["profile_id"] == candidate.get("profile_id")
            )
            lines.append(
                f"Rank {rank}: `{candidate.get('profile_id')}` with MTP `{'on' if candidate.get('mtp_enabled') else 'off'}` "
                f"and assistant `{candidate.get('assistant_model_id') or 'none'}`."
            )
            lines.append("")
            lines.append(f"- Speed: {recommendation['speed_summary']}")
            lines.append(f"- Quality: {recommendation['quality_summary']}")
            if recommendation["tradeoffs"]:
                lines.append(f"- Tradeoffs: {' '.join(recommendation['tradeoffs'])}")
            if recommendation["caveats"]:
                lines.append(f"- Caveats: {' '.join(recommendation['caveats'])}")

    assistant_observations: list[dict[str, Any]] = []
    for candidate in normalized_manifest["candidates"]:
        summary = candidate.get("assistant_summary") or {}
        if summary.get("observation_count"):
            assistant_observations.append(
                {
                    "profile_id": candidate.get("profile_id"),
                    "assistant_model_id": candidate.get("assistant_model_id"),
                    "probe_statuses": summary.get("probe_statuses") or [],
                    "failure_reasons": summary.get("failure_reasons") or [],
                }
            )

    if assistant_observations:
        lines.extend(["", "## Assistant Outcome Summary", ""])
        for observation in assistant_observations:
            lines.append(
                f"- `{observation['profile_id']}` assistant `{observation['assistant_model_id'] or 'none'}`: "
                f"statuses {', '.join(observation['probe_statuses']) or 'none'}"
            )
            if observation["failure_reasons"]:
                lines.append(f"  Failure reasons: {'; '.join(observation['failure_reasons'])}")

    if recommendation_manifest["missing_evidence"]:
        lines.extend(["", "## Missing Evidence", ""])
        for item in recommendation_manifest["missing_evidence"]:
            lines.append(f"- {item}")

    lines.extend(["", "## Traceability", ""])
    lines.append(f"- Normalization ID: `{normalized_manifest['normalization_id']}`")
    lines.append(f"- Recommendation ID: `{recommendation_manifest['recommendation_id']}`")
    lines.append(f"- Source run IDs: {', '.join(recommendation_manifest['source_run_ids']) or 'none'}")
    lines.append(
        f"- Source evaluation run IDs: {', '.join(recommendation_manifest['source_evaluation_run_ids']) or 'none'}"
    )
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    repo_root = pathlib.Path(__file__).resolve().parents[2]
    profiles_doc = load_json(repo_root / args.profiles_json)
    profiles_by_id = {
        profile.get("id"): profile
        for profile in profiles_doc.get("profiles", [])
        if isinstance(profile, dict) and profile.get("id")
    }

    candidate_index: dict[tuple[Any, ...], dict[str, Any]] = {}
    global_missing: set[str] = set()
    assistant_observations: list[dict[str, Any]] = []
    selected_run_ids: set[str] = set()
    selected_evaluation_run_ids: set[str] = set()
    source_paths: set[str] = set()

    runs_dir = repo_root / args.runs_dir
    evaluations_dir = repo_root / args.evaluations_dir

    for manifest_path in discover_manifest_paths(runs_dir, "run_manifest.json"):
        collect_run_evidence(
            repo_root=repo_root,
            manifest_path=manifest_path,
            args=args,
            profiles_by_id=profiles_by_id,
            candidate_index=candidate_index,
            global_missing=global_missing,
            assistant_observations=assistant_observations,
            selected_run_ids=selected_run_ids,
            source_paths=source_paths,
        )

    for manifest_path in discover_manifest_paths(evaluations_dir, "evaluation_manifest.json"):
        collect_evaluation_evidence(
            repo_root=repo_root,
            manifest_path=manifest_path,
            args=args,
            profiles_by_id=profiles_by_id,
            candidate_index=candidate_index,
            global_missing=global_missing,
            selected_evaluation_run_ids=selected_evaluation_run_ids,
            source_paths=source_paths,
        )

    created = now_iso(dt.datetime.now(dt.timezone.utc))
    candidates = [finalize_candidate(candidate, created) for candidate in candidate_index.values()]
    candidates.sort(key=lambda candidate: (candidate.get("workload") or "", candidate.get("profile_id") or "", candidate.get("assistant_model_id") or ""))

    workloads_in_profiles = sorted({profile.get("workload") for profile in profiles_by_id.values() if profile.get("workload")})
    workloads_with_candidates = {candidate.get("workload") for candidate in candidates if candidate.get("workload")}
    for workload in workloads_in_profiles:
        if workload not in workloads_with_candidates:
            global_missing.add(f"No normalized evidence available for workload `{workload}`.")

    if not candidates:
        raise SystemExit(f"No run or evaluation evidence matched model_id={args.model_id!r}")

    validate_report_evidence(candidates)

    now = dt.datetime.now(dt.timezone.utc)
    normalization_id = build_artifact_id(args.model_id, "normalized", now)
    recommendation_id = build_artifact_id(args.model_id, "recommendation", now)
    comparison_groups = build_comparison_groups(candidates)
    missing_evidence = dedupe_sorted(global_missing)
    source_run_ids = sorted(selected_run_ids)
    source_evaluation_run_ids = sorted(selected_evaluation_run_ids)
    source_paths_sorted = sorted(source_paths)

    normalized_manifest = build_normalized_manifest(
        normalization_id=normalization_id,
        recommendation_id=recommendation_id,
        created_at=created,
        model_id=args.model_id,
        assistant_model_id=args.assistant_model_id,
        candidates=candidates,
        comparison_groups=comparison_groups,
        source_run_ids=source_run_ids,
        source_evaluation_run_ids=source_evaluation_run_ids,
        source_paths=source_paths_sorted,
        missing_evidence=missing_evidence,
    )
    recommendation_manifest = build_recommendation_manifest(
        recommendation_id=recommendation_id,
        created_at=created,
        model_id=args.model_id,
        assistant_model_id=args.assistant_model_id,
        candidates=candidates,
        comparison_groups=comparison_groups,
        source_run_ids=source_run_ids,
        source_evaluation_run_ids=source_evaluation_run_ids,
        source_paths=source_paths_sorted,
        missing_evidence=missing_evidence,
    )
    recommendation_manifest["normalization_id"] = normalization_id
    summary_markdown = build_summary_markdown(recommendation_manifest, normalized_manifest, comparison_groups)

    normalized_dir = repo_root / args.normalized_dir / normalization_id
    recommendation_dir = repo_root / args.recommendations_dir / recommendation_id
    summary_path = repo_root / args.summaries_dir / f"{recommendation_id}.md"

    save_json(normalized_dir / "normalized_manifest.json", normalized_manifest)
    save_json(recommendation_dir / "recommendation_manifest.json", recommendation_manifest)
    save_text(summary_path, summary_markdown)

    output = {
        "schema_version": SCHEMA_VERSION,
        "normalization_id": normalization_id,
        "recommendation_id": recommendation_id,
        "created_at": created,
        "model_id": args.model_id,
        "assistant_model_id": args.assistant_model_id,
        "normalized_manifest_path": relative_to_repo(normalized_dir / "normalized_manifest.json", repo_root),
        "recommendation_manifest_path": relative_to_repo(recommendation_dir / "recommendation_manifest.json", repo_root),
        "summary_path": relative_to_repo(summary_path, repo_root),
        "source_run_ids": source_run_ids,
        "source_evaluation_run_ids": source_evaluation_run_ids,
        "missing_evidence": missing_evidence,
    }
    print(json.dumps(output, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())