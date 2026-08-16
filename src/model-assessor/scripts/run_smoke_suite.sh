#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
MODEL_ID=""
ASSISTANT_MODEL_ID=""
BASE_URL="${OMLX_BASE_URL:-http://127.0.0.1:8000}"
API_KEY="${OMLX_API_KEY:-}"
SMOKE_JSON="$ROOT_DIR/config/smoke_suite.json"
PROFILES_JSON="$ROOT_DIR/config/benchmark_profiles.json"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --model-id)
      MODEL_ID="$2"
      shift 2
      ;;
    --assistant-model-id)
      ASSISTANT_MODEL_ID="$2"
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

read_profile_ids() {
  local doc_path="$1"
  python3 - "$doc_path" <<'PY'
import json, pathlib, sys
path = pathlib.Path(sys.argv[1])
doc = json.loads(path.read_text(encoding='utf-8'))
for item in doc.get("profiles", []):
    if isinstance(item, dict):
        profile_id = item.get("id")
        if profile_id:
            print(profile_id)
PY
}

PROFILE_IDS=()
while IFS= read -r profile_id; do
  PROFILE_IDS+=("$profile_id")
done < <(read_profile_ids "$SMOKE_JSON")

if [[ ${#PROFILE_IDS[@]} -eq 0 ]]; then
  echo "No smoke profiles found in $SMOKE_JSON" >&2
  exit 1
fi

for profile_id in "${PROFILE_IDS[@]}"; do
  echo "Phase 1/3: benchmark/probe collection for smoke profile: $profile_id"
  python3 "$ROOT_DIR/scripts/next_phase/run_assessment.py" \
    --base-url "$BASE_URL" \
    --api-key "$API_KEY" \
    --model-id "$MODEL_ID" \
    --assistant-model-id "${ASSISTANT_MODEL_ID}" \
    --profile-id "$profile_id" \
    --suite single \
    --profiles-json "$PROFILES_JSON"

  echo "Phase 2/3: prompt-quality evaluation for smoke profile: $profile_id"
  python3 "$ROOT_DIR/scripts/next_phase/run_prompt_evals.py" \
    --base-url "$BASE_URL" \
    --api-key "$API_KEY" \
    --model-id "$MODEL_ID" \
    --assistant-model-id "${ASSISTANT_MODEL_ID}" \
    --profile-id "$profile_id"
done

echo "Phase 3/3: generate recommendation report after all smoke evidence is complete"
python3 "$ROOT_DIR/scripts/next_phase/generate_recommendation_report.py" \
  --model-id "$MODEL_ID" \
  ${ASSISTANT_MODEL_ID:+--assistant-model-id "$ASSISTANT_MODEL_ID"}

LATEST_MANIFEST="$(ls -dt "$ROOT_DIR"/results/recommendations/*/recommendation_manifest.json 2>/dev/null | head -n 1)"
if [[ -z "$LATEST_MANIFEST" ]]; then
  echo "Recommendation report generation did not emit a recommendation manifest" >&2
  exit 1
fi

echo "Phase 4: generate client config artifacts for the latest recommendation manifest"
python3 "$ROOT_DIR/scripts/next_phase/generate_client_config_artifacts.py" \
  --recommendation-manifest "$LATEST_MANIFEST"
