#!/usr/bin/env python3
import argparse
import json
import pathlib
import sys

SCRIPT_DIR = pathlib.Path(__file__).resolve().parent / "next_phase"
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from runner_lib import OMLXHarness, load_json, merge_settings, pick_model, resolve_profile, save_json  # noqa: E402


def main():
    parser = argparse.ArgumentParser(description="oMLX benchmark harness")
    parser.add_argument("--base-url", default="http://127.0.0.1:8000")
    parser.add_argument("--api-key", default=None)
    parser.add_argument("--model-id", required=True)
    parser.add_argument("--profile-id", default=None)
    parser.add_argument("--profiles-json", default="config/benchmark_profiles.json")
    parser.add_argument("--settings", default=None, help="Raw JSON settings override")
    parser.add_argument("--prompt-lengths", nargs="*", type=int)
    parser.add_argument("--generation-length", type=int)
    parser.add_argument("--batch-sizes", nargs="*", type=int)
    parser.add_argument("--stream-timeout", type=int, default=3600)
    parser.add_argument("--results-dir", default="results/runs")
    parser.add_argument("--remember-login", action="store_true")
    args = parser.parse_args()

    api_key = args.api_key or __import__("os").environ.get("OMLX_API_KEY")
    if not api_key:
        raise SystemExit("OMLX_API_KEY is required via --api-key or environment")

    ts = __import__("datetime").datetime.now().strftime("%Y%m%d-%H%M%S")
    run_slug = args.profile_id or "adhoc"
    outdir = pathlib.Path(args.results_dir) / f"{ts}-{run_slug}"
    outdir.mkdir(parents=True, exist_ok=True)

    profile = None
    settings_overrides = {}
    benchmark_overrides = {}

    if args.profile_id:
        profiles_doc = load_json(pathlib.Path(args.profiles_json))
        profile = resolve_profile(profiles_doc, args.profile_id)
        settings_overrides.update(profile.get("settings") or {})
        benchmark_overrides.update(profile.get("benchmark") or {})

    if args.settings:
        settings_overrides.update(json.loads(args.settings))

    if args.prompt_lengths is not None and len(args.prompt_lengths) > 0:
        benchmark_overrides["prompt_lengths"] = args.prompt_lengths
    if args.generation_length is not None:
        benchmark_overrides["generation_length"] = args.generation_length
    if args.batch_sizes is not None:
        benchmark_overrides["batch_sizes"] = args.batch_sizes

    prompt_lengths = benchmark_overrides.get("prompt_lengths", [1024])
    generation_length = benchmark_overrides.get("generation_length", 128)
    batch_sizes = benchmark_overrides.get("batch_sizes", [])

    client = OMLXHarness(args.base_url)
    bench_id = None

    try:
        login = client.login_admin(api_key, remember=args.remember_login)
        save_json(outdir / "01_login.json", login)

        active_benchmark = client.get_active_benchmark()
        save_json(outdir / "00_active_benchmark.json", active_benchmark)
        if active_benchmark.get("running"):
            cancel_response = client.cancel_active_benchmark_if_running()
            save_json(outdir / "00_active_benchmark_cancel.json", cancel_response)

        profile_fields = client.get_profile_fields()
        save_json(outdir / "02_profile_fields.json", profile_fields)

        models_doc = client.list_models()
        save_json(outdir / "03_models.json", models_doc)

        selected_model = pick_model(models_doc, args.model_id)
        save_json(outdir / "04_selected_model.json", selected_model)

        try:
            generation_config = client.get_generation_config(args.model_id)
        except Exception as exc:
            generation_config = {"warning": str(exc)}
        save_json(outdir / "05_generation_config.json", generation_config)

        merged_settings = merge_settings(selected_model.get("settings") or {}, settings_overrides)
        save_json(outdir / "06_settings_request.json", merged_settings)
        settings_result = client.update_model_settings(args.model_id, merged_settings)
        save_json(outdir / "07_settings_response.json", settings_result)

        start_payload = {
            "model_id": args.model_id,
            "prompt_lengths": prompt_lengths,
            "generation_length": generation_length,
            "batch_sizes": batch_sizes,
        }
        save_json(outdir / "08_bench_request.json", start_payload)
        bench_start = client.start_benchmark(args.model_id, prompt_lengths, generation_length, batch_sizes)
        save_json(outdir / "09_bench_start.json", bench_start)
        bench_id = bench_start["bench_id"]

        events = []
        for event in client.stream_benchmark(bench_id, timeout=args.stream_timeout):
            events.append(event)
            print(json.dumps(event, separators=(",", ":")), flush=True)
        save_json(outdir / "10_bench_sse.json", events)

        results = client.get_benchmark_results(bench_id)
        save_json(outdir / "11_bench_results.json", results)

        summary = {
            "base_url": args.base_url,
            "model_id": args.model_id,
            "profile_id": args.profile_id,
            "bench_id": bench_id,
            "settings_overrides": settings_overrides,
            "benchmark": start_payload,
            "results_dir": str(outdir.resolve()),
            "final_status": results.get("status"),
        }
        save_json(outdir / "12_summary.json", summary)
        print(json.dumps(summary, indent=2))

    except KeyboardInterrupt:
        if bench_id:
            try:
                cancel = client.cancel_benchmark(bench_id)
                save_json(outdir / "zz_cancel.json", cancel)
            except Exception as exc:
                save_json(outdir / "zz_cancel_error.json", {"error": str(exc), "bench_id": bench_id})
        raise
    except Exception as exc:
        save_json(outdir / "zz_error.json", {"error": str(exc), "bench_id": bench_id})
        raise


if __name__ == "__main__":
    main()
