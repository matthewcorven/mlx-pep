# Apple Silicon Ornith MTPLX Matrix

## Detected hardware

| Key | Value |
|---|---|
| model_name | MacBook Pro |
| model_identifier | Mac16,5 |
| chip | Apple M4 Max |
| memory_gb | 128 |
| storage_capacity_tb | 2 |
| storage_free_gb | 546.49 |
| current_iogpu_wired_limit_mb | 0 |
| current_omlx_memory_guard_tier | balanced |
| current_omlx_ceiling_gb | 107.5 |
| current_omlx_metal_cap_gb | 107.5 |
| recommended_iogpu_wired_limit_mb | 124518 |

## High profile

| Group | Location | Key | Ornith 9B MTPLX | Ornith 35B MTPLX |
|---|---|---|---:|---:|
| macbook sys | Terminal / sysctl | iogpu.wired_limit_mb | 122880 | 124518 |
| omlx | oMLX runtime | memory_guard_tier | balanced | aggressive |
| omlx | oMLX runtime | memory_guard_ceiling_gb | auto | 112 |
| other | /Users/core/Library/Application Support/Code - Insiders/User/chatLanguageModels.json | maxInputTokens | 160000 | 96000 |
| other | /Users/core/Library/Application Support/Code - Insiders/User/chatLanguageModels.json | maxOutputTokens | 8192 | 4096 |

## Balanced profile

| Group | Location | Key | Ornith 9B MTPLX | Ornith 35B MTPLX |
|---|---|---|---:|---:|
| macbook sys | Terminal / sysctl | iogpu.wired_limit_mb | 122880 | 122880 |
| omlx | oMLX runtime | memory_guard_tier | balanced | balanced |
| omlx | oMLX runtime | memory_guard_ceiling_gb | auto | 108 |
| other | /Users/core/Library/Application Support/Code - Insiders/User/chatLanguageModels.json | maxInputTokens | 128000 | 64000 |
| other | /Users/core/Library/Application Support/Code - Insiders/User/chatLanguageModels.json | maxOutputTokens | 4096 | 3072 |

## Efficient profile

| Group | Location | Key | Ornith 9B MTPLX | Ornith 35B MTPLX |
|---|---|---|---:|---:|
| macbook sys | Terminal / sysctl | iogpu.wired_limit_mb | 0 | 0 |
| omlx | oMLX runtime | memory_guard_tier | safe | safe |
| omlx | oMLX runtime | memory_guard_ceiling_gb | 96 | 92 |
| other | /Users/core/Library/Application Support/Code - Insiders/User/chatLanguageModels.json | maxInputTokens | 96000 | 48000 |
| other | /Users/core/Library/Application Support/Code - Insiders/User/chatLanguageModels.json | maxOutputTokens | 2048 | 2048 |

## Notes

- `iogpu.wired_limit_mb=0` keeps the Apple default Metal cap; on this machine oMLX currently sees about **107.5 GB**.
- The current oMLX runtime is using **balanced** memory guard.
- The `other` rows target VS Code local model config values; Copilot itself does not expose prefill chunking or batch-size knobs in a documented way.
- This generator only reads local state. It does not unload models, alter oMLX, or install/uninstall anything.
