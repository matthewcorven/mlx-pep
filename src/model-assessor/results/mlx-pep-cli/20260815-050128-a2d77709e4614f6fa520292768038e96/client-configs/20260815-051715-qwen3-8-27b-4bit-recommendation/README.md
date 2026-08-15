# AI Harness Recommendation Reference

Recommendation ID: `20260815-051715-qwen3-8-27b-4bit-recommendation`
Created: `2026-08-15T05:17:15.235881+00:00`
Model ID: `Qwen3.8-27B-4bit`
Normalization ID: `20260815-051715-qwen3-8-27b-4bit-normalized`

These files are generated for operator review only.
They do not modify live VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, OpenCode, or oMLX configuration.

The primary manual-testing artifact is `ai-harness-reference.md`. It is one table that keeps the official harness terms beside this run's recommended oMLX and model values.

## Official Harness Terms Researched

| Harness | Config Surface | Key Official Terms | Source URLs |
| --- | --- | --- | --- |
| `VS Code` | Language Models editor plus settings.json selectors | `Chat: Manage Language Models`<br>`inlineChat.defaultModel`<br>`chat.utilityModel`<br>`chat.utilitySmallModel` | https://code.visualstudio.com/docs/agent-customization/language-models |
| `VS Code Insiders` | chatLanguageModels.json plus model picker selectors | `chatLanguageModels.json`<br>`vendor`<br>`name`<br>`models[].id`<br>`models[].name`<br>`models[].url`<br>`models[].apiType`<br>`models[].toolCalling`<br>`models[].maxInputTokens`<br>`models[].maxOutputTokens`<br>`chat.utilityModel`<br>`chat.utilitySmallModel` | https://code.visualstudio.com/docs/agent-customization/language-models |
| `Claude Code` | settings.json model/env fields or shell environment before launch | `model`<br>`env.ANTHROPIC_BASE_URL`<br>`env.ANTHROPIC_MODEL`<br>`env.ANTHROPIC_API_KEY`<br>`env.ANTHROPIC_AUTH_TOKEN`<br>`ANTHROPIC_CUSTOM_MODEL_OPTION`<br>`ANTHROPIC_CUSTOM_MODEL_OPTION_NAME`<br>`ANTHROPIC_CUSTOM_MODEL_OPTION_DESCRIPTION`<br>`ANTHROPIC_CUSTOM_MODEL_OPTION_SUPPORTED_CAPABILITIES` | https://code.claude.com/docs/en/settings<br>https://code.claude.com/docs/en/model-config<br>https://code.claude.com/docs/en/llm-gateway<br>https://code.claude.com/docs/en/env-vars |
| `GitHub Copilot CLI` | shell environment, command-line flags, and ~/.copilot/settings.json | `COPILOT_PROVIDER_BASE_URL`<br>`COPILOT_PROVIDER_TYPE`<br>`COPILOT_PROVIDER_API_KEY`<br>`COPILOT_MODEL`<br>`--model`<br>`settings.json:model` | https://docs.github.com/en/copilot/concepts/agents/copilot-cli/about-copilot-cli<br>https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-config-dir-reference<br>https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-command-reference |
| `OpenCode` | opencode.json provider and model configuration | `provider.<provider_id>.npm`<br>`provider.<provider_id>.name`<br>`provider.<provider_id>.options.baseURL`<br>`provider.<provider_id>.options.apiKey`<br>`provider.<provider_id>.models.<model_id>.name`<br>`provider.<provider_id>.models.<model_id>.limit.context`<br>`provider.<provider_id>.models.<model_id>.limit.output`<br>`model`<br>`small_model` | https://opencode.ai/docs/config<br>https://opencode.ai/docs/models<br>https://opencode.ai/docs/providers |

## Ranked Workload Recommendations

| Workload | Rank | Profile | MTP | Assistant | Confidence |
| --- | ---: | --- | --- | --- | --- |
| `deep_research` | 1 | `deep_research_mtp_off` | `off` | `none` | `low` |
| `deep_research` | 2 | `deep_research_mtp_on` | `off` | `none` | `low` |
| `long_code_research_tools` | 1 | `long_code_research_tools_mtp_on` | `off` | `none` | `low` |
| `long_code_research_tools` | 2 | `long_code_research_tools_mtp_off` | `off` | `none` | `low` |
| `long_coding` | 1 | `long_coding_mtp_on` | `off` | `none` | `low` |
| `long_coding` | 2 | `long_coding_mtp_off` | `off` | `none` | `low` |
| `short_code_research_tools` | 1 | `short_code_research_tools_mtp_off` | `off` | `none` | `low` |
| `short_code_research_tools` | 2 | `short_code_research_tools_mtp_on` | `off` | `none` | `low` |
| `short_coding` | 1 | `short_coding_mtp_off` | `off` | `none` | `low` |
| `short_coding` | 2 | `short_coding_mtp_on` | `off` | `none` | `low` |

## `deep_research`

### Rank 1

