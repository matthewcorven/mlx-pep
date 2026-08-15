#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import json
import pathlib
import re
import sys
from typing import Any

REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.next_phase.runner_lib import build_instance_topology


SCHEMA_VERSION = "1.0"
RECOMMENDED_SETTINGS_KEYS = [
    "max_context_window",
    "max_tokens",
    "temperature",
    "top_p",
    "top_k",
    "min_p",
    "force_sampling",
    "mtp_enabled",
    "vlm_mtp_enabled",
    "vlm_mtp_draft_model",
]
SUPPORTED_HARNESS_IDS = ["vscode", "vscode_insiders", "claude_code", "github_copilot_cli", "opencode"]

VS_CODE_LANGUAGE_MODELS_DOC = "https://code.visualstudio.com/docs/agent-customization/language-models"
CLAUDE_CODE_SETTINGS_DOC = "https://code.claude.com/docs/en/settings"
CLAUDE_CODE_MODEL_CONFIG_DOC = "https://code.claude.com/docs/en/model-config"
CLAUDE_CODE_GATEWAY_DOC = "https://code.claude.com/docs/en/llm-gateway"
CLAUDE_CODE_ENV_DOC = "https://code.claude.com/docs/en/env-vars"
COPILOT_CLI_ABOUT_DOC = "https://docs.github.com/en/copilot/concepts/agents/copilot-cli/about-copilot-cli"
COPILOT_CLI_CONFIG_DOC = "https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-config-dir-reference"
COPILOT_CLI_COMMAND_DOC = "https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-command-reference"
OPENCODE_CONFIG_DOC = "https://opencode.ai/docs/config"
OPENCODE_MODELS_DOC = "https://opencode.ai/docs/models"
OPENCODE_PROVIDERS_DOC = "https://opencode.ai/docs/providers"

HARNESSES = [
    {
        "id": "vscode",
        "display_name": "VS Code",
        "config_surface": "Language Models editor plus settings.json selectors",
        "official_terms": [
            "Chat: Manage Language Models",
            "inlineChat.defaultModel",
            "chat.utilityModel",
            "chat.utilitySmallModel",
        ],
        "source_urls": [VS_CODE_LANGUAGE_MODELS_DOC],
        "support_status": "local_models_supported_via_built_in_or_extension_provider",
    },
    {
        "id": "vscode_insiders",
        "display_name": "VS Code Insiders",
        "config_surface": "chatLanguageModels.json plus model picker selectors",
        "official_terms": [
            "chatLanguageModels.json",
            "vendor",
            "name",
            "models[].id",
            "models[].name",
            "models[].url",
            "models[].apiType",
            "models[].toolCalling",
            "models[].maxInputTokens",
            "models[].maxOutputTokens",
            "chat.utilityModel",
            "chat.utilitySmallModel",
        ],
        "source_urls": [VS_CODE_LANGUAGE_MODELS_DOC],
        "support_status": "custom_endpoint_provider_documented_in_insiders",
    },
    {
        "id": "claude_code",
        "display_name": "Claude Code",
        "config_surface": "settings.json model/env fields or shell environment before launch",
        "official_terms": [
            "model",
            "env.ANTHROPIC_BASE_URL",
            "env.ANTHROPIC_MODEL",
            "env.ANTHROPIC_API_KEY",
            "env.ANTHROPIC_AUTH_TOKEN",
            "ANTHROPIC_CUSTOM_MODEL_OPTION",
            "ANTHROPIC_CUSTOM_MODEL_OPTION_NAME",
            "ANTHROPIC_CUSTOM_MODEL_OPTION_DESCRIPTION",
            "ANTHROPIC_CUSTOM_MODEL_OPTION_SUPPORTED_CAPABILITIES",
        ],
        "source_urls": [
            CLAUDE_CODE_SETTINGS_DOC,
            CLAUDE_CODE_MODEL_CONFIG_DOC,
            CLAUDE_CODE_GATEWAY_DOC,
            CLAUDE_CODE_ENV_DOC,
        ],
        "support_status": "gateway_or_provider_specific_path_required",
    },
    {
        "id": "github_copilot_cli",
        "display_name": "GitHub Copilot CLI",
        "config_surface": "shell environment, command-line flags, and ~/.copilot/settings.json",
        "official_terms": [
            "COPILOT_PROVIDER_BASE_URL",
            "COPILOT_PROVIDER_TYPE",
            "COPILOT_PROVIDER_API_KEY",
            "COPILOT_MODEL",
            "--model",
            "settings.json:model",
        ],
        "source_urls": [COPILOT_CLI_ABOUT_DOC, COPILOT_CLI_CONFIG_DOC, COPILOT_CLI_COMMAND_DOC],
        "support_status": "official_byok_provider_env_supported",
    },
    {
        "id": "opencode",
        "display_name": "OpenCode",
        "config_surface": "opencode.json provider and model configuration",
        "official_terms": [
            "provider.<provider_id>.npm",
            "provider.<provider_id>.name",
            "provider.<provider_id>.options.baseURL",
            "provider.<provider_id>.options.apiKey",
            "provider.<provider_id>.models.<model_id>.name",
            "provider.<provider_id>.models.<model_id>.limit.context",
            "provider.<provider_id>.models.<model_id>.limit.output",
            "model",
            "small_model",
        ],
        "source_urls": [OPENCODE_CONFIG_DOC, OPENCODE_MODELS_DOC, OPENCODE_PROVIDERS_DOC],
        "support_status": "official_custom_provider_and_local_model_supported",
    },
]
HARNESS_BY_ID = {item["id"]: item for item in HARNESSES}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate client recommendation artifacts from a structured recommendation manifest"
    )
    parser.add_argument("--recommendation-manifest", required=True)
    parser.add_argument("--profiles-json", default="config/benchmark_profiles.json")
    parser.add_argument("--client-configs-dir", default="results/client-configs")
    return parser.parse_args()


