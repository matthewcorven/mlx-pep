# Prompt Templates For Phase-2 Quality Evaluation

These prompts are meant for representative output-quality evaluation after phase-1 synthetic benchmarking identifies promising profiles.

## General Rules

- Use the same prompt text across compared profiles.
- Record all tool outputs alongside final answers.
- Evaluate not only correctness but usefulness for the operator.
- Preserve any system prompt and model settings used in each run.

## 1. Short Code Research With External Tools

### Objective

Quickly identify where a behavior is implemented and summarize the controlling code path.

### Prompt Template

```text
You are investigating a codebase using external tools when needed.

Task:
Find where {BEHAVIOR_OR_SYMBOL} is implemented.
Summarize the controlling code path.
List the most relevant files.
Keep the answer concise and technical.
Do not propose code changes.
```

### Success Criteria

- identifies the correct controlling file or abstraction
- uses tools only as needed
- produces a concise, technically accurate summary

## 2. Long Code Research With External Tools

### Objective

Map a larger feature area across multiple files and explain architecture or behavior flow.

### Prompt Template

```text
You are investigating a codebase using external tools when needed.

Task:
Explain how {FEATURE_OR_WORKFLOW} works end to end.
Identify the main entry points, major data flow, and any important abstraction boundaries.
Include the files that matter most.
Be comprehensive but avoid unnecessary detail.
Do not change code.
```

### Success Criteria

- covers the main flow accurately
- does not get lost in unrelated files
- produces a synthesis rather than a dump of findings

## 3. Short Coding

### Objective

Produce a small targeted code change or patch recommendation with high precision.

### Prompt Template

```text
You are making a focused engineering change.

Task:
Implement or describe the smallest change needed to {GOAL}.
Prefer minimal edits.
Do not widen scope.
State assumptions briefly.
```

### Success Criteria

- output is precise and minimally scoped
- response does not ramble
- code or patch suggestion is directly actionable

## 4. Long Coding

### Objective

Plan or implement a multi-file coding task that requires sustained reasoning and more output tokens.

### Prompt Template

```text
You are making a multi-file engineering change.

Task:
Implement or describe the changes needed to {GOAL}.
Preserve existing conventions.
Group the work into major change areas.
Include validation steps.
Avoid unrelated refactors.
```

### Success Criteria

- maintains coherence over a longer response
- keeps the scope grouped and organized
- includes useful validation guidance

## 5. Deep Research

### Objective

Synthesize a larger amount of evidence into an operator-facing recommendation.

### Prompt Template

```text
You are performing deep technical research.

Task:
Analyze {TOPIC} using the provided evidence and external tools when needed.
Produce a recommendation for a human operator.
Separate findings, tradeoffs, and recommendation.
State uncertainty where appropriate.
```

### Success Criteria

- synthesizes rather than merely aggregates
- distinguishes findings from conclusions
- gives an operator-usable recommendation

## Suggested Evaluation Rubric

Use a simple 1 to 5 score per run for:

- correctness
- relevance
- concision
- structure
- operator usefulness
- tool discipline

For coding profiles, also score:

- determinism
- patch quality

For research profiles, also score:

- synthesis quality
- evidence integration
