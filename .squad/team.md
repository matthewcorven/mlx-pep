# Squad Team

> mlx-pep

## Coordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Morpheus | Lead | .squad/agents/morpheus/charter.md | active |
| Neo | Core Dev | .squad/agents/neo/charter.md | active |
| Trinity | CLI/Harness Dev | .squad/agents/trinity/charter.md | active |
| Tank | Service Dev | .squad/agents/tank/charter.md | active |
| Switch | Tester | .squad/agents/switch/charter.md | active |
| Scribe | Session Logger | .squad/agents/scribe/charter.md | active |
| Ralph | Work Monitor | .squad/agents/ralph/charter.md | active |
| Rai | RAI Reviewer | .squad/agents/Rai/charter.md | active |
| Fact Checker | Fact Checker | .squad/agents/fact-checker/charter.md | active |


## Coding Agent

<!-- copilot-auto-assign: true -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Coding Agent |

### Capabilities

**🟢 Good fit — auto-route when enabled:**
- Bug fixes with clear reproduction steps
- Test coverage (adding missing tests, fixing flaky tests)
- Lint/format fixes and code style cleanup
- Dependency updates and version bumps
- Small isolated features with clear specs
- Boilerplate/scaffolding generation
- Documentation fixes and README updates

Auto-assign is enabled in this repo: eligible issues keep their owning `squad:{member}` label and also get `squad:copilot` so GitHub assigns @copilot while the member label/charter stays authoritative for voice and boundaries.

**🟡 Needs review — route to @copilot but flag for squad member PR review:**
- Medium features with clear specs and acceptance criteria
- Refactoring with existing test coverage
- API endpoint additions following established patterns
- Migration scripts with well-defined schemas

**🔴 Not suitable — route to squad member instead:**
- Architecture decisions and system design
- Multi-system integration requiring coordination
- Ambiguous requirements needing clarification
- Security-critical changes (auth, encryption, access control)
- Performance-critical paths requiring benchmarking
- Changes requiring cross-team discussion

## Project Context

- **Project:** mlx-pep
- **Created:** 2026-08-11
- **Requested by:** @matthewcorven
- **Product:** Apple Silicon local-model profile toolkit for discovering, applying, generating, and sharing tuned JSONL profiles
- **Stack:** .NET 10, System.CommandLine, Terminal.Gui, ASP.NET Core minimal API, Azure Blob Storage, Python model-assessor, Hugging Face cache, oMLX