def load_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: pathlib.Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def save_text(path: pathlib.Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.rstrip() + "\n", encoding="utf-8")


def resolve_path(repo_root: pathlib.Path, path_value: str) -> pathlib.Path:
    path = pathlib.Path(path_value)
    if path.is_absolute():
        return path
    return repo_root / path


def relative_to_repo(path: pathlib.Path, repo_root: pathlib.Path) -> str:
    return path.resolve().relative_to(repo_root.resolve()).as_posix()


def env_slug(value: str) -> str:
    slug = re.sub(r"[^A-Z0-9]+", "_", value.upper()).strip("_")
    return slug or "WORKLOAD"


def dedupe_sorted(values: list[str]) -> list[str]:
    return sorted({value for value in values if value})


def on_off(value: bool | None) -> str:
    if value is None:
        return "profile"
    return "on" if value else "off"


def require_keys(document: dict[str, Any], keys: list[str], document_name: str) -> None:
    missing = [key for key in keys if key not in document]
    if missing:
        raise SystemExit(f"{document_name} missing required keys: {', '.join(missing)}")


def pick_settings_source_path(recommendation: dict[str, Any]) -> str | None:
    candidates = []
    profile_id = recommendation.get("profile_id")
    for source_path in recommendation.get("source_paths", []):
        if not isinstance(source_path, str):
            continue
        if not source_path.endswith("/01_settings_request.json"):
            continue
        if profile_id and f"/{profile_id}/" not in source_path:
            continue
        candidates.append(source_path)
    if not candidates:
        return None
    return sorted(candidates)[0]


def load_settings_source(
    repo_root: pathlib.Path,
    recommendation: dict[str, Any],
) -> tuple[dict[str, Any] | None, str | None]:
    relative_path = pick_settings_source_path(recommendation)
    if relative_path is None:
        return None, None
    path = repo_root / relative_path
    if not path.is_file():
        return None, relative_path
    return load_json(path), relative_path


def filtered_settings(settings: dict[str, Any]) -> dict[str, Any]:
    filtered: dict[str, Any] = {}
    for key in RECOMMENDED_SETTINGS_KEYS:
        value = settings.get(key)
        if value is not None:
            filtered[key] = value
    return filtered


