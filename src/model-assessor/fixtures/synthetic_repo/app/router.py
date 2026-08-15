from app.service import summarize_project, should_enable_sync


def dispatch_request(mode: str, project: dict) -> dict:
    if should_enable_sync(project):
        sync_state = "sync-enabled"
    else:
        sync_state = "sync-skipped"

    if mode == "summary":
        return {
            "kind": "summary",
            "sync_state": sync_state,
            "payload": summarize_project(project),
        }

    return {
        "kind": "passthrough",
        "sync_state": sync_state,
        "payload": project,
    }
