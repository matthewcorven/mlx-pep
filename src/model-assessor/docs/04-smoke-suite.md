# Smoke Suite

The smoke suite is a reduced 4-profile subset of the full matrix. Its purpose is to validate the harness, auth path, setting application, result persistence, and the most important short-vs-long and MTP-off-vs-on comparisons before a full run.

## Smoke-Suite Profiles

| Profile ID | Purpose |
| --- | --- |
| `short_code_research_tools_mtp_off` | baseline short tool-using workload |
| `short_coding_mtp_off` | deterministic short coding workload |
| `long_coding_mtp_off` | long coding baseline for long-prefill behavior |
| `long_coding_mtp_on` | direct long-coding MTP ablation |

## Why These Four

- They validate both tool-using and coding-oriented workloads.
- They include both short and long prompt regimes.
- They include the smallest useful MTP ablation pair.
- They keep the first automation cycle short enough for debugging.

## Exit Criteria For Advancing To Full Matrix

Run the full matrix only if the smoke suite shows all of the following:

1. login and admin session creation work without manual intervention
2. settings are applied deterministically per profile
3. benchmark start, stream, and results retrieval all complete successfully
4. results are persisted in a stable output directory structure
5. the MTP ablation pair yields plausible differential results rather than obvious harness or state leakage

## Failure Handling Guidance

If the smoke suite fails:

- verify the current model exists in `/admin/api/models`
- verify speculative flags supported by the selected model
- verify admin cookie auth is still accepted by the local server
- rerun a single profile with `mtp_off` before retrying the whole suite
