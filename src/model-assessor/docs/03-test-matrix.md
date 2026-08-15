# Comprehensive Test Matrix

This matrix is the first comprehensive benchmark plan. It is intentionally limited to 10 profiles so the output remains interpretable.

## Matrix Design

The matrix uses:

- 5 workload classes
- 2 speculative states: `mtp_off` and `mtp_on`

Each workload class has a fixed settings bundle. Only the MTP state changes between the paired profiles.

## Workload Classes

### 1. Short code research with external tools

Goal: quick repository inspection, one or two tool calls, concise synthesis.

Baseline settings:

- `max_context_window`: `16384`
- `max_tokens`: `1536`
- `temperature`: `0.2`
- `top_p`: `0.95`
- `top_k`: `64`
- `min_p`: `0.0`

Benchmark shape:

- `prompt_lengths`: `[1024]`
- `generation_length`: `256`
- `batch_sizes`: `[2, 4]`

### 2. Long code research with external tools

Goal: larger codebase context, multiple tool-result summaries, long synthesis.

Baseline settings:

- `max_context_window`: `65536`
- `max_tokens`: `4096`
- `temperature`: `0.25`
- `top_p`: `0.95`
- `top_k`: `64`
- `min_p`: `0.0`

Benchmark shape:

- `prompt_lengths`: `[4096, 8192]`
- `generation_length`: `256`
- `batch_sizes`: `[2]`

### 3. Short coding

Goal: focused code generation, patching, or explanation with high determinism.

Baseline settings:

- `max_context_window`: `16384`
- `max_tokens`: `1024`
- `temperature`: `0.1`
- `top_p`: `0.9`
- `top_k`: `40`
- `min_p`: `0.0`

Benchmark shape:

- `prompt_lengths`: `[1024]`
- `generation_length`: `256`
- `batch_sizes`: `[]`

### 4. Long coding

Goal: multi-file edits, longer code emission, more sustained decode.

Baseline settings:

- `max_context_window`: `32768`
- `max_tokens`: `4096`
- `temperature`: `0.1`
- `top_p`: `0.9`
- `top_k`: `40`
- `min_p`: `0.0`

Benchmark shape:

- `prompt_lengths`: `[4096, 8192]`
- `generation_length`: `512`
- `batch_sizes`: `[]`

### 5. Deep research

Goal: long-context synthesis and evidence aggregation across many sources.

Baseline settings:

- `max_context_window`: `131072`
- `max_tokens`: `8192`
- `temperature`: `0.35`
- `top_p`: `0.97`
- `top_k`: `80`
- `min_p`: `0.0`

Benchmark shape:

- `prompt_lengths`: `[16384]`
- `generation_length`: `512`
- `batch_sizes`: `[]`

## Concrete Profile List

| Profile ID | Workload | MTP |
| --- | --- | --- |
| `short_code_research_tools_mtp_off` | short code research with tools | off |
| `short_code_research_tools_mtp_on` | short code research with tools | on |
| `long_code_research_tools_mtp_off` | long code research with tools | off |
| `long_code_research_tools_mtp_on` | long code research with tools | on |
| `short_coding_mtp_off` | short coding | off |
| `short_coding_mtp_on` | short coding | on |
| `long_coding_mtp_off` | long coding | off |
| `long_coding_mtp_on` | long coding | on |
| `deep_research_mtp_off` | deep research | off |
| `deep_research_mtp_on` | deep research | on |

## Why This Is Enough For Phase 1

- It covers the workload classes that actually matter.
- It tests MTP explicitly without opening a full combinatorial sweep.
- It keeps comparisons interpretable because each pair only differs in MTP state.
- It leaves room for later targeted sweeps if one workload class needs deeper investigation.

## Deferred Variations

These are intentionally not part of the first pass:

- independent context-window sweep
- independent max-token sweep
- independent top-p sweep
- independent top-k sweep
- non-zero min-p sweep
- repetition or presence penalty exploration

Those should only be added after the first pass identifies the most promising workload profiles.
