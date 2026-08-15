# Change Request

The synthetic repo needs a new `project_slug` field in summary responses.

## Constraints

- Preserve the existing `project_id`, `owner`, `note_count`, and `sync_enabled` fields.
- `project_slug` should be lowercase and replace spaces with `-`.
- Keep the change minimal and avoid unrelated refactors.
- Add validation guidance covering both archived and non-archived projects.