Profile: `deep_research_mtp_off`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 1 benchmark result(s); mean E2E 122.088 s; TTFT 94195.500 ms; generation TPS 18.400 tok/s; prefill TPS 173.900 tok/s; total throughput 138.400 tok/s; peak memory 17.86 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 131072,
  "max_tokens": 8192,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.35,
  "top_k": 80,
  "top_p": 0.97,
  "vlm_mtp_enabled": false
}
```

### Rank 2

Profile: `deep_research_mtp_on`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 1 benchmark result(s); mean E2E 129.786 s; TTFT 96676.700 ms; generation TPS 15.500 tok/s; prefill TPS 169.500 tok/s; total throughput 130.200 tok/s; peak memory 17.86 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 131072,
  "max_tokens": 8192,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.35,
  "top_k": 80,
  "top_p": 0.97,
  "vlm_mtp_enabled": false
}
```


## `long_code_research_tools`

### Rank 1

Profile: `long_code_research_tools_mtp_on`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 3 benchmark result(s); mean E2E 40.415 s; TTFT 34152.150 ms; generation TPS 19.750 tok/s; prefill TPS 178.650 tok/s; total throughput 132.650 tok/s; peak memory 17.14 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 65536,
  "max_tokens": 4096,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.25,
  "top_k": 64,
  "top_p": 0.95,
  "vlm_mtp_enabled": false
}
```

### Rank 2

Profile: `long_code_research_tools_mtp_off`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 3 benchmark result(s); mean E2E 43.379 s; TTFT 35425.300 ms; generation TPS 17.700 tok/s; prefill TPS 177.550 tok/s; total throughput 126.800 tok/s; peak memory 17.14 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 65536,
  "max_tokens": 4096,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.25,
  "top_k": 64,
  "top_p": 0.95,
  "vlm_mtp_enabled": false
}
```


## `long_coding`

### Rank 1

Profile: `long_coding_mtp_on`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 2 benchmark result(s); mean E2E 54.828 s; TTFT 35654.200 ms; generation TPS 19.250 tok/s; prefill TPS 171.450 tok/s; total throughput 119.550 tok/s; peak memory 17.35 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 32768,
  "max_tokens": 4096,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.1,
  "top_k": 40,
  "top_p": 0.9,
  "vlm_mtp_enabled": false
}
```

### Rank 2

Profile: `long_coding_mtp_off`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 2 benchmark result(s); mean E2E 59.942 s; TTFT 33692.050 ms; generation TPS 19.550 tok/s; prefill TPS 183.250 tok/s; total throughput 108.600 tok/s; peak memory 17.35 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 32768,
  "max_tokens": 4096,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.1,
  "top_k": 40,
  "top_p": 0.9,
  "vlm_mtp_enabled": false
}
```


## `short_code_research_tools`

### Rank 1

Profile: `short_code_research_tools_mtp_off`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 1 benchmark result(s); mean E2E 13.016 s; TTFT 4083.000 ms; generation TPS 28.700 tok/s; prefill TPS 250.800 tok/s; total throughput 98.300 tok/s; peak memory 15.85 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 16384,
  "max_tokens": 1536,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.2,
  "top_k": 64,
  "top_p": 0.95,
  "vlm_mtp_enabled": false
}
```

### Rank 2

Profile: `short_code_research_tools_mtp_on`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 1 benchmark result(s); mean E2E 13.749 s; TTFT 4424.300 ms; generation TPS 27.500 tok/s; prefill TPS 231.500 tok/s; total throughput 93.100 tok/s; peak memory 15.85 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 16384,
  "max_tokens": 1536,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.2,
  "top_k": 64,
  "top_p": 0.95,
  "vlm_mtp_enabled": false
}
```


## `short_coding`

### Rank 1

Profile: `short_coding_mtp_off`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 1 benchmark result(s); mean E2E 17.800 s; TTFT 5479.100 ms; generation TPS 20.800 tok/s; prefill TPS 186.900 tok/s; total throughput 71.900 tok/s; peak memory 15.85 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 16384,
  "max_tokens": 1024,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.1,
  "top_k": 40,
  "top_p": 0.9,
  "vlm_mtp_enabled": false
}
```

### Rank 2

Profile: `short_coding_mtp_on`
MTP: `off`
Assistant: `none`
Confidence: `low`

Speed summary: 1 benchmark result(s); mean E2E 18.026 s; TTFT 5564.300 ms; generation TPS 20.600 tok/s; prefill TPS 184.000 tok/s; total throughput 71.000 tok/s; peak memory 15.85 GiB.

Quality summary: No prompt-quality evaluation evidence available.

Tradeoffs:
- Ranking leans on speed evidence because no prompt-quality evaluation exists for this profile yet.

Caveats:
- Secondary evidence type for this workload is missing: evaluation.

Recommended oMLX settings:

```json
{
  "force_sampling": true,
  "max_context_window": 16384,
  "max_tokens": 1024,
  "min_p": 0.0,
  "mtp_enabled": false,
  "temperature": 0.1,
  "top_k": 40,
  "top_p": 0.9,
  "vlm_mtp_enabled": false
}
```

## Operator Steps

1. Choose the workload and rank to test from the ranked recommendations above.
2. Open `ai-harness-reference.md` and use the row for that workload, rank, and target harness.
3. Apply the row's oMLX server settings through the oMLX admin API or repo-local runner workflow before launching the harness.
4. Use the row's official harness terms and recommended values as the manual configuration checklist.
5. Review `unsupported-settings.md` before assuming an oMLX setting can be expressed directly in a client configuration file.
