#!/usr/bin/env python3
from __future__ import annotations

import argparse
import glob
import json
import pathlib
import sys
from typing import Any


SCRIPT_DIR = pathlib.Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from run_assessment import summarize_benchmark_guard_findings  # noqa: E402


def load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: pathlib.Path, value: Any) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def audit_run(run_dir: pathlib.Path) -> dict[str, Any]:
    manifest_path = run_dir / "run_manifest.json"
    manifest = load_json(manifest_path)

    guard_errors: list[str] = []
    guard_warnings: list[str] = []
    profile_summaries: list[dict[str, Any]] = []

    for path in sorted(run_dir.glob("*/06_bench_results.json")):
        profile_id = path.parent.name
        bench_results = load_json(path)
        errors, warnings = summarize_benchmark_guard_findings(profile_id, bench_results)
        guard_errors.extend(errors)
        guard_warnings.extend(warnings)
        profile_summaries.append(
            {
                "profile_id": profile_id,
                "status": bench_results.get("status"),
                "row_count": len(bench_results.get("results") or []),
                "guard_findings": errors,
                "warning_findings": warnings,
            }
        )

    existing_errors = manifest.get("errors") or []
    existing_warnings = manifest.get("warnings") or []
    non_profile_errors = [
        error
        for error in existing_errors
        if not any(error.startswith(f"{summary['profile_id']}: ") for summary in profile_summaries)
    ]
    non_profile_warnings = [
        warning
        for warning in existing_warnings
        if not any(warning.startswith(f"{summary['profile_id']}: ") for summary in profile_summaries)
    ]
    merged_errors = non_profile_errors + guard_errors
    merged_warnings = non_profile_warnings + guard_warnings
    manifest["errors"] = merged_errors
    manifest["warnings"] = merged_warnings
    manifest["status"] = "partial" if merged_errors else "success"

    for summary in profile_summaries:
        profile_manifest_path = run_dir / summary["profile_id"] / "profile_manifest.json"
        if profile_manifest_path.is_file():
            profile_manifest = load_json(profile_manifest_path)
            profile_manifest["guard_findings"] = summary["guard_findings"]
            profile_manifest["warning_findings"] = summary["warning_findings"]
            save_json(profile_manifest_path, profile_manifest)

    return {
        "manifest": manifest,
        "manifest_path": manifest_path,
        "profile_summaries": profile_summaries,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit and optionally repair next-phase run manifests")
    parser.add_argument("run_dirs", nargs="+", help="Run directories or glob patterns")
    parser.add_argument("--write", action="store_true", help="Rewrite run_manifest.json and profile manifests in place")
    args = parser.parse_args()

    expanded_paths: list[pathlib.Path] = []
    for item in args.run_dirs:
        matches = sorted(glob.glob(item))
        if matches:
            expanded_paths.extend(pathlib.Path(match).resolve() for match in matches)
        else:
            expanded_paths.append(pathlib.Path(item).resolve())

    reports: list[dict[str, Any]] = []
    exit_code = 0
    for run_dir in expanded_paths:
        report = audit_run(run_dir)
        reports.append(
            {
                "run_dir": str(run_dir),
                "status": report["manifest"].get("status"),
                "errors": report["manifest"].get("errors") or [],
                "warnings": report["manifest"].get("warnings") or [],
                "profile_summaries": report["profile_summaries"],
            }
        )
        if args.write:
            save_json(report["manifest_path"], report["manifest"])
        if report["manifest"].get("errors"):
            exit_code = 1

    print(json.dumps(reports, indent=2, sort_keys=True))
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())