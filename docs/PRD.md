# mlx-pep — Product Requirements Document

> Status: MVP scope locked. This document is the source of truth for the MVP milestone
> and the fast-follow backlog. Every GitHub issue links back to a section here.

## 1. Purpose

Provide users with well-performing local SLM/LLM models **plus** the associated system
and coding-harness configuration (VS Code / VS Code Insiders, GitHub Copilot CLI) needed
to actually run them well on their own hardware.

mlx-pep converges four use cases around that single purpose: discover/share tuned
**profiles**, reuse **models already in the shared Hugging Face cache**, apply profiles to
the local system and harnesses, and **generate** new tuned profiles by automated on-device
profiling.

## 2. Use Cases

1. **Browse community profiles** — list/search community-provided profile JSONL payloads and
   use them locally as-is or with manual tweaks.
2. **Reuse the shared HF cache** — browse/download models at the standard Hugging Face cache
   (`~/.cache/huggingface/hub`) shared by Transformers, MLX, vLLM, and llama.cpp, so models you
   already pulled just work with no re-download, and new downloads are available to every runtime.
3. **Per-model profiles** — for any model / variant (by Hugging Face id), download any community
   profile or one of your own locally saved profiles, and apply it.
4. **Automated profiling** — run the selected model on the user's local system against a fixed
   test suite (static assets in the repo) and produce a final report of recommended
   **High-performance / Balanced / Efficient** profiles, each backed by a JSONL payload, saved
   locally and optionally published as a community-shared profile.

## 3. Non-Goals (MVP)

- OpenCode and Claude Code harness config emission (fast-follow).
- Running inference directly through vLLM / llama.cpp / raw Transformers (MVP profiles via oMLX).
- Authn/authz for the community service beyond rate limiting + IP/CIDR/hostname blocking.
- Windows/Linux first-class support (developed cross-platform, validated on macOS Apple Silicon).

## 4. Personas

- **Operator** — runs mlx-pep on their Mac to get a working local model + harness config fast.
- **Contributor** — publishes a profile they generated so others with similar hardware benefit.

## 5. Architecture Overview

Everything is **.NET 10**.

```
mlx-pep/
  src/
    MlxPep.Core/        # shared: profile schema, HF-cache reader, system/oMLX detectors
    MlxPep.Cli/         # CLI foundation (System.CommandLine) + Terminal.Gui TUI layer
    MlxPep.Service/     # ASP.NET Core 10 single-file minimal API + Azure Blob backend
  tests/
    MlxPep.Core.Tests/
    MlxPep.Cli.Tests/
    MlxPep.Service.Tests/
  docs/
  assets/profiling-suite/   # static test assets for UC4
```

### 5.1 CLI is the foundation; TUI is presentation only

- **All operations and business logic live in the CLI command layer** and are 100% reachable via
  parameters (scriptable, CI-friendly, `--json` output on every command).
- The **TUI (Terminal.Gui)** is a thin presentation layer: hard-coded constants + builder logic that
  invokes the same command handlers. No business logic may live in the TUI.
- Package: `Terminal.Gui` (gui-cs). CLI parsing: `System.CommandLine`.

### 5.2 Dependency auto-detection

At runtime mlx-pep **detects existing user/global dependencies** and uses them; if a required
dependency is missing it guides the user to install it in **user** or **global** scope. Detected:
`dotnet`, `hf` CLI, `python3` + `model-assessor`, running/installed **oMLX**, **VS Code** and
**VS Code Insiders** (app bundle + `code` / `code-insiders` on PATH), **GitHub Copilot CLI**.

### 5.3 UC4 profiling delegates to `model-assessor`

mlx-pep does **not** re-implement the profiling pipeline. It **shells out** to the adjacent Python
`model-assessor` repo (oMLX admin harness → benchmark/prompt evals → normalized evidence →
recommendation manifest) and maps its recommendation output into three mlx-pep profile JSONL
payloads (High-performance / Balanced / Efficient).

### 5.4 Community profile service

A tiny **ASP.NET Core 10 single-file** minimal API backed by **Azure Blob Storage**:

- Public **download URLs** for profile JSONL.
- **CRUD + use-case endpoints** (list, get, publish/update, delete; query by model id / tier / hardware).
- **Rate limiting** via built-in `Microsoft.AspNetCore.RateLimiting` (`AddFixedWindowLimiter`).
- **In-memory, config-driven blocking** by **hostname**, **CIDR**, and **IP**, each independently
  toggleable via config/user options, hot-reloaded.

## 6. Profile JSONL Contract

See `docs/profile-schema.md`. A profile is one JSON object per line describing, for a given
Hugging Face model id + tier, the settings across three groups: **macbook sys**, **oMLX**, and
**harness (copilot-cli, vscode)**, plus sampler defaults and provenance/hardware fingerprint.

The existing `generate_ornith_matrix.py` prototype at the repo root is the reference implementation
for reading live **macbook sys** (`system_profiler`, `sysctl iogpu.wired_limit_mb`) and **oMLX**
(config + log) state; its logic is ported into `MlxPep.Core` detectors.

## 7. MVP Scope (Definition of Done)

The MVP is complete when an operator can, on a fresh Apple-Silicon Mac with oMLX installed:

1. `mlx-pep doctor` — see every dependency detected with install guidance for anything missing.
2. `mlx-pep models list` — see models already in the shared HF cache; `mlx-pep models get <hf-id>`
   downloads via `hf` into the shared cache.
3. `mlx-pep profiles list/search/pull <id>` — browse and download community profiles from the service.
4. `mlx-pep apply <profile> --harness copilot-cli|vscode` — write/emit harness config (with dry-run + backup).
5. `mlx-pep assess <hf-id>` — run UC4 profiling via model-assessor and emit 3 tiered JSONL profiles;
   `--publish` uploads to the service.
6. The community **service** is deployed with full CRUD, rate limiting, and IP/CIDR/hostname blocking.
7. The TUI exposes 1–5 as menus with 100% parity to the CLI.

## 8. Milestones

- **MVP** — sections 5–7 above.
- **Fast-Follow** — OpenCode + Claude Code harnesses; additional runtimes; service auth; publish-flow
  polish; Windows/Linux validation.

## 9. Risks & Mitigations

- **model-assessor coupling** (Python dependency from a .NET app): isolate behind a `ProfilingRunner`
  abstraction with a stable JSON contract; validate its presence in `doctor`.
- **oMLX admin API drift**: pin observed version behavior; treat unknown fields as evidence, not errors.
- **Service abuse**: rate limiting + blocklists are MVP, not fast-follow.
- **Harness config format drift**: emit to dry-run first, always back up existing files.
