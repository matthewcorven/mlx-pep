#!/usr/bin/env python3
from __future__ import annotations

import argparse
import datetime as dt
import json
import pathlib
import shutil
import subprocess
import tempfile
from typing import Any


def run(command: list[str], cwd: pathlib.Path | None = None) -> str:
    completed = subprocess.run(
        command,
        cwd=str(cwd) if cwd else None,
        check=True,
        capture_output=True,
        text=True,
    )
    return completed.stdout.strip()


def save_text(path: pathlib.Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.rstrip() + "\n", encoding="utf-8")


def save_json(path: pathlib.Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def detect_default_branch(repo_url: str) -> str:
    output = run(["git", "ls-remote", "--symref", repo_url, "HEAD"])
    for line in output.splitlines():
        if line.startswith("ref:") and line.endswith("\tHEAD"):
            return line.split("refs/heads/", 1)[1].split("\t", 1)[0]
    return "main"


def ensure_fresh_clone(repo_url: str, clone_dir: pathlib.Path, branch: str) -> tuple[str, str]:
    if clone_dir.exists():
        if not (clone_dir / ".git").exists():
            shutil.rmtree(clone_dir)
        else:
            run(["git", "fetch", "origin", branch, "--depth", "1"], cwd=clone_dir)
            run(["git", "checkout", branch], cwd=clone_dir)
            run(["git", "reset", "--hard", f"origin/{branch}"], cwd=clone_dir)
            commit = run(["git", "rev-parse", "HEAD"], cwd=clone_dir)
            return branch, commit

    clone_dir.parent.mkdir(parents=True, exist_ok=True)
    run(["git", "clone", "--depth", "1", "--branch", branch, repo_url, str(clone_dir)])
    commit = run(["git", "rev-parse", "HEAD"], cwd=clone_dir)
    return branch, commit


def extract_snippet(file_path: pathlib.Path, anchor: str, before: int = 6, after: int = 10) -> str:
    lines = file_path.read_text(encoding="utf-8").splitlines()
    match_index = None
    for index, line in enumerate(lines):
        if anchor in line:
            match_index = index
            break
    if match_index is None:
        raise RuntimeError(f"Anchor '{anchor}' not found in {file_path}")

    start = max(0, match_index - before)
    end = min(len(lines), match_index + after + 1)
    numbered = [f"{line_no + 1}: {lines[line_no]}" for line_no in range(start, end)]
    return "\n".join(numbered)


def build_aspire_context_bundle(repo_dir: pathlib.Path) -> str:
    snippets = [
        (
            "Unsupported-language diagnostic in the AppHost build targets",
            "src/Aspire.Hosting.AppHost/build/Aspire.Hosting.AppHost.in.targets",
            extract_snippet(
                repo_dir / "src/Aspire.Hosting.AppHost/build/Aspire.Hosting.AppHost.in.targets",
                "_WarnOnUnsupportedLanguage",
                before=8,
                after=8,
            ),
        ),
        (
            "Published diagnostic text for partially supported AppHost languages",
            "docs/list-of-diagnostics.md",
            extract_snippet(repo_dir / "docs/list-of-diagnostics.md", "ASPIRE001", before=2, after=2),
        ),
        (
            "CLI registration points for language discovery, guest AppHost projects, and TypeScript tooling checks",
            "src/Aspire.Cli/Program.cs",
            extract_snippet(repo_dir / "src/Aspire.Cli/Program.cs", "GuestAppHostProject", before=6, after=12),
        ),
        (
            "CLI output format spec showing AppHost discovery for C# and TypeScript",
            "docs/specs/cli-output-formats.md",
            extract_snippet(repo_dir / "docs/specs/cli-output-formats.md", '"language": "TypeScript"', before=10, after=14),
        ),
        (
            "TypeScript polyglot API compatibility notes and checked validation surfaces",
            "docs/ci/typescript-api-compat.md",
            extract_snippet(repo_dir / "docs/ci/typescript-api-compat.md", "tests/PolyglotAppHosts", before=8, after=8),
        ),
    ]

    sections = [
        "# Aspire Zig AppHost Planning Context",
        "",
        "This bundle is generated deterministically from the current default branch of the public `microsoft/aspire` repository.",
        "Use it to evaluate long-code-research planning quality for adding Zig as a supported AppHost language alongside C# and TypeScript.",
        "",
    ]
    for title, relative_path, snippet in snippets:
        sections.extend(
            [
                f"## {title}",
                "",
                f"Source: `{relative_path}`",
                "",
                "```text",
                snippet,
                "```",
                "",
            ]
        )
    return "\n".join(sections)


def write_static_practical_files(fixtures_dir: pathlib.Path) -> list[str]:
    files_written: list[str] = []

    aspire_brief = """# Practical Long-Code-Research Brief

Goal: assess how Aspire currently handles polyglot AppHost languages and plan a phased implementation for adding Zig as a supported AppHost language alongside C# and TypeScript.

Expected answer shape:

- summarize the current language-support seams
- identify concrete implementation surfaces that would need changes
- propose a phased plan with validation points and likely risks
- stay grounded in the supplied Aspire evidence instead of inventing support that already exists
"""
    save_text(fixtures_dir / "aspire_zig_apphost/01_task_brief.md", aspire_brief)
    files_written.append("fixtures/practical_live/aspire_zig_apphost/01_task_brief.md")

    commerce_brief = """# Practical Long-Coding Brief

Build a single-shot architecture and implementation response for a modern e-commerce system with these constraints:

- UI and backend both use Next.js in TypeScript
- product catalogue reads should be cached in Redis
- supporting datasets should include categories, pricing rules, inventory snapshots, merchandising content, and search facets
- orchestration should use Aspire with a TypeScript AppHost
- the answer should describe functional architecture for UI, backend, cache strategy, data boundaries, and deployment composition
- the answer should stay within a single coherent implementation proposal rather than brainstorming many unrelated options
"""
    save_text(fixtures_dir / "nextjs_aspire_commerce/01_solution_brief.md", commerce_brief)
    files_written.append("fixtures/practical_live/nextjs_aspire_commerce/01_solution_brief.md")

    commerce_requirements = """# Commerce Requirements

Required functional slices:

- storefront browsing, search, and product detail pages
- cart and checkout orchestration
- order submission and order-status lookup
- admin or backoffice flows for catalogue updates and pricing-rule refresh
- cache invalidation flow when catalogue or pricing data changes
- Aspire TypeScript AppHost composition for web frontend, web API, Redis, and background data-refresh components

Non-functional constraints:

- TypeScript end to end
- pragmatic service boundaries rather than microservice sprawl
- clear validation plan for cache correctness, degraded cache behavior, and local orchestration startup
"""
    save_text(fixtures_dir / "nextjs_aspire_commerce/02_requirements.md", commerce_requirements)
    files_written.append("fixtures/practical_live/nextjs_aspire_commerce/02_requirements.md")

    commerce_constraints = """# Commerce Constraints And Data Notes

- Redis is a cache for product catalogue and supporting datasets, not the primary system of record.
- Supporting datasets can include curated content, category trees, shipping regions, promotions, and tax or pricing lookup inputs.
- The response should explicitly distinguish the AppHost from the application services it orchestrates.
- The response should explain what belongs in the Next.js application layer versus background workers or data-refresh jobs.
"""
    save_text(fixtures_dir / "nextjs_aspire_commerce/03_constraints.md", commerce_constraints)
    files_written.append("fixtures/practical_live/nextjs_aspire_commerce/03_constraints.md")

    echo_scope = """# Practical Deep-Research Scope

Assess the viability of modifying Amazon Echo Show devices across known version families and revisions so they can participate in a Home Assistant-centric setup and custom voice or media workflows.

The evaluation should distinguish:

- what might be feasible only for specific generations or hardware revisions
- what remains speculative or impractical because of boot-chain, firmware, or service-lock-in constraints
- what can be integrated indirectly through Home Assistant, external speakers, or custom voice stacks instead of modifying the device itself
"""
    save_text(fixtures_dir / "echo_show_home_assistant/01_scope.md", echo_scope)
    files_written.append("fixtures/practical_live/echo_show_home_assistant/01_scope.md")

    echo_matrix = """# Echo Show Device Families To Cover

Known families to treat separately in the viability analysis:

- Echo Show 5 generations and hardware revisions
- Echo Show 8 generations and hardware revisions
- Echo Show 10 generations and motorized-display revisions
- Echo Show 15 wall-display family
- Echo Show 21 family
- older first-generation Echo Show devices

Required analysis dimensions:

- likely hardware access or boot-chain flexibility
- firmware and update-lock risks
- microphone, speaker, and display reuse practicality
- Home Assistant integration path
- custom voice-query path
- Spotify playback path using a paid account
- Amazon Music playback path using a paid account
"""
    save_text(fixtures_dir / "echo_show_home_assistant/02_device_matrix.md", echo_matrix)
    files_written.append("fixtures/practical_live/echo_show_home_assistant/02_device_matrix.md")

    echo_constraints = """# Echo Show Research Constraints

- Do not assume every Echo Show generation is rootable or similarly modifiable.
- Distinguish legal, DRM, and service-terms constraints from purely technical constraints.
- Paid Spotify and Amazon Music accounts remove one barrier, but they do not guarantee open local APIs or device-side integration rights.
- A good answer should compare direct device modification with indirect alternatives such as Home Assistant Assist, external displays, custom assistants, or media relays.
"""
    save_text(fixtures_dir / "echo_show_home_assistant/03_constraints.md", echo_constraints)
    files_written.append("fixtures/practical_live/echo_show_home_assistant/03_constraints.md")

    readme = """# Practical Live Fixtures

This fixture root contains practical prompt-quality scenarios intended for immediate live use with `scripts/next_phase/run_prompt_evals.py`.

The Aspire scenario is prepared from the latest default branch of `https://github.com/microsoft/aspire` by `scripts/next_phase/prepare_practical_live_fixtures.py`.
The long-coding and deep-research scenarios are curated local briefs designed to exercise more operator-realistic prompts than the small synthetic repo.
"""
    save_text(fixtures_dir / "README.md", readme)
    files_written.append("fixtures/practical_live/README.md")

    return files_written


def build_manifest(fixtures_dir: pathlib.Path, fixture_files: list[str], fixture_version: str) -> dict[str, Any]:
    total_size = 0
    for relative_path in fixture_files:
        total_size += (fixtures_dir.parents[1] / relative_path).stat().st_size
    return {
        "fixture_version": fixture_version,
        "fixture_files": sorted(fixture_files),
        "fixture_size_bytes": total_size,
        "change_notes": [
            "Initial practical live fixture set for long-code-research, long-coding, and deep-research prompt-quality coverage.",
            "Aspire language-support planning context is generated from the latest default branch of the public microsoft/aspire repository.",
            "Long and deep scenarios are local practical briefs meant for immediate live use with the existing prompt evaluation runner.",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Prepare practical live prompt-quality fixtures")
    parser.add_argument("--fixtures-dir", default="fixtures/practical_live")
    parser.add_argument("--aspire-repo-url", default="https://github.com/microsoft/aspire")
    parser.add_argument("--aspire-clone-dir", default=None)
    parser.add_argument("--aspire-branch", default=None)
    args = parser.parse_args()

    repo_root = pathlib.Path(__file__).resolve().parents[2]
    fixtures_dir = (repo_root / args.fixtures_dir).resolve()
    clone_dir = pathlib.Path(args.aspire_clone_dir) if args.aspire_clone_dir else pathlib.Path(tempfile.gettempdir()) / "model-assessor-aspire-practical"

    branch = args.aspire_branch or detect_default_branch(args.aspire_repo_url)
    branch, commit = ensure_fresh_clone(args.aspire_repo_url, clone_dir, branch)
    short_commit = commit[:12]

    if fixtures_dir.exists():
        for child in fixtures_dir.iterdir():
            if child.is_dir():
                shutil.rmtree(child)
            else:
                child.unlink()
    fixtures_dir.mkdir(parents=True, exist_ok=True)

    generated_at = dt.datetime.now(dt.timezone.utc).isoformat()
    aspire_state = {
        "repo_url": args.aspire_repo_url,
        "default_branch": branch,
        "commit": commit,
        "generated_at": generated_at,
        "clone_dir": str(clone_dir),
    }
    save_json(fixtures_dir / "aspire_zig_apphost/00_source_state.json", aspire_state)
    save_text(fixtures_dir / "aspire_zig_apphost/02_context_bundle.md", build_aspire_context_bundle(clone_dir))

    fixture_files = [
        "fixtures/practical_live/aspire_zig_apphost/00_source_state.json",
        "fixtures/practical_live/aspire_zig_apphost/02_context_bundle.md",
    ]
    fixture_files.extend(write_static_practical_files(fixtures_dir))

    fixture_version = f"practical-live-{dt.datetime.now().strftime('%Y%m%d')}-{short_commit}"
    manifest = build_manifest(fixtures_dir, fixture_files, fixture_version)
    save_json(fixtures_dir / "fixture_manifest.json", manifest)

    print(
        json.dumps(
            {
                "fixture_root": str(fixtures_dir),
                "fixture_version": fixture_version,
                "aspire_branch": branch,
                "aspire_commit": commit,
                "fixture_file_count": len(fixture_files),
                "fixture_manifest": str(fixtures_dir / "fixture_manifest.json"),
            },
            indent=2,
            sort_keys=True,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())