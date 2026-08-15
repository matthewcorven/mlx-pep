# Chunk 5: Client Configuration Artifacts

## Purpose

Generate one AI harness reference table for VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode from the structured recommendation manifest and official harness documentation.

## Context To Read

- `docs/06-next-phase-handoff.md`
- `docs/06-next-phase-chunks/01-agent-architecture.md`
- `docs/06-next-phase-chunks/shared-contracts.md`
- `docs/06-next-phase-chunks/04-normalization-and-reporting.md`
- recommendation manifest output from Chunk 4

## Scope

This chunk owns AI harness-facing configuration guidance. It should not rerun benchmarks, rescore prompt outputs, or change ranking logic except where a harness cannot support a recommended setting directly.

This chunk is also where workload recommendations become manual harness-configuration references. The implementation should not emit guessed per-harness files; it should emit a single table that places each harness's official terms next to the recommended values for every workload row.

Current repo status: the artifact generator produces topology-aware objects and explicit `instance_topology` metadata, and the live assessment runner now consumes the same contract so generated objects can describe real separate-port, multi-instance execution rather than advisory-only examples.

## Required Outputs

- Markdown AI harness guidance per workload class.
- JSON client recommendation manifests.
- One `ai-harness-reference.md` table covering:
  - VS Code
  - VS Code Insiders
  - Claude Code
  - GitHub Copilot CLI
  - OpenCode
- A harness research reference describing, for each harness, official config surface, official terms, source URLs, precedence/limitations if known, and whether each oMLX recommendation can be expressed directly.
- A machine-readable set of AI harness reference rows, one per workload recommendation and supported harness.
- An instance-topology declaration that tells the operator whether one hosted model instance is enough or whether multiple simultaneous instances are required because workload recommendations diverge on MTP, assistant usage, or other oMLX-side settings.
- Documentation describing unsupported or manual-only client settings.

## Acceptance Criteria

- Artifacts are generated from the structured recommendation manifest rather than freeform report text.
- Every workload recommendation maps to an oMLX profile and MTP/assistant decision or clearly states why no client-specific setting can express it.
- Every supported harness receives a first-class table row for each workload recommendation rather than a guessed config file or generic shell metadata example.
- Client outputs distinguish direct settings, operator instructions, and unsupported/manual steps.
- All client-config outputs are recommendations for operator review. The chunk must not auto-apply, auto-load, or write actual user/workspace client configuration files.
- Output filenames and content label snippets as examples, templates, or recommendations.
- Snippets avoid embedding secrets such as API keys.
- The generator is deterministic and repeatable.
- Outputs are organized under a documented results or config path.
- The implementation remains model-agnostic and can emit artifacts for future model IDs.
- AI harness reference rows use parameterized model IDs and profile data from the recommendation manifest, not hardcoded Gemma defaults.
- Generated artifacts are reference-table guidance for manual configuration and validation, not assumed copy-paste runnable config files.
- If workload recommendations cannot share one hosted instance, the generated output says so explicitly and identifies the divergent oMLX-side settings that force multiple simultaneous instances.
- Unsupported settings are documented separately so the operator can distinguish direct client settings from oMLX-side settings.

## Definition Of Done

- Generator code, templates if needed, and docs exist in the repo.
- JSON artifacts validate as JSON.
- Generated snippets are safe to inspect and do not contain credentials.
- A sample generation path works from sample or real recommendation data.
- `shared-contracts.md` remains accurate for client artifact outputs, or is updated with any intentional compatible change.
- The final response lists changed files, generated sample artifacts, and unsupported client behaviors.

## Launch Prompt

You are implementing Chunk 5 of the model-assessor next phase. Read `docs/06-next-phase-handoff.md`, Chunk 1, Chunk 4, `docs/06-next-phase-chunks/shared-contracts.md`, and `docs/06-next-phase-chunks/05-client-config-artifacts.md`. Build deterministic AI harness reference artifact generation for VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode. Consume structured recommendation data and official harness documentation; do not reinterpret benchmark evidence. Generate one table with per-harness, per-workload terminology and recommended values for operator inspection only; do not mutate actual client config. Declare when multiple simultaneously hosted model instances are required. Validate JSON and report changed files plus sample outputs.
