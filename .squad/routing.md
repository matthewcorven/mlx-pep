# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture, scope, issue triage | Morpheus | PRD decomposition, trade-offs, issue assignment, cross-cutting design review |
| Core runtime, schema, detectors | Neo | Profile schema, HF cache reader, system/oMLX detectors, profiling contracts |
| CLI, TUI, harness config | Trinity | System.CommandLine handlers, Terminal.Gui presentation, VS Code/Copilot CLI emitters |
| Service and storage | Tank | ASP.NET Core API, Azure Blob integration, rate limiting, IP/CIDR/hostname blocking |
| Testing and reviewer passes | Switch | Unit tests, integration tests, acceptance coverage, failure triage |
| Fact verification | Fact Checker | Verify claims, challenge assumptions, confirm external references |
| Session logging | Scribe | Automatic — never needs routing |
| RAI review | Rai | Content safety, bias checks, credential detection, ethical review |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Morpheus |
| `squad:{name}` | Own the issue domain/charter identity | Named member |
| `squad:copilot` | Execute an eligible issue as @copilot while preserving the paired `squad:{member}` owner label | @copilot |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When an issue is eligible for @copilot, the Lead keeps the owning `squad:{member}` label and also adds `squad:copilot`; GitHub assigns the coding agent, but the member label/charter remains the authoritative persona.
3. When a `squad:{member}` label is applied without `squad:copilot`, that named member picks up the issue in their next session.
4. Members can reassign by removing their label and adding another member's label.
5. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, that label defines the owning squad persona. If `squad:copilot` is also present, @copilot executes the issue in that member's voice and boundaries by reading the paired charter. The Lead handles all `squad` (base label) triage.