def build_recommended_settings(
    recommendation: dict[str, Any],
    profile_doc: dict[str, Any] | None,
    resolved_settings: dict[str, Any] | None,
) -> dict[str, Any]:
    settings: dict[str, Any] = {}
    if profile_doc and isinstance(profile_doc.get("settings"), dict):
        settings.update(filtered_settings(profile_doc["settings"]))
    elif resolved_settings:
        settings.update(filtered_settings(resolved_settings))

    mtp_recommended = recommendation.get("mtp_recommended")
    if isinstance(mtp_recommended, bool):
        settings["mtp_enabled"] = mtp_recommended
        settings["vlm_mtp_enabled"] = mtp_recommended

    assistant_model_id = recommendation.get("assistant_model_id")
    assistant_recommended = bool(recommendation.get("assistant_recommended"))
    if assistant_recommended and assistant_model_id:
        settings["vlm_mtp_draft_model"] = assistant_model_id
    else:
        settings.pop("vlm_mtp_draft_model", None)

    return settings


def build_instance_records(recommendation_manifest: dict[str, Any], recommendations: list[dict[str, Any]]) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    topology = recommendation_manifest.get("instance_topology") or build_instance_topology(recommendations)
    instances = topology.get("instances") or []
    if not instances:
        instances = [
            {
                "instance_id": "instance-1",
                "port": 8000,
                "base_url": "http://127.0.0.1:8000",
                "workload": None,
                "profile_id": None,
                "mtp_enabled": None,
                "assistant_model_id": recommendation_manifest.get("assistant_model_id"),
            }
        ]
    return topology, instances


def resolve_instance_for_workload(
    topology: dict[str, Any],
    instances: list[dict[str, Any]],
    workload: str,
) -> dict[str, Any]:
    instance_id = topology.get("workload_to_instance", {}).get(workload, instances[0].get("instance_id", "instance-1"))
    return next((item for item in instances if item.get("instance_id") == instance_id), instances[0])


def inference_api_base_url(instance: dict[str, Any]) -> str:
    base_url = instance.get("base_url") or f"http://127.0.0.1:{instance.get('port', 8000)}"
    return base_url.rstrip("/") + "/v1"


def markdown_kv_list(items: list[tuple[str, str]]) -> str:
    return "<br>".join(f"`{key}`: `{value}`" for key, value in items)


def markdown_list(items: list[str]) -> str:
    return "<br>".join(f"`{item}`" for item in items)


def format_scalar(value: Any) -> str:
    if isinstance(value, (bool, int, float)) or value is None:
        return json.dumps(value)
    return str(value)


def format_settings_inline(settings: dict[str, Any]) -> str:
    pairs = [f"`{key}`=`{format_scalar(value)}`" for key, value in sorted(settings.items())]
    return "<br>".join(pairs) if pairs else "none"


def workload_model_label(recommendation_manifest: dict[str, Any], recommendation: dict[str, Any]) -> str:
    return f"{recommendation_manifest['model_id']} ({recommendation['workload']} rank {recommendation['rank']})"


