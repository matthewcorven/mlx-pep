#!/usr/bin/env python3
import argparse
import os
import pathlib
import sys
from typing import Any, Dict, List, Optional

SCRIPT_DIR = pathlib.Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from runner_lib import (  # noqa: E402
    OMLXHarness,
    benchmark_payload_for_profile,
    build_instance_topology,
    build_profile_settings,
    build_run_id,
    detect_assistant_setting_field,
    ensure_vlm_mtp_assistant_configuration,
    list_profile_field_names,
    load_json,
    match_topology_instance,
    merge_settings,
    pick_model,
    record_probe_not_attempted,
    relative_to_repo,
    resolve_profile,
    resolve_smoke_profiles,
    save_json,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run deterministic oMLX assessment suites and assistant probes")
    parser.add_argument("--model-id", required=True)
    parser.add_argument("--assistant-model-id", default=None)
    parser.add_argument("--mtp", choices=("on", "off", "profile"), default="profile")
    parser.add_argument("--profile-id", action="append", dest="profile_ids")
    parser.add_argument("--suite", choices=("smoke", "full", "single"), default="single")
    parser.add_argument("--base-url", default=os.environ.get("OMLX_BASE_URL", "http://127.0.0.1:8000"))
    parser.add_argument("--api-key", default=None)
    parser.add_argument("--results-dir", default="results/runs")
    parser.add_argument("--profiles-json", default="config/benchmark_profiles.json")
    parser.add_argument("--smoke-json", default="config/smoke_suite.json")
    parser.add_argument("--stream-timeout", type=int, default=3600)
    parser.add_argument("--remember-login", action="store_true")
    parser.add_argument("--probe-only", action="store_true")
    parser.add_argument("--topology-manifest", default=None)
    return parser.parse_args()


def select_profile_ids(args: argparse.Namespace, repo_root: pathlib.Path) -> List[str]:
    if args.suite == "smoke":
        smoke_doc = load_json(repo_root / args.smoke_json)
        return resolve_smoke_profiles(smoke_doc)
    if args.suite == "full":
        profiles_doc = load_json(repo_root / args.profiles_json)
        return [profile["id"] for profile in profiles_doc.get("profiles", [])]
    if args.profile_ids:
        return args.profile_ids
    raise SystemExit("--profile-id is required when --suite single")


def build_candidate_sources(assistant_model_id: Optional[str]) -> List[dict]:
    if not assistant_model_id:
        return []
    return [{"source": "operator_supplied", "candidate_id": assistant_model_id, "notes": None}]


def write_run_manifest(
    path: pathlib.Path,
    payload: Dict,
):
    save_json(path, payload)


def extract_instance_topology(document: Dict[str, Any]) -> Dict[str, Any]:
    topology = document.get("instance_topology")
    if isinstance(topology, dict):
        return topology
    if isinstance(document.get("instances"), list) and document.get("instance_mode"):
        return document
    raise ValueError("topology manifest does not contain an instance_topology block")


def build_profile_execution_plan(
    profiles: List[Dict[str, Any]],
    mtp_mode: str,
    base_url_seed: str,
    requested_assistant_model_id: Optional[str],
    selected_model: Optional[Dict[str, Any]] = None,
    topology: Optional[Dict[str, Any]] = None,
) -> tuple[Dict[str, Any], List[Dict[str, Any]]]:
    topology_was_supplied = topology is not None

    if topology is None:
        if selected_model is None:
            raise ValueError("selected_model is required when deriving topology from profiles")
        recommendation_entries: List[Dict[str, Any]] = []
        for profile in profiles:
            _, mtp_enabled = build_profile_settings(profile, mtp_mode, selected_model=selected_model)
            recommendation_entries.append(
                {
                    "workload": profile.get("workload"),
                    "rank": 1,
                    "profile_id": profile.get("id"),
                    "mtp_recommended": mtp_enabled,
                    "assistant_model_id": requested_assistant_model_id if (requested_assistant_model_id and mtp_enabled) else None,
                }
            )
        topology = build_instance_topology(recommendation_entries, base_url_seed=base_url_seed)

    execution_plan: List[Dict[str, Any]] = []
    for profile in profiles:
        workload = profile.get("workload")
        profile_id = profile.get("id")
        instance = match_topology_instance(
            topology,
            workload=workload,
            profile_id=profile_id,
            assistant_model_id=requested_assistant_model_id,
        )
        if instance is None:
            instance = match_topology_instance(topology, workload=workload, profile_id=profile_id)
        if instance is None:
            raise ValueError(f"No topology instance matches profile {profile_id} ({workload})")

        effective_mtp_enabled = instance.get("mtp_enabled")
        if effective_mtp_enabled is None and selected_model is not None:
            _, effective_mtp_enabled = build_profile_settings(profile, mtp_mode, selected_model=selected_model)
        effective_mtp_enabled = bool(effective_mtp_enabled)

        instance_assistant_model_id = instance.get("assistant_model_id")
        if (
            topology_was_supplied
            and instance_assistant_model_id
            and requested_assistant_model_id
            and instance_assistant_model_id != requested_assistant_model_id
        ):
            raise ValueError(
                f"Topology assistant mismatch for {profile_id}: {instance_assistant_model_id} != {requested_assistant_model_id}"
            )

        effective_assistant_model_id = instance_assistant_model_id
        if not topology_was_supplied and effective_assistant_model_id is None and effective_mtp_enabled:
            effective_assistant_model_id = requested_assistant_model_id

        current_settings = dict((selected_model or {}).get("settings") or {})
        vlm_mtp_requested = bool((profile.get("settings") or {}).get("vlm_mtp_enabled") or current_settings.get("vlm_mtp_enabled"))
        if effective_mtp_enabled and vlm_mtp_requested and not effective_assistant_model_id:
            raise ValueError(
                f"Profile {profile_id} enables VLM MTP and requires --assistant-model-id or a topology assistant_model_id. "
                "Clean-start runs cannot rely on inherited draft-model state."
            )

        execution_plan.append(
            {
                "profile_id": profile_id,
                "workload": workload,
                "instance_id": instance.get("instance_id") or "instance-1",
                "base_url": instance.get("base_url") or base_url_seed,
                "port": instance.get("port"),
                "mtp_enabled": effective_mtp_enabled,
                "assistant_model_id": effective_assistant_model_id,
                "topology_reason": instance.get("reason"),
            }
        )

    return topology, execution_plan


def capture_admin_context(
    client: OMLXHarness,
    model_id: str,
    api_key: str,
    remember_login: bool,
) -> Dict[str, Any]:
    login = client.login_admin(api_key, remember=remember_login)

    active_benchmark = client.get_active_benchmark()
    cancel_response = None
    if active_benchmark.get("running"):
        cancel_response = client.cancel_active_benchmark_if_running()

    profile_fields = client.get_profile_fields()
    profile_field_names = list_profile_field_names(profile_fields)
    models_doc = client.list_models()
    selected_model = pick_model(models_doc, model_id)

    try:
        generation_config = client.get_generation_config(model_id)
    except Exception as exc:  # noqa: BLE001
        generation_config = {"warning": str(exc)}

    return {
        "client": client,
        "login": login,
        "active_benchmark": active_benchmark,
        "cancel_response": cancel_response,
        "profile_fields": profile_fields,
        "profile_field_names": profile_field_names,
        "models_doc": models_doc,
        "selected_model": selected_model,
        "generation_config": generation_config,
    }


def persist_admin_context(
    context: Dict[str, Any],
    output_dir: pathlib.Path,
    repo_root: pathlib.Path,
) -> Dict[str, str]:
    paths = {
        "login": output_dir / "01_login.json",
        "active_benchmark": output_dir / "00_active_benchmark.json",
        "profile_fields": output_dir / "02_profile_fields.json",
        "model_inventory": output_dir / "03_models.json",
        "selected_model": output_dir / "04_selected_model.json",
        "generation_config": output_dir / "05_generation_config.json",
    }
    save_json(paths["login"], context["login"])
    save_json(paths["active_benchmark"], context["active_benchmark"])
    if context.get("cancel_response") is not None:
        save_json(output_dir / "00_active_benchmark_cancel.json", context["cancel_response"])
    save_json(paths["profile_fields"], context["profile_fields"])
    save_json(paths["model_inventory"], context["models_doc"])
    save_json(paths["selected_model"], context["selected_model"])
    save_json(paths["generation_config"], context["generation_config"])
    artifact_paths = {key: relative_to_repo(path, repo_root) for key, path in paths.items()}
    context["artifact_paths"] = artifact_paths
    return artifact_paths


def resolve_requested_assistant_model_id(
    execution_plan: List[Dict[str, Any]],
    requested_assistant_model_id: Optional[str],
) -> Optional[str]:
    assistant_model_ids = sorted(
        {
            entry.get("assistant_model_id")
            for entry in execution_plan
            if entry.get("assistant_model_id")
        }
    )
    if len(assistant_model_ids) > 1:
        raise ValueError("run_assessment supports one assistant model per invocation")
    if assistant_model_ids:
        effective_assistant_model_id = assistant_model_ids[0]
        if requested_assistant_model_id and requested_assistant_model_id != effective_assistant_model_id:
            raise ValueError(
                f"Requested assistant model {requested_assistant_model_id} does not match topology assistant {effective_assistant_model_id}"
            )
        return effective_assistant_model_id
    return requested_assistant_model_id


def summarize_benchmark_guard_findings(profile_id: str, bench_results: Dict) -> tuple[List[str], List[str]]:
    errors: List[str] = []
    warnings: List[str] = []
    result_status = bench_results.get("status")
    if result_status != "completed":
        errors.append(f"{profile_id}: benchmark status {result_status or 'unknown'}")

    result_rows = bench_results.get("results") or []
    if not result_rows:
        errors.append(f"{profile_id}: benchmark returned no comparable result rows")

    upload_state = bench_results.get("upload_state") or {}
    skipped_features = upload_state.get("skipped_features") or []
    if skipped_features:
        skipped_reason = upload_state.get("skipped_reason") or "unspecified"
        warnings.append(
            f"{profile_id}: upload skipped features {', '.join(str(item) for item in skipped_features)} ({skipped_reason})"
        )

    return errors, warnings


def main() -> int:
    args = parse_args()
    repo_root = pathlib.Path(__file__).resolve().parents[2]
    api_key = args.api_key or os.environ.get("OMLX_API_KEY")
    if not api_key:
        raise SystemExit("OMLX_API_KEY is required via --api-key or environment")

    profile_ids = select_profile_ids(args, repo_root)
    profiles_doc = load_json(repo_root / args.profiles_json)
    profiles = [resolve_profile(profiles_doc, profile_id) for profile_id in profile_ids]
    run_id = build_run_id(args.model_id, args.suite if args.suite != "single" else profile_ids[0])
    run_dir = repo_root / args.results_dir / run_id
    run_dir.mkdir(parents=True, exist_ok=True)

    manifest_errors: List[str] = []
    manifest_warnings: List[str] = []
    benchmark_result_paths: List[str] = []
    settings_request_paths: List[str] = []
    profile_ids_run: List[str] = []
    status = "success"

    topology_source_path = None
    provided_topology = None
    if args.topology_manifest:
        topology_manifest_path = (repo_root / args.topology_manifest).resolve()
        provided_topology = extract_instance_topology(load_json(topology_manifest_path))
        topology_source_path = relative_to_repo(topology_manifest_path, repo_root)

    bootstrap_base_url = args.base_url
    if provided_topology:
        first_instance = (provided_topology.get("instances") or [{}])[0]
        bootstrap_base_url = first_instance.get("base_url") or args.base_url

    bootstrap_client = OMLXHarness(bootstrap_base_url)
    try:
        bootstrap_context = capture_admin_context(
            bootstrap_client,
            model_id=args.model_id,
            api_key=api_key,
            remember_login=args.remember_login,
        )
    except Exception as exc:  # noqa: BLE001
        save_json(run_dir / "zz_bootstrap_error.json", {"error": str(exc), "base_url": bootstrap_base_url})
        raise

    root_artifact_paths = persist_admin_context(bootstrap_context, run_dir, repo_root)
    topology, profile_execution_plan = build_profile_execution_plan(
        profiles=profiles,
        mtp_mode=args.mtp,
        base_url_seed=args.base_url,
        requested_assistant_model_id=args.assistant_model_id,
        selected_model=bootstrap_context["selected_model"],
        topology=provided_topology,
    )

    topology_path = run_dir / "instance_topology.json"
    execution_plan_path = run_dir / "profile_execution_plan.json"
    save_json(topology_path, topology)
    save_json(execution_plan_path, profile_execution_plan)

    instance_contexts: Dict[str, Dict[str, Any]] = {}
    instance_artifacts: Dict[str, Dict[str, str]] = {}
    bootstrap_reused = False
    for plan_entry in profile_execution_plan:
        instance_id = plan_entry["instance_id"]
        if instance_id in instance_contexts:
            continue

        instance_dir = run_dir / "instances" / instance_id
        if not bootstrap_reused and plan_entry["base_url"] == bootstrap_base_url:
            context = bootstrap_context
            bootstrap_reused = True
        else:
            try:
                context = capture_admin_context(
                    OMLXHarness(plan_entry["base_url"]),
                    model_id=args.model_id,
                    api_key=api_key,
                    remember_login=args.remember_login,
                )
            except Exception as exc:  # noqa: BLE001
                status = "partial"
                manifest_errors.append(f"{instance_id}: {exc}")
                save_json(instance_dir / "zz_error.json", {"error": str(exc), "instance_id": instance_id})
                continue

        context["instance_id"] = instance_id
        context["base_url"] = plan_entry["base_url"]
        instance_artifacts[instance_id] = persist_admin_context(context, instance_dir, repo_root)
        instance_contexts[instance_id] = context

    effective_assistant_model_id = resolve_requested_assistant_model_id(profile_execution_plan, args.assistant_model_id)
    candidate_sources = build_candidate_sources(effective_assistant_model_id)
    assistant_probe_path = run_dir / "assistant_probe.json"
    assistant_probe_rel = relative_to_repo(assistant_probe_path, repo_root)

    assistant_probe_context = next(
        (
            instance_contexts[entry["instance_id"]]
            for entry in profile_execution_plan
            if entry.get("assistant_model_id") and entry["instance_id"] in instance_contexts
        ),
        bootstrap_context,
    )

    if effective_assistant_model_id:
        try:
            assistant_model = pick_model(assistant_probe_context["models_doc"], effective_assistant_model_id)
        except ValueError:
            assistant_probe = record_probe_not_attempted(
                run_id=run_id,
                model_id=args.model_id,
                assistant_model_id=effective_assistant_model_id,
                candidate_sources=candidate_sources,
                fallback_action="target_model_only",
                evidence_paths=[assistant_probe_context["artifact_paths"]["model_inventory"]],
                failure_reason="assistant model not found in oMLX inventory",
            )
        else:
            save_json(run_dir / "04_assistant_model.json", assistant_model)
            assistant_setting_field = detect_assistant_setting_field(
                assistant_probe_context["selected_model"].get("settings") or {},
                assistant_probe_context["profile_field_names"],
            )
            if not assistant_setting_field:
                assistant_probe = {
                    "schema_version": "1.0",
                    "run_id": run_id,
                    "model_id": args.model_id,
                    "assistant_model_id": effective_assistant_model_id,
                    "candidate_sources": candidate_sources,
                    "omlx_inventory_check": "found",
                    "probe_attempted": False,
                    "probe_status": "unsupported",
                    "failure_reason": "no assistant-model setting field advertised by oMLX for the target model",
                    "fallback_action": "target_model_only",
                    "evidence_paths": [
                        assistant_probe_context["artifact_paths"]["model_inventory"],
                        assistant_probe_context["artifact_paths"]["selected_model"],
                        assistant_probe_context["artifact_paths"]["profile_fields"],
                    ],
                }
            else:
                probe_settings = merge_settings(
                    assistant_probe_context["selected_model"].get("settings") or {},
                    {assistant_setting_field: effective_assistant_model_id},
                )
                probe_request_path = run_dir / "06_assistant_probe_settings_request.json"
                save_json(probe_request_path, probe_settings)
                try:
                    probe_response = assistant_probe_context["client"].update_model_settings(args.model_id, probe_settings)
                except Exception as exc:  # noqa: BLE001
                    probe_error_path = run_dir / "07_assistant_probe_settings_error.json"
                    save_json(probe_error_path, {"error": str(exc), "field": assistant_setting_field})
                    assistant_probe = {
                        "schema_version": "1.0",
                        "run_id": run_id,
                        "model_id": args.model_id,
                        "assistant_model_id": effective_assistant_model_id,
                        "candidate_sources": candidate_sources,
                        "omlx_inventory_check": "found",
                        "probe_attempted": True,
                        "probe_status": "unsupported",
                        "failure_reason": str(exc),
                        "fallback_action": "target_model_only",
                        "evidence_paths": [
                            relative_to_repo(run_dir / "03_models.json", repo_root),
                            relative_to_repo(probe_request_path, repo_root),
                            relative_to_repo(probe_error_path, repo_root),
                        ],
                    }
                else:
                    probe_response_path = run_dir / "07_assistant_probe_settings_response.json"
                    save_json(probe_response_path, probe_response)
                    assistant_probe = {
                        "schema_version": "1.0",
                        "run_id": run_id,
                        "model_id": args.model_id,
                        "assistant_model_id": effective_assistant_model_id,
                        "candidate_sources": candidate_sources,
                        "omlx_inventory_check": "found",
                        "probe_attempted": True,
                        "probe_status": "supported",
                        "failure_reason": None,
                        "fallback_action": "none",
                        "evidence_paths": [
                            relative_to_repo(run_dir / "03_models.json", repo_root),
                            relative_to_repo(probe_request_path, repo_root),
                            relative_to_repo(probe_response_path, repo_root),
                        ],
                    }
        save_json(assistant_probe_path, assistant_probe)
    else:
        assistant_probe = record_probe_not_attempted(
            run_id=run_id,
            model_id=args.model_id,
            assistant_model_id=None,
            candidate_sources=[],
            fallback_action="none",
            evidence_paths=[],
            failure_reason=None,
        )
        save_json(assistant_probe_path, assistant_probe)

    if args.probe_only:
        manifest = {
            "schema_version": "1.0",
            "run_id": run_id,
            "created_at": dt_now_iso(),
            "model_id": args.model_id,
            "assistant_model_id": effective_assistant_model_id,
            "suite": args.suite,
            "profile_ids": profile_ids,
            "mtp_mode": args.mtp,
            "base_url": profile_execution_plan[0]["base_url"] if profile_execution_plan else args.base_url,
            "instance_topology": topology,
            "profile_execution_plan": profile_execution_plan,
            "topology_source_path": topology_source_path,
            "artifact_paths": {
                "model_inventory": root_artifact_paths["model_inventory"],
                "profile_fields": root_artifact_paths["profile_fields"],
                "settings_requests": [],
                "benchmark_results": [],
                "assistant_probe": assistant_probe_rel,
                "instance_topology": relative_to_repo(topology_path, repo_root),
                "profile_execution_plan": relative_to_repo(execution_plan_path, repo_root),
                "instance_artifacts": instance_artifacts,
            },
            "status": "success" if assistant_probe.get("probe_status") != "failed" else "partial",
            "errors": [],
        }
        write_run_manifest(run_dir / "run_manifest.json", manifest)
        print(json_dump(manifest))
        return 0

    plan_by_profile_id = {entry["profile_id"]: entry for entry in profile_execution_plan}

    for profile in profiles:
        profile_id = profile["id"]
        plan_entry = plan_by_profile_id[profile_id]
        profile_ids_run.append(profile_id)
        context = instance_contexts.get(plan_entry["instance_id"])
        if context is None:
            status = "partial"
            manifest_errors.append(f"{profile_id}: missing initialized context for {plan_entry['instance_id']}")
            profile_dir = run_dir / profile_id
            profile_dir.mkdir(parents=True, exist_ok=True)
            save_json(profile_dir / "zz_error.json", {"error": "missing instance context", "profile_id": profile_id})
            continue

        effective_mtp_mode = "on" if plan_entry["mtp_enabled"] else "off"
        settings_overrides, mtp_enabled = build_profile_settings(
            profile,
            effective_mtp_mode,
            selected_model=context["selected_model"],
        )

        effective_profile_assistant_model_id = (
            plan_entry.get("assistant_model_id") if assistant_probe.get("probe_status") == "supported" else None
        )
        assistant_setting_field = ensure_vlm_mtp_assistant_configuration(
            profile_id=profile_id,
            settings_overrides=settings_overrides,
            current_settings=context["selected_model"].get("settings") or {},
            profile_field_names=context["profile_field_names"],
            assistant_model_id=effective_profile_assistant_model_id,
        )
        if assistant_setting_field:
            settings_overrides[assistant_setting_field] = effective_profile_assistant_model_id

        benchmark_payload = benchmark_payload_for_profile(profile)
        current_settings = context["selected_model"].get("settings") or {}
        merged_settings = merge_settings(current_settings, settings_overrides)

        profile_dir = run_dir / profile_id
        profile_dir.mkdir(parents=True, exist_ok=True)
        save_json(profile_dir / "00_execution_plan.json", plan_entry)

        settings_request_path = profile_dir / "01_settings_request.json"
        save_json(settings_request_path, merged_settings)
        settings_request_paths.append(relative_to_repo(settings_request_path, repo_root))

        try:
            settings_response = context["client"].update_model_settings(args.model_id, merged_settings)
            save_json(profile_dir / "02_settings_response.json", settings_response)
            context["selected_model"]["settings"] = settings_response.get("settings") or merged_settings

            bench_request = {"model_id": args.model_id, **benchmark_payload}
            save_json(profile_dir / "03_bench_request.json", bench_request)
            bench_start = context["client"].start_benchmark(
                args.model_id,
                benchmark_payload["prompt_lengths"],
                benchmark_payload["generation_length"],
                benchmark_payload["batch_sizes"],
            )
            save_json(profile_dir / "04_bench_start.json", bench_start)

            bench_id = bench_start["bench_id"]
            events = []
            for event in context["client"].stream_benchmark(bench_id, timeout=args.stream_timeout):
                events.append(event)
                print(json_dump(event, compact=True), flush=True)
            save_json(profile_dir / "05_bench_sse.json", events)

            bench_results = context["client"].get_benchmark_results(bench_id)
            bench_results_path = profile_dir / "06_bench_results.json"
            save_json(bench_results_path, bench_results)
            benchmark_result_paths.append(relative_to_repo(bench_results_path, repo_root))

            guard_errors, guard_warnings = summarize_benchmark_guard_findings(profile_id, bench_results)
            if guard_errors:
                status = "partial"
                manifest_errors.extend(guard_errors)
            if guard_warnings:
                manifest_warnings.extend(guard_warnings)

            profile_manifest = {
                "schema_version": "1.0",
                "run_id": run_id,
                "created_at": dt_now_iso(),
                "model_id": args.model_id,
                "assistant_model_id": plan_entry.get("assistant_model_id") if assistant_probe.get("probe_status") == "supported" else None,
                "profile_id": profile_id,
                "workload": profile.get("workload"),
                "mtp_enabled": mtp_enabled,
                "instance_id": plan_entry["instance_id"],
                "base_url": plan_entry["base_url"],
                "source_paths": [
                    relative_to_repo(profile_dir / "00_execution_plan.json", repo_root),
                    relative_to_repo(settings_request_path, repo_root),
                    relative_to_repo(bench_results_path, repo_root),
                ],
                "status": bench_results.get("status", "success"),
                "guard_findings": guard_errors,
                "warning_findings": guard_warnings,
            }
            save_json(profile_dir / "profile_manifest.json", profile_manifest)
        except Exception as exc:  # noqa: BLE001
            status = "partial"
            manifest_errors.append(f"{profile_id}: {exc}")
            save_json(profile_dir / "zz_error.json", {"error": str(exc), "profile_id": profile_id})

    if status == "success" and manifest_errors:
        status = "partial"

    manifest = {
        "schema_version": "1.0",
        "run_id": run_id,
        "created_at": dt_now_iso(),
        "model_id": args.model_id,
        "assistant_model_id": effective_assistant_model_id,
        "suite": args.suite,
        "profile_ids": profile_ids_run,
        "mtp_mode": args.mtp,
        "base_url": profile_execution_plan[0]["base_url"] if profile_execution_plan else args.base_url,
        "instance_topology": topology,
        "profile_execution_plan": profile_execution_plan,
        "topology_source_path": topology_source_path,
        "artifact_paths": {
            "model_inventory": root_artifact_paths["model_inventory"],
            "profile_fields": root_artifact_paths["profile_fields"],
            "settings_requests": settings_request_paths,
            "benchmark_results": benchmark_result_paths,
            "assistant_probe": assistant_probe_rel,
            "instance_topology": relative_to_repo(topology_path, repo_root),
            "profile_execution_plan": relative_to_repo(execution_plan_path, repo_root),
            "instance_artifacts": instance_artifacts,
        },
        "status": status,
        "errors": manifest_errors,
        "warnings": manifest_warnings,
    }
    write_run_manifest(run_dir / "run_manifest.json", manifest)
    print(json_dump(manifest))
    return 0 if status in {"success", "partial"} else 1


def dt_now_iso() -> str:
    return __import__("datetime").datetime.now(__import__("datetime").timezone.utc).isoformat()


def json_dump(value: Dict, compact: bool = False) -> str:
    if compact:
        return __import__("json").dumps(value, separators=(",", ":"), sort_keys=True)
    return __import__("json").dumps(value, indent=2, sort_keys=True)


if __name__ == "__main__":
    raise SystemExit(main())
