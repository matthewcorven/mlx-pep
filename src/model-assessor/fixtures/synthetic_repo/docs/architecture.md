# Synthetic Repo Architecture

## Entry Path

The request flow starts in `app/router.py`. `dispatch_request` decides whether sync should run by calling `should_enable_sync` from `app/service.py`. Summary responses also delegate to `summarize_project` in the same service module.

## Rules

- Sync is enabled only when the project has the `enable-sync` tag.
- Archived projects always skip sync.
- Summary responses include owner, project ID, sync state, and note count.

## Reporting Guidance

Operator-facing writeups should name the entry file, the service helper that enforces the rule, and the archived-project caveat.