def build_harness_reference_row(
    harness_id: str,
    recommendation_manifest: dict[str, Any],
    recommendation: dict[str, Any],
    instance: dict[str, Any],
) -> dict[str, Any]:
    harness = HARNESS_BY_ID[harness_id]
    workload = recommendation["workload"]
    inference_base = inference_api_base_url(instance)
    model_id = recommendation_manifest["model_id"]
    assistant_model_id = recommendation.get("assistant_model_id") or "none"
    recommended_settings = recommendation.get("recommended_server_settings") or {}
    model_label = workload_model_label(recommendation_manifest, recommendation)

    if harness_id == "vscode":
        recommended_values = [
            ("Chat: Manage Language Models", "register a built-in or extension-provided local model entry first"),
            ("inlineChat.defaultModel", model_label),
            ("chat.utilityModel", model_label),
            ("chat.utilitySmallModel", model_label),
        ]
        notes = (
            "VS Code documents local-model support through built-in providers or extension-provided providers. "
            "The generic Custom Endpoint provider is documented on the same page as an Insiders capability, "
            "so this row is a selector-only reference for stable VS Code."
        )
    elif harness_id == "vscode_insiders":
        recommended_values = [
            ("vendor", "customendpoint"),
            ("name", f"oMLX {workload}"),
            ("apiKey", "operator-supplied OMLX_API_KEY"),
            ("models[].id", model_id),
            ("models[].name", model_label),
            ("models[].url", "requires a compatible Chat Completions, Responses, or Messages endpoint; raw oMLX chat compatibility is not yet verified in this repo"),
            ("models[].apiType", "chat-completions"),
            ("models[].toolCalling", "true if your routed endpoint supports it"),
            ("models[].maxInputTokens", str(recommended_settings.get("max_context_window", "operator-confirm"))),
            ("models[].maxOutputTokens", str(recommended_settings.get("max_tokens", "operator-confirm"))),
            ("chat.utilityModel", model_label),
            ("chat.utilitySmallModel", model_label),
        ]
        notes = (
            "Custom Endpoint is the documented generic self-hosted path in VS Code Insiders. "
            "The repo has only validated oMLX `/v1/models` and `/v1/completions`, so agent/tool-calling compatibility "
            "and the exact endpoint URL still require operator validation."
        )
    elif harness_id == "claude_code":
        recommended_values = [
            ("model", model_id),
            ("env.ANTHROPIC_MODEL", model_id),
            ("env.ANTHROPIC_BASE_URL", "Anthropic-compatible gateway URL in front of oMLX"),
            ("env.ANTHROPIC_API_KEY", "gateway or forwarded oMLX credential"),
            ("ANTHROPIC_CUSTOM_MODEL_OPTION", model_id),
            ("ANTHROPIC_CUSTOM_MODEL_OPTION_NAME", model_label),
            ("ANTHROPIC_CUSTOM_MODEL_OPTION_DESCRIPTION", f"oMLX workload {workload}, rank {recommendation['rank']}"),
        ]
        notes = (
            "Claude Code officially supports direct Anthropic API access, Bedrock, Vertex, Foundry, and Anthropic-compatible gateways. "
            "Its gateway docs require Anthropic Messages, Bedrock, or Vertex-style endpoints. Raw oMLX is not documented as a direct provider here, "
            "so use a gateway or proxy layer rather than the raw oMLX public API."
        )
    elif harness_id == "github_copilot_cli":
        recommended_values = [
            ("COPILOT_PROVIDER_TYPE", "openai"),
            ("COPILOT_PROVIDER_BASE_URL", inference_base),
            ("COPILOT_PROVIDER_API_KEY", "operator-supplied OMLX_API_KEY"),
            ("COPILOT_MODEL", model_id),
            ("settings.json:model", model_id),
        ]
        notes = (
            "GitHub Copilot CLI officially supports your own model provider through environment variables, including OpenAI-compatible endpoints, Ollama, and vLLM. "
            "The official docs require tool calling and streaming support, and recommend a context window of at least 128k tokens. "
            "This repo has not yet validated those requirements against oMLX."
        )
    else:
        provider_id = f"omlx-{instance.get('instance_id', 'instance-1')}"
        recommended_values = [
            ("provider.<provider_id>.npm", "@ai-sdk/openai-compatible"),
            ("provider.<provider_id>.name", f"oMLX {workload}"),
            ("provider.<provider_id>.options.baseURL", inference_base),
            ("provider.<provider_id>.options.apiKey", "{env:OMLX_API_KEY}"),
            (f"provider.{provider_id}.models.{model_id}.name", model_label),
            (f"provider.{provider_id}.models.{model_id}.limit.context", str(recommended_settings.get("max_context_window", "operator-confirm"))),
            (f"provider.{provider_id}.models.{model_id}.limit.output", str(recommended_settings.get("max_tokens", "operator-confirm"))),
            ("model", f"{provider_id}/{model_id}"),
            ("small_model", f"{provider_id}/{model_id}"),
        ]
        notes = (
            "OpenCode officially supports local models and custom providers through `opencode.json`. "
            "The documented custom-provider path uses `@ai-sdk/openai-compatible` with `options.baseURL`, `options.apiKey`, model metadata, and top-level `model`. "
            "Tool-calling behavior should still be verified against the routed oMLX model."
        )

    return {
        "workload": workload,
        "rank": recommendation.get("rank"),
        "profile_id": recommendation.get("profile_id"),
        "harness_id": harness_id,
        "harness_display_name": harness["display_name"],
        "config_surface": harness["config_surface"],
        "official_terms": harness["official_terms"],
        "recommended_values": [{"term": key, "value": value} for key, value in recommended_values],
        "instance_id": instance.get("instance_id"),
        "instance_base_url": instance.get("base_url") or f"http://127.0.0.1:{instance.get('port', 8000)}",
        "inference_api_base_url": inference_base,
        "model_id": model_id,
        "assistant_model_id": assistant_model_id,
        "recommended_server_settings": recommended_settings,
        "support_status": harness["support_status"],
        "notes": notes,
        "source_urls": harness["source_urls"],
    }


