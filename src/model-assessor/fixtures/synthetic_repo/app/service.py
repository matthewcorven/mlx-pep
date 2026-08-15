from data.project_notes import PROJECT_NOTES


SYNC_TAG = "enable-sync"


def should_enable_sync(project: dict) -> bool:
    return SYNC_TAG in project.get("tags", []) and not project.get("archived", False)


def summarize_project(project: dict) -> dict:
    return {
        "project_id": project["id"],
        "owner": project["owner"],
        "note_count": len(PROJECT_NOTES.get(project["id"], [])),
        "sync_enabled": should_enable_sync(project),
    }
