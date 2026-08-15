# oMLX API Validation

This document records the live-validated local API behavior needed for automation.

## Validation Summary

Validated against a live local oMLX instance at `http://127.0.0.1:8000` with version metadata exposed by `/openapi.json` as `0.4.4.dev1`.

## Auth Model

### Public API

Use a bearer token:

```bash
curl -H "Authorization: Bearer $OMLX_API_KEY" \
  http://127.0.0.1:8000/v1/models
```

### Admin API

Admin endpoints require a login request that returns a session cookie.

```bash
curl -c cookies.txt \
  -H 'Content-Type: application/json' \
  -d '{"api_key":"'$OMLX_API_KEY'"}' \
  http://127.0.0.1:8000/admin/api/login
```

Then reuse the cookie:

```bash
curl -b cookies.txt http://127.0.0.1:8000/admin/api/models
```

Bearer auth alone was not sufficient for the admin API.

## Working Endpoints

| Endpoint | Method | Purpose |
| --- | --- | --- |
| `/openapi.json` | `GET` | discover API version and schema |
| `/docs` | `GET` | Swagger UI |
| `/v1/models` | `GET` | list public model inventory |
| `/admin/api/login` | `POST` | establish admin session cookie |
| `/admin/api/models` | `GET` | list models plus current settings and compatibility flags |
| `/admin/api/models/{model_id}/generation_config` | `GET` | read model-config defaults |
| `/admin/api/profile-fields` | `GET` | discover supported settings fields |
| `/admin/api/models/{model_id}/settings` | `PUT` | update current model settings |
| `/admin/api/bench/start` | `POST` | launch a benchmark run |
| `/admin/api/bench/active` | `GET` | inspect current benchmark status |
| `/admin/api/bench/{bench_id}/stream` | `GET` | stream benchmark progress via SSE |
| `/admin/api/bench/{bench_id}/results` | `GET` | fetch final benchmark results |
| `/admin/api/bench/{bench_id}/cancel` | `POST` | cancel a running benchmark |

## Verified Request Shapes

### Update per-model settings

```json
{
  "max_context_window": 32768,
  "max_tokens": 1536,
  "temperature": 0.3,
  "top_p": 0.95,
  "top_k": 64,
  "min_p": 0.0,
  "force_sampling": true,
  "mtp_enabled": false,
  "vlm_mtp_enabled": false
}
```

Note: sparse `PUT` semantics were not proven. The safest automation path is read, merge, then full-body `PUT`.

### Start benchmark

```json
{
  "model_id": "gemma-4-12B-it-bf16",
  "prompt_lengths": [1024, 4096],
  "generation_length": 128,
  "batch_sizes": [2, 4]
}
```

## SSE Event Types Observed

- `progress`
- `result`
- `done`
- `upload_skipped`
- `error`
- `keepalive`

Automation should consume the stream but still fetch `/results` afterward for stable persistence.

## Important Live Findings

1. The current Gemma 4 12B model exposed `vlm_mtp_enabled` in its settings.
2. Experimental speculative features can cause benchmark uploads to be marked as skipped.
3. The assistant model was visible in model listings but a benchmark against it failed because that model type was not benchmark-supported.
4. `generation_config` defaults can differ from the currently active admin settings.

## Known Gaps

- Positive `bench/active` payload while a benchmark is still running was not captured.
- The exact server behavior for sparse settings `PUT` was not validated.
- Batch benchmark result rows were not captured in every variant, even though the API accepted `batch_sizes`.
- Assistant-model benchmarking is not currently supported for the validated Gemma assistant checkpoint.