def build_harness_reference_rows(
    recommendation_manifest: dict[str, Any],
    workload_entries: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    recommendations = recommendation_manifest.get("recommendations") or []
    topology, instances = build_instance_records(recommendation_manifest, recommendations)
    rows: list[dict[str, Any]] = []
    for workload_entry in workload_entries:
        for ranked in workload_entry["ranked_recommendations"]:
            instance = resolve_instance_for_workload(topology, instances, ranked["workload"])
            for harness_id in SUPPORTED_HARNESS_IDS:
                rows.append(build_harness_reference_row(harness_id, recommendation_manifest, ranked, instance))
    rows.sort(key=lambda item: (item["workload"], item["rank"], SUPPORTED_HARNESS_IDS.index(item["harness_id"])))
    return rows


def build_harness_research_reference(
    recommendation_id: str,
    repo_root: pathlib.Path,
    output_dir: pathlib.Path,
) -> list[dict[str, Any]]:
    artifact_path = relative_to_repo(output_dir / "ai-harness-reference.md", repo_root)
    return [
        {
            "harness_id": harness["id"],
            "display_name": harness["display_name"],
            "artifact_path": artifact_path,
            "config_surface": harness["config_surface"],
            "official_terms": harness["official_terms"],
            "support_status": harness["support_status"],
            "source_urls": harness["source_urls"],
        }
        for harness in HARNESSES
    ]


def build_unsupported_settings(
    workload_entries: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    by_setting: dict[str, dict[str, Any]] = {}
    for workload_entry in workload_entries:
        for ranked in workload_entry["ranked_recommendations"]:
            for setting, value in ranked["recommended_server_settings"].items():
                current = by_setting.setdefault(
                    setting,
                    {
                        "setting": setting,
                        "harnesses": SUPPORTED_HARNESS_IDS.copy(),
                        "workloads": set(),
                        "values": [],
                        "reason": "This is an oMLX-side runtime setting, not a portable client-native custom-model key across VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode.",
                        "apply_instead": "Apply the setting through the oMLX admin API, the benchmark profile catalog, or the repo-local assessment scripts before launching the selected AI harness.",
                    },
                )
                current["workloads"].add(ranked["workload"])
                current["values"].append({"workload": ranked["workload"], "rank": ranked["rank"], "value": value})
    output = []
    for setting in sorted(by_setting):
        item = by_setting[setting]
        item["workloads"] = sorted(item["workloads"])
        item["values"] = sorted(item["values"], key=lambda value: (value["workload"], value["rank"]))
        output.append(item)
    return output


def build_unsupported_markdown(items: list[dict[str, Any]]) -> str:
    lines = [
        "# Unsupported Native Client Settings",
        "",
        "These recommendation fields remain oMLX-side settings. The generated AI harness reference table carries the values so an operator can apply them intentionally before using a downstream harness.",
        "",
        "| Setting | Workloads | Reason | Apply Instead |",
        "| --- | --- | --- | --- |",
    ]
    for item in items:
        lines.append(
            f"| `{item['setting']}` | {', '.join(item['workloads'])} | {item['reason']} | {item['apply_instead']} |"
        )
    return "\n".join(lines)


def build_workload_entries(
    repo_root: pathlib.Path,
    recommendation_manifest: dict[str, Any],
    profiles_by_id: dict[str, dict[str, Any]],
) -> tuple[list[dict[str, Any]], list[str]]:
    workload_groups: dict[str, list[dict[str, Any]]] = {}
    source_paths: list[str] = [relative_to_repo(resolve_path(repo_root, "config/benchmark_profiles.json"), repo_root)]

    for recommendation in recommendation_manifest["recommendations"]:
        workload = recommendation.get("workload") or "unknown"
        workload_groups.setdefault(workload, []).append(recommendation)

    workload_entries: list[dict[str, Any]] = []
    for workload in sorted(workload_groups):
        ranked_recommendations: list[dict[str, Any]] = []
        for recommendation in sorted(workload_groups[workload], key=lambda item: (item.get("rank") or 9999, item.get("profile_id") or "")):
            profile_id = recommendation.get("profile_id")
            profile_doc = profiles_by_id.get(profile_id)
            resolved_settings, settings_source_path = load_settings_source(repo_root, recommendation)
            recommended_settings = build_recommended_settings(recommendation, profile_doc, resolved_settings)
            source_paths.extend(recommendation.get("source_paths", []))
            if settings_source_path:
                source_paths.append(settings_source_path)

            ranked_recommendations.append(
                {
                    "schema_version": SCHEMA_VERSION,
                    "run_id": None,
                    "evaluation_run_id": None,
                    "normalization_id": recommendation_manifest.get("normalization_id"),
                    "recommendation_id": recommendation_manifest["recommendation_id"],
                    "created_at": recommendation_manifest["created_at"],
                    "model_id": recommendation_manifest["model_id"],
                    "assistant_model_id": recommendation.get("assistant_model_id"),
                    "profile_id": profile_id,
                    "workload": workload,
                    "mtp_enabled": recommendation.get("mtp_recommended"),
                    "rank": recommendation.get("rank"),
                    "assistant_recommended": bool(recommendation.get("assistant_recommended")),
                    "confidence": recommendation.get("confidence"),
                    "speed_summary": recommendation.get("speed_summary"),
                    "quality_summary": recommendation.get("quality_summary"),
                    "tradeoffs": recommendation.get("tradeoffs") or [],
                    "caveats": recommendation.get("caveats") or [],
                    "recommended_server_settings": recommended_settings,
                    "profile_source_path": "config/benchmark_profiles.json",
                    "settings_source_path": settings_source_path,
                    "source_paths": recommendation.get("source_paths") or [],
                    "harness_reference_action": "Use the matching workload and harness rows in ai-harness-reference.md; apply oMLX-side settings before configuring or launching the selected harness.",
                    "unsupported_direct_settings": sorted(recommended_settings.keys()),
                }
            )

        workload_entries.append(
            {
                "schema_version": SCHEMA_VERSION,
                "run_id": None,
                "evaluation_run_id": None,
                "normalization_id": recommendation_manifest.get("normalization_id"),
                "recommendation_id": recommendation_manifest["recommendation_id"],
                "created_at": recommendation_manifest["created_at"],
                "model_id": recommendation_manifest["model_id"],
                "assistant_model_id": recommendation_manifest.get("assistant_model_id"),
                "profile_id": None,
                "workload": workload,
                "mtp_enabled": None,
                "recommendation_count": len(ranked_recommendations),
                "ranked_recommendations": ranked_recommendations,
            }
        )

    return workload_entries, dedupe_sorted(source_paths)


def build_readme(
    recommendation_manifest: dict[str, Any],
    harness_research_reference: list[dict[str, Any]],
    workload_entries: list[dict[str, Any]],
) -> str:
    lines = [
        "# AI Harness Recommendation Reference",
        "",
        f"Recommendation ID: `{recommendation_manifest['recommendation_id']}`",
        f"Created: `{recommendation_manifest['created_at']}`",
        f"Model ID: `{recommendation_manifest['model_id']}`",
        f"Normalization ID: `{recommendation_manifest.get('normalization_id') or 'none'}`",
        "",
        "These files are generated for operator review only.",
        "They do not modify live VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, OpenCode, or oMLX configuration.",
        "",
        "The primary manual-testing artifact is `ai-harness-reference.md`. It is one table that keeps the official harness terms beside this run's recommended oMLX and model values.",
        "",
        "## Official Harness Terms Researched",
        "",
        "| Harness | Config Surface | Key Official Terms | Source URLs |",
        "| --- | --- | --- | --- |",
    ]
    for item in harness_research_reference:
        lines.append(
            f"| `{item['display_name']}` | {item['config_surface']} | {markdown_list(item['official_terms'])} | {'<br>'.join(item['source_urls'])} |"
        )

    lines.extend(
        [
            "",
            "## Ranked Workload Recommendations",
            "",
            "| Workload | Rank | Profile | MTP | Assistant | Confidence |",
            "| --- | ---: | --- | --- | --- | --- |",
        ]
    )
    for workload_entry in workload_entries:
        for ranked in workload_entry["ranked_recommendations"]:
            lines.append(
                f"| `{ranked['workload']}` | {ranked['rank']} | `{ranked.get('profile_id') or 'unknown'}` | `{on_off(ranked.get('mtp_enabled'))}` | `{ranked.get('assistant_model_id') or 'none'}` | `{ranked.get('confidence') or 'unknown'}` |"
            )

    for workload_entry in workload_entries:
        lines.extend(["", f"## `{workload_entry['workload']}`", ""])
        for ranked in workload_entry["ranked_recommendations"]:
            lines.extend(
                [
                    f"### Rank {ranked['rank']}",
                    "",
                    f"Profile: `{ranked.get('profile_id') or 'unknown'}`",
                    f"MTP: `{on_off(ranked.get('mtp_enabled'))}`",
                    f"Assistant: `{ranked.get('assistant_model_id') or 'none'}`",
                    f"Confidence: `{ranked.get('confidence') or 'unknown'}`",
                    "",
                    f"Speed summary: {ranked.get('speed_summary') or 'none'}",
                    "",
                    f"Quality summary: {ranked.get('quality_summary') or 'none'}",
                    "",
                ]
            )
            if ranked["tradeoffs"]:
                lines.append("Tradeoffs:")
                for item in ranked["tradeoffs"]:
                    lines.append(f"- {item}")
                lines.append("")
            if ranked["caveats"]:
                lines.append("Caveats:")
                for item in ranked["caveats"]:
                    lines.append(f"- {item}")
                lines.append("")
            lines.extend(
                [
                    "Recommended oMLX settings:",
                    "",
                    "```json",
                    json.dumps(ranked["recommended_server_settings"], indent=2, sort_keys=True),
                    "```",
                    "",
                ]
            )

    lines.extend(
        [
            "## Operator Steps",
            "",
            "1. Choose the workload and rank to test from the ranked recommendations above.",
            "2. Open `ai-harness-reference.md` and use the row for that workload, rank, and target harness.",
            "3. Apply the row's oMLX server settings through the oMLX admin API or repo-local runner workflow before launching the harness.",
            "4. Use the row's official harness terms and recommended values as the manual configuration checklist.",
            "5. Review `unsupported-settings.md` before assuming an oMLX setting can be expressed directly in a client configuration file.",
        ]
    )
    return "\n".join(lines)


def build_ai_harness_reference_markdown(
    recommendation_manifest: dict[str, Any],
    harness_reference_rows: list[dict[str, Any]],
) -> str:
    lines = [
        "# AI Harness Reference Table",
        "",
        f"Recommendation ID: `{recommendation_manifest['recommendation_id']}`",
        f"Model ID: `{recommendation_manifest['model_id']}`",
        "",
        "Use this single table for manual harness testing. Each row pairs the terms used by the target AI harness with the recommended values from this assessment run.",
        "",
        "Do not paste placeholder credentials into a real config. Replace credential placeholders with your own local secret handling.",
        "",
        "| Workload | Rank | Harness | Config Surface | Official Terms And Recommended Values | oMLX Server Settings | Instance | Notes | Sources |",
        "| --- | ---: | --- | --- | --- | --- | --- | --- | --- |",
    ]
    for row in harness_reference_rows:
        recommended_pairs = [(item["term"], str(item["value"])) for item in row["recommended_values"]]
        instance = (
            f"`{row['instance_id']}`<br>base: `{row['instance_base_url']}`<br>API base: `{row['inference_api_base_url']}`"
        )
        lines.append(
            "| "
            f"`{row['workload']}` | "
            f"{row['rank']} | "
            f"`{row['harness_display_name']}` | "
            f"{row['config_surface']} | "
            f"{markdown_kv_list(recommended_pairs)} | "
            f"{format_settings_inline(row['recommended_server_settings'])} | "
            f"{instance} | "
            f"{row['notes']} | "
            f"{'<br>'.join(row['source_urls'])} |"
        )
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    repo_root = pathlib.Path(__file__).resolve().parents[2]

    recommendation_manifest_path = resolve_path(repo_root, args.recommendation_manifest)
    profiles_json_path = resolve_path(repo_root, args.profiles_json)

    recommendation_manifest = load_json(recommendation_manifest_path)
    require_keys(
        recommendation_manifest,
        ["schema_version", "recommendation_id", "created_at", "model_id", "recommendations", "source_paths"],
        "recommendation_manifest",
    )

    profiles_doc = load_json(profiles_json_path)
    profiles_by_id = {
        profile.get("id"): profile
        for profile in profiles_doc.get("profiles", [])
        if isinstance(profile, dict) and profile.get("id")
    }

    output_dir = resolve_path(repo_root, args.client_configs_dir) / recommendation_manifest["recommendation_id"]
    workload_entries, derived_source_paths = build_workload_entries(repo_root, recommendation_manifest, profiles_by_id)
    topology = build_instance_topology(recommendation_manifest.get("recommendations", []))
    recommendation_manifest = dict(recommendation_manifest)
    recommendation_manifest["instance_topology"] = topology
    harness_research_reference = build_harness_research_reference(
        recommendation_manifest["recommendation_id"],
        repo_root,
        output_dir,
    )
    harness_reference_rows = build_harness_reference_rows(recommendation_manifest, workload_entries)
    unsupported_settings = build_unsupported_settings(workload_entries)
    source_paths = dedupe_sorted(
        [
            relative_to_repo(recommendation_manifest_path, repo_root),
            relative_to_repo(profiles_json_path, repo_root),
            *recommendation_manifest.get("source_paths", []),
            *derived_source_paths,
        ]
    )

    readme_path = output_dir / "README.md"
    client_json_path = output_dir / "client_recommendations.json"
    harness_reference_path = output_dir / "ai-harness-reference.md"
    unsupported_path = output_dir / "unsupported-settings.md"
    obsolete_paths = [
        output_dir / "vscode-settings.example.json",
        output_dir / "claude-code.example.md",
        output_dir / "github-copilot-cli.example.md",
        output_dir / "opencode.example.json",
    ]

    client_recommendations = {
        "schema_version": SCHEMA_VERSION,
        "run_id": None,
        "evaluation_run_id": None,
        "normalization_id": recommendation_manifest.get("normalization_id"),
        "recommendation_id": recommendation_manifest["recommendation_id"],
        "created_at": recommendation_manifest["created_at"],
        "model_id": recommendation_manifest["model_id"],
        "assistant_model_id": recommendation_manifest.get("assistant_model_id"),
        "profile_id": None,
        "workload": None,
        "mtp_enabled": None,
        "source_paths": source_paths,
        "recommendation_manifest_path": relative_to_repo(recommendation_manifest_path, repo_root),
        "profiles_catalog_path": relative_to_repo(profiles_json_path, repo_root),
        "artifacts": {
            "readme": relative_to_repo(readme_path, repo_root),
            "client_recommendations": relative_to_repo(client_json_path, repo_root),
            "ai_harness_reference": relative_to_repo(harness_reference_path, repo_root),
            "unsupported_settings": relative_to_repo(unsupported_path, repo_root),
        },
        "instance_topology": topology,
        "supported_harnesses": SUPPORTED_HARNESS_IDS,
        "harness_research_reference": harness_research_reference,
        "client_recommendation_rows": harness_reference_rows,
        "workloads": workload_entries,
        "unsupported_settings": unsupported_settings,
        "missing_evidence": recommendation_manifest.get("missing_evidence") or [],
    }

    save_text(readme_path, build_readme(recommendation_manifest, harness_research_reference, workload_entries))
    save_text(harness_reference_path, build_ai_harness_reference_markdown(recommendation_manifest, harness_reference_rows))
    save_json(client_json_path, client_recommendations)
    save_text(unsupported_path, build_unsupported_markdown(unsupported_settings))
    for obsolete_path in obsolete_paths:
        if obsolete_path.exists():
            obsolete_path.unlink()

    output = {
        "schema_version": SCHEMA_VERSION,
        "recommendation_id": recommendation_manifest["recommendation_id"],
        "created_at": recommendation_manifest["created_at"],
        "model_id": recommendation_manifest["model_id"],
        "artifact_dir": relative_to_repo(output_dir, repo_root),
        "artifact_paths": client_recommendations["artifacts"],
        "workload_count": len(workload_entries),
    }
    print(json.dumps(output, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())