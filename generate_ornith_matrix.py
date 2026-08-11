#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Any


OMLX_CONFIG = Path.home() / "Library/Application Support/oMLX/config.json"
OMLX_LOG = Path.home() / "Library/Application Support/oMLX/logs/server.log"
CHAT_MODELS = Path.home() / "Library/Application Support/Code - Insiders/User/chatLanguageModels.json"


@dataclass(frozen=True)
class ModelProfile:
    label: str
    high_input_tokens: int
    balanced_input_tokens: int
    efficient_input_tokens: int
    high_output_tokens: int
    balanced_output_tokens: int
    efficient_output_tokens: int


MODELS = {
    "ornith_9b_mtplx": ModelProfile(
        label="Ornith 9B MTPLX",
        high_input_tokens=160_000,
        balanced_input_tokens=128_000,
        efficient_input_tokens=96_000,
        high_output_tokens=8_192,
        balanced_output_tokens=4_096,
        efficient_output_tokens=2_048,
    ),
    "ornith_35b_mtplx": ModelProfile(
        label="Ornith 35B MTPLX",
        high_input_tokens=96_000,
        balanced_input_tokens=64_000,
        efficient_input_tokens=48_000,
        high_output_tokens=4_096,
        balanced_output_tokens=3_072,
        efficient_output_tokens=2_048,
    ),
}


def run(*cmd: str) -> str:
    proc = subprocess.run(cmd, check=False, text=True, capture_output=True)
    return proc.stdout.strip() if proc.returncode == 0 else ""


def read_hardware() -> dict[str, Any]:
    hardware_text = run("system_profiler", "SPHardwareDataType", "SPStorageDataType")
    model_name = re.search(r"Model Name:\s+(.+)", hardware_text)
    model_identifier = re.search(r"Model Identifier:\s+(.+)", hardware_text)
    chip = re.search(r"Chip:\s+(.+)", hardware_text)
    memory = re.search(r"Memory:\s+(\d+)\s+GB", hardware_text)
    storage_free = re.search(r"Free:\s+([\d.]+)\s+GB", hardware_text)
    storage_capacity = re.search(r"Capacity:\s+(\d+)\s+TB", hardware_text)

    wired_limit_raw = run("sysctl", "iogpu.wired_limit_mb")
    wired_match = re.search(r"iogpu\.wired_limit_mb:\s+(\d+)", wired_limit_raw)
    wired_limit_mb = int(wired_match.group(1)) if wired_match else 0

    return {
        "model_name": model_name.group(1) if model_name else "Unknown",
        "model_identifier": model_identifier.group(1) if model_identifier else "Unknown",
        "chip": chip.group(1) if chip else "Unknown",
        "memory_gb": int(memory.group(1)) if memory else 0,
        "storage_free_gb": float(storage_free.group(1)) if storage_free else None,
        "storage_capacity_tb": int(storage_capacity.group(1)) if storage_capacity else None,
        "wired_limit_mb": wired_limit_mb,
    }


def read_omlx_state() -> dict[str, Any]:
    config: dict[str, Any] = json.loads(OMLX_CONFIG.read_text()) if OMLX_CONFIG.exists() else {}
    latest_guard = "unknown"
    latest_ceiling_gb: float | None = None
    latest_metal_cap_gb: float | None = None
    latest_recommended_wired_limit_mb: int | None = None

    if OMLX_LOG.exists():
        lines = OMLX_LOG.read_text(errors="ignore").splitlines()
        for line in reversed(lines):
            if latest_guard == "unknown":
                m = re.search(r"Memory guard tier:\s+([a-z]+)", line)
                if m:
                    latest_guard = m.group(1)
            if latest_ceiling_gb is None:
                m = re.search(r"ceiling=([\d.]+)GB", line)
                if m:
                    latest_ceiling_gb = float(m.group(1))
            if latest_metal_cap_gb is None:
                m = re.search(r"Metal cap \(([\d.]+)GB", line)
                if m:
                    latest_metal_cap_gb = float(m.group(1))
            if latest_recommended_wired_limit_mb is None:
                m = re.search(r"iogpu\.wired_limit_mb=(\d+)", line)
                if m:
                    latest_recommended_wired_limit_mb = int(m.group(1))
            if (
                latest_guard != "unknown"
                and latest_ceiling_gb is not None
                and latest_metal_cap_gb is not None
                and latest_recommended_wired_limit_mb is not None
            ):
                break

    return {
        "config_path": str(OMLX_CONFIG),
        "log_path": str(OMLX_LOG),
        "base_path": config.get("base_path", ""),
        "port": config.get("port"),
        "model_dir": config.get("model_dir", ""),
        "current_memory_guard_tier": latest_guard,
        "current_ceiling_gb": latest_ceiling_gb,
        "current_metal_cap_gb": latest_metal_cap_gb,
        "recommended_wired_limit_mb": latest_recommended_wired_limit_mb,
    }


def profile_values(hardware: dict[str, Any], omlx: dict[str, Any]) -> dict[str, Any]:
    current_wired = hardware["wired_limit_mb"]
    recommended_wired = omlx.get("recommended_wired_limit_mb") or min((hardware["memory_gb"] - 6) * 1024, hardware["memory_gb"] * 1024)
    balanced_wired = min(122_880, recommended_wired)

    return {
        "metadata": {
            "hardware": hardware,
            "omlx": omlx,
            "derived": {
                "recommended_wired_limit_mb": recommended_wired,
            },
        },
        "profiles": {
            "high": [
                {
                    "group": "macbook sys",
                    "location": "Terminal / sysctl",
                    "key": "iogpu.wired_limit_mb",
                    MODELS["ornith_9b_mtplx"].label: str(balanced_wired),
                    MODELS["ornith_35b_mtplx"].label: str(recommended_wired),
                },
                {
                    "group": "omlx",
                    "location": "oMLX runtime",
                    "key": "memory_guard_tier",
                    MODELS["ornith_9b_mtplx"].label: "balanced",
                    MODELS["ornith_35b_mtplx"].label: "aggressive",
                },
                {
                    "group": "omlx",
                    "location": "oMLX runtime",
                    "key": "memory_guard_ceiling_gb",
                    MODELS["ornith_9b_mtplx"].label: "auto",
                    MODELS["ornith_35b_mtplx"].label: "112",
                },
                {
                    "group": "other",
                    "location": str(CHAT_MODELS),
                    "key": "maxInputTokens",
                    MODELS["ornith_9b_mtplx"].label: str(MODELS["ornith_9b_mtplx"].high_input_tokens),
                    MODELS["ornith_35b_mtplx"].label: str(MODELS["ornith_35b_mtplx"].high_input_tokens),
                },
                {
                    "group": "other",
                    "location": str(CHAT_MODELS),
                    "key": "maxOutputTokens",
                    MODELS["ornith_9b_mtplx"].label: str(MODELS["ornith_9b_mtplx"].high_output_tokens),
                    MODELS["ornith_35b_mtplx"].label: str(MODELS["ornith_35b_mtplx"].high_output_tokens),
                },
            ],
            "balanced": [
                {
                    "group": "macbook sys",
                    "location": "Terminal / sysctl",
                    "key": "iogpu.wired_limit_mb",
                    MODELS["ornith_9b_mtplx"].label: str(current_wired or balanced_wired),
                    MODELS["ornith_35b_mtplx"].label: str(balanced_wired),
                },
                {
                    "group": "omlx",
                    "location": "oMLX runtime",
                    "key": "memory_guard_tier",
                    MODELS["ornith_9b_mtplx"].label: "balanced",
                    MODELS["ornith_35b_mtplx"].label: "balanced",
                },
                {
                    "group": "omlx",
                    "location": "oMLX runtime",
                    "key": "memory_guard_ceiling_gb",
                    MODELS["ornith_9b_mtplx"].label: "auto",
                    MODELS["ornith_35b_mtplx"].label: "108",
                },
                {
                    "group": "other",
                    "location": str(CHAT_MODELS),
                    "key": "maxInputTokens",
                    MODELS["ornith_9b_mtplx"].label: str(MODELS["ornith_9b_mtplx"].balanced_input_tokens),
                    MODELS["ornith_35b_mtplx"].label: str(MODELS["ornith_35b_mtplx"].balanced_input_tokens),
                },
                {
                    "group": "other",
                    "location": str(CHAT_MODELS),
                    "key": "maxOutputTokens",
                    MODELS["ornith_9b_mtplx"].label: str(MODELS["ornith_9b_mtplx"].balanced_output_tokens),
                    MODELS["ornith_35b_mtplx"].label: str(MODELS["ornith_35b_mtplx"].balanced_output_tokens),
                },
            ],
            "efficient": [
                {
                    "group": "macbook sys",
                    "location": "Terminal / sysctl",
                    "key": "iogpu.wired_limit_mb",
                    MODELS["ornith_9b_mtplx"].label: "0",
                    MODELS["ornith_35b_mtplx"].label: "0",
                },
                {
                    "group": "omlx",
                    "location": "oMLX runtime",
                    "key": "memory_guard_tier",
                    MODELS["ornith_9b_mtplx"].label: "safe",
                    MODELS["ornith_35b_mtplx"].label: "safe",
                },
                {
                    "group": "omlx",
                    "location": "oMLX runtime",
                    "key": "memory_guard_ceiling_gb",
                    MODELS["ornith_9b_mtplx"].label: "96",
                    MODELS["ornith_35b_mtplx"].label: "92",
                },
                {
                    "group": "other",
                    "location": str(CHAT_MODELS),
                    "key": "maxInputTokens",
                    MODELS["ornith_9b_mtplx"].label: str(MODELS["ornith_9b_mtplx"].efficient_input_tokens),
                    MODELS["ornith_35b_mtplx"].label: str(MODELS["ornith_35b_mtplx"].efficient_input_tokens),
                },
                {
                    "group": "other",
                    "location": str(CHAT_MODELS),
                    "key": "maxOutputTokens",
                    MODELS["ornith_9b_mtplx"].label: str(MODELS["ornith_9b_mtplx"].efficient_output_tokens),
                    MODELS["ornith_35b_mtplx"].label: str(MODELS["ornith_35b_mtplx"].efficient_output_tokens),
                },
            ],
        },
    }


def render_markdown(data: dict[str, Any]) -> str:
    hardware = data["metadata"]["hardware"]
    omlx = data["metadata"]["omlx"]
    derived = data["metadata"]["derived"]

    lines = [
        "# Apple Silicon Ornith MTPLX Matrix",
        "",
        "## Detected hardware",
        "",
        "| Key | Value |",
        "|---|---|",
        f"| model_name | {hardware['model_name']} |",
        f"| model_identifier | {hardware['model_identifier']} |",
        f"| chip | {hardware['chip']} |",
        f"| memory_gb | {hardware['memory_gb']} |",
        f"| storage_capacity_tb | {hardware['storage_capacity_tb']} |",
        f"| storage_free_gb | {hardware['storage_free_gb']} |",
        f"| current_iogpu_wired_limit_mb | {hardware['wired_limit_mb']} |",
        f"| current_omlx_memory_guard_tier | {omlx['current_memory_guard_tier']} |",
        f"| current_omlx_ceiling_gb | {omlx['current_ceiling_gb']} |",
        f"| current_omlx_metal_cap_gb | {omlx['current_metal_cap_gb']} |",
        f"| recommended_iogpu_wired_limit_mb | {derived['recommended_wired_limit_mb']} |",
        "",
    ]

    for profile_name, rows in data["profiles"].items():
        lines.extend(
            [
                f"## {profile_name.title()} profile",
                "",
                f"| Group | Location | Key | {MODELS['ornith_9b_mtplx'].label} | {MODELS['ornith_35b_mtplx'].label} |",
                "|---|---|---|---:|---:|",
            ]
        )
        for row in rows:
            lines.append(
                f"| {row['group']} | {row['location']} | {row['key']} | "
                f"{row[MODELS['ornith_9b_mtplx'].label]} | {row[MODELS['ornith_35b_mtplx'].label]} |"
            )
        lines.append("")

    lines.extend(
        [
            "## Notes",
            "",
            f"- `iogpu.wired_limit_mb=0` keeps the Apple default Metal cap; on this machine oMLX currently sees about **{omlx['current_metal_cap_gb']} GB**.",
            f"- The current oMLX runtime is using **{omlx['current_memory_guard_tier']}** memory guard.",
            "- The `other` rows target VS Code local model config values; Copilot itself does not expose prefill chunking or batch-size knobs in a documented way.",
            "- This generator only reads local state. It does not unload models, alter oMLX, or install/uninstall anything.",
            "",
        ]
    )
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate an Apple Silicon Ornith MTPLX config matrix.")
    parser.add_argument("--json", action="store_true", help="Emit JSON instead of Markdown.")
    parser.add_argument("--write", type=Path, help="Write output to this path.")
    args = parser.parse_args()

    data = profile_values(read_hardware(), read_omlx_state())
    output = json.dumps(data, indent=2) if args.json else render_markdown(data)
    if args.write:
        args.write.write_text(output)
    print(output)


if __name__ == "__main__":
    main()
