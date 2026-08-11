# mlx-pep Profile Schema (JSONL)

A **profile set** is a `.jsonl` file: one JSON object per line, one line per tier
(`high` / `balanced` / `efficient`) for a single Hugging Face model id.

`schemaVersion` is required and starts at `1`.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `schemaVersion` | int | yes | Contract version. Start at `1`. |
| `id` | string | yes | Stable profile id: `<model-slug>-<tier>-<shorthash>`. |
| `modelHfId` | string | yes | Hugging Face repo id, e.g. `wang-yang/Ornith-1.0-35B-MTPLX`. |
| `tier` | enum | yes | `high` \| `balanced` \| `efficient`. |
| `engine` | string | yes | Runtime the profile targets. MVP: `omlx`. |
| `system` | object | yes | "macbook sys" group. Keys are exact settings, e.g. `iogpu.wired_limit_mb`. |
| `omlx` | object | yes | oMLX group, e.g. `memory_guard_tier`, `memory_guard_ceiling_gb`. |
| `harness` | object | yes | Per-harness config, e.g. `vscode.maxInputTokens`, `copilotCli.*`. |
| `sampler` | object | no | `temperature`, `topP`, `topK`, `repetitionPenalty`, `contextTokens`. |
| `provenance` | object | yes | `author`, `createdAt` (ISO-8601), `source` (`community`\|`local`\|`assess`). |
| `hardware` | object | yes | Fingerprint the profile was tuned for: `chip`, `memoryGb`, `modelIdentifier`. |

## Example

```json
{"schemaVersion":1,"id":"ornith-35b-mtplx-balanced-a1b2c3","modelHfId":"wang-yang/Ornith-1.0-35B-MTPLX","tier":"balanced","engine":"omlx","system":{"iogpu.wired_limit_mb":122880},"omlx":{"memory_guard_tier":"balanced","memory_guard_ceiling_gb":108},"harness":{"vscode":{"maxInputTokens":64000,"maxOutputTokens":3072},"copilotCli":{"maxPromptTokens":64000}},"sampler":{"temperature":0.7,"topP":0.95,"topK":20,"repetitionPenalty":1.02,"contextTokens":64000},"provenance":{"author":"matthewcorven","createdAt":"2026-08-11T00:00:00Z","source":"assess"},"hardware":{"chip":"Apple M4 Max","memoryGb":128,"modelIdentifier":"Mac16,5"}}
```

## Validation rules

- `tier` values must be unique within a profile set file.
- `system`/`omlx`/`harness` keys are free-form but validated against a known-key allowlist with a
  warning (not error) for unknown keys, so new settings can be shared before the client knows them.
- Serialization uses `System.Text.Json` source-generation in `MlxPep.Core`.
