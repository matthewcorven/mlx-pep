# Findings And Decisions

This document summarizes what has been learned so far and the decisions that follow from it.

## Findings

### 1. Gemma 4 12B supports very large context, but large context should not be the default

The model can support a much larger context window than should be used as an everyday operator default. Prefill cost rises quickly, and time-to-first-token becomes the dominant user experience issue for coding and research tasks.

### 2. MTP is workload-sensitive, not universally good or bad

Earlier runs suggested that MTP hurt short workloads and batching. Later runs under lower-entropy settings made the picture more mixed:

- short prompts still favored MTP off
- long prompts moved toward parity or mild MTP benefit
- batching no longer clearly regressed under the later settings

The safest interpretation is that MTP should be treated as a controlled ablation, not a global default.

### 3. Temperature is the most likely sampling knob affecting MTP usefulness

The strongest hypothesis from the observed runs is that lower temperature improved draft acceptance enough to make MTP less harmful or more useful. Top-p, top-k, and min-p are likely second-order effects compared with temperature.

### 4. oMLX benchmarking is automatable, but through HTTP rather than a benchmark CLI

The `omlx` CLI is sufficient for server lifecycle management. Benchmark automation requires the admin API.

## Decisions

### Default benchmarking strategy

Use a two-phase process:

1. Phase 1: throughput and latency benchmarking using repeatable profile definitions.
2. Phase 2: representative prompt quality evaluation using workload-specific prompts.

### Default workload coverage

The matrix will cover these five workload classes:

- short code research with external tools
- long code research with external tools
- short coding
- long coding
- deep research

### First-pass variation strategy

For the first comprehensive run, vary only:

- workload profile
- MTP on or off

Hold the rest of the settings fixed per workload class. This keeps the result set interpretable.

### Initial recommended operating assumptions

- keep `min_p` at `0.0`
- use lower temperature for coding profiles
- keep context windows bounded by workload class rather than model maximum
- explicitly set MTP flags for every run rather than inheriting server state

## Open Questions For Phase 2

1. At what prompt length does MTP cross from neutral or harmful to beneficial for this model?
2. Does the same MTP crossover hold under real prompt workloads, not only synthetic benchmark prompts?
3. Which client integrations need distinct model profiles rather than a shared profile family?
4. What is the best single reference-table format for translating benchmark findings into the official configuration terminology used by VS Code, VS Code Insiders, Claude Code, GitHub Copilot CLI, and OpenCode?
