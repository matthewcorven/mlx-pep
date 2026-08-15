#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MODEL_ID=""
BASE_URL="${OMLX_BASE_URL:-http://127.0.0.1:8000}"
API_KEY="${OMLX_API_KEY:-}"
PROFILES_JSON="$ROOT_DIR/config/benchmark_profiles.json"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --model-id)
      MODEL_ID="$2"
      shift 2
      ;;
    --base-url)
      BASE_URL="$2"
      shift 2
      ;;
    --api-key)
      API_KEY="$2"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [[ -z "$MODEL_ID" ]]; then
  echo "--model-id is required" >&2
  exit 1
fi

if [[ -z "$API_KEY" ]]; then
  echo "OMLX_API_KEY or --api-key is required" >&2
  exit 1
fi

PROFILE_IDS=()
while IFS= read -r profile_id; do
  PROFILE_IDS+=("$profile_id")
done < <(python3 - <<'PY' "$PROFILES_JSON"
import json, pathlib, sys
path = pathlib.Path(sys.argv[1])
doc = json.loads(path.read_text(encoding='utf-8'))
for item in doc["profiles"]:
    print(item["id"])
PY
)

for profile_id in "${PROFILE_IDS[@]}"; do
  echo "Running profile: $profile_id"
  python3 "$ROOT_DIR/scripts/next_phase/run_assessment.py" \
    --base-url "$BASE_URL" \
    --api-key "$API_KEY" \
    --model-id "$MODEL_ID" \
    --profile-id "$profile_id" \
    --suite single \
    --profiles-json "$PROFILES_JSON"
done
