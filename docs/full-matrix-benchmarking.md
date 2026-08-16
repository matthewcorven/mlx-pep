# Full Matrix Benchmarking Workflow

## Overview

This guide covers the **Hybrid approach** (Option 3) for full matrix benchmarking and production assessment. It uses deterministic topology routing to achieve:

- **Smoke Phase**: Analyze topology requirements once
- **Topology Manifest**: Human-editable file specifying instances and routes
- **Full Phase**: Run complete suite with deterministic multi-instance routing
- **Production Phase**: Deploy with reproducible topology

---

## Workflow Architecture

### Phase 1: Smoke Benchmark (Topology Analysis)

Run a minimal smoke suite to analyze multi-instance topology needs:

```bash
mlx-pep assess <model-hf-id> --suite smoke
```

**Output:**
- Smoke benchmark results in `results/mlx-pep-cli/{operationId}/`
- Auto-generated single-instance topology manifest
- Recommendations for optimal instance configuration

**Example Output:**
```
✓ Smoke benchmark complete
  - Processed 8 test cases
  - Identified hardware profile: "balanced"
  - Recommended instances: vllm (4 replicas), ollama (2 replicas)
  - Estimated throughput: 45-50 req/sec per instance
```

---

### Phase 2: Create Topology Manifest

Create a human-editable topology manifest specifying:
- Which instances to launch
- Route patterns for each instance
- Load balancing strategy
- Timeout and retry settings

**Location:** Create at any path (e.g., `topology-manifests/production.json`)

**Example Topology Manifest:**

```json
{
  "version": "1.0",
  "mode": "multi-instance",
  "instances": [
    {
      "id": "vllm-0",
      "type": "vllm",
      "url": "http://localhost:8000",
      "capacity": {
        "concurrent_requests": 32,
        "max_batch_size": 256,
        "target_qps": 50
      },
      "health_check": {
        "enabled": true,
        "interval_seconds": 30,
        "timeout_seconds": 5
      }
    },
    {
      "id": "vllm-1",
      "type": "vllm",
      "url": "http://localhost:8001",
      "capacity": {
        "concurrent_requests": 32,
        "max_batch_size": 256,
        "target_qps": 50
      },
      "health_check": {
        "enabled": true,
        "interval_seconds": 30,
        "timeout_seconds": 5
      }
    },
    {
      "id": "ollama-0",
      "type": "ollama",
      "url": "http://localhost:11434",
      "capacity": {
        "concurrent_requests": 8,
        "max_batch_size": 64,
        "target_qps": 10
      },
      "health_check": {
        "enabled": true,
        "interval_seconds": 30,
        "timeout_seconds": 5
      }
    }
  ],
  "routes": [
    {
      "name": "vllm-round-robin",
      "instances": ["vllm-0", "vllm-1"],
      "strategy": "round-robin",
      "timeout_seconds": 30,
      "retry_on_failure": true,
      "max_retries": 2
    },
    {
      "name": "ollama-fallback",
      "instances": ["ollama-0"],
      "strategy": "round-robin",
      "timeout_seconds": 60,
      "retry_on_failure": false
    }
  ],
  "routing_policy": {
    "default_route": "vllm-round-robin",
    "fallback_route": "ollama-fallback",
    "circuit_breaker": {
      "failure_threshold": 5,
      "reset_timeout_seconds": 60
    }
  }
}
```

**Schema Reference:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `version` | string | ✓ | Manifest format version (e.g., "1.0") |
| `mode` | string | ✓ | "single-instance" or "multi-instance" |
| `instances[].id` | string | ✓ | Unique instance identifier |
| `instances[].type` | string | ✓ | vllm, ollama, or custom |
| `instances[].url` | string | ✓ | Base URL for API calls |
| `instances[].capacity.concurrent_requests` | number | ✓ | Max concurrent requests |
| `instances[].capacity.max_batch_size` | number | ✓ | Max batch size for optimization |
| `instances[].capacity.target_qps` | number | ✓ | Target requests per second |
| `instances[].health_check.enabled` | boolean | ✓ | Enable health monitoring |
| `instances[].health_check.interval_seconds` | number | ✓ | Check interval in seconds |
| `routes[].name` | string | ✓ | Unique route identifier |
| `routes[].instances` | array | ✓ | List of instance IDs in this route |
| `routes[].strategy` | string | ✓ | "round-robin", "least-loaded", or "random" |
| `routes[].timeout_seconds` | number | ✓ | Request timeout |
| `routes[].retry_on_failure` | boolean | ✓ | Retry failed requests |
| `routes[].max_retries` | number | ✓ | Max retry attempts |

---

### Phase 3: Full Benchmark with Topology Manifest

Run the full benchmark suite with deterministic routing:

```bash
# Option A: Specify topology manifest path (this implementation)
mlx-pep assess <model-hf-id> --suite full --topology-manifest path/to/topology.json

# This will:
# 1. Validate the topology manifest file exists
# 2. Pass it to the Python model-assessor
# 3. Route all benchmark requests through specified instances
# 4. Generate comprehensive results
```

**Example Command:**

```bash
mlx-pep assess meta-llama/Llama-2-70b-hf \
  --suite full \
  --topology-manifest ./topology-manifests/production.json
```

**Expected Output:**

```
✓ Full benchmark starting with topology manifest
  - Using instances: vllm-0, vllm-1, ollama-0
  - Load strategy: round-robin with fallback
  - Running 256 test cases...

✓ Full benchmark complete
  - Total requests: 256
  - Total latency: 1024.5s
  - Average latency per request: 4.0s
  - P99 latency: 8.2s
  - Throughput: 42 req/sec (aggregate)
  - Instance breakdown:
    - vllm-0: 85 requests, avg 3.8s
    - vllm-1: 87 requests, avg 3.9s
    - ollama-0: 42 requests (fallback), avg 6.1s
```

---

## Step-by-Step Workflow

### For Development/Testing

```bash
# 1. Analyze topology with smoke suite
mlx-pep assess meta-llama/Llama-2-7b \
  --suite smoke

# Output: topology analysis
# Review results to decide on instance configuration

# 2. Create topology manifest based on findings
# (Edit topology-manifests/dev.json)

# 3. Launch dev instances (example)
# Terminal 1:
docker run -p 8000:8000 vllm/vllm:latest \
  --model meta-llama/Llama-2-7b

# Terminal 2:
ollama serve

# 4. Run full benchmark with topology
mlx-pep assess meta-llama/Llama-2-7b \
  --suite full \
  --topology-manifest ./topology-manifests/dev.json

# 5. Review results in results/mlx-pep-cli/
```

---

### For Production Assessment

```bash
# 1. Pre-compute optimal topology (one-time)
mlx-pep assess meta-llama/Llama-2-70b \
  --suite smoke

# 2. Create production topology manifest
# (Based on smoke results + operational constraints)
# Save to: topology-manifests/prod-llama-70b.json

# 3. Create identical instance topology in production
# Deploy vllm replicas on GPU nodes
# Deploy ollama replicas on CPU nodes

# 4. Run production assessment (deterministic)
mlx-pep assess meta-llama/Llama-2-70b \
  --suite full \
  --topology-manifest ./topology-manifests/prod-llama-70b.json

# 5. This produces:
# - results/mlx-pep-cli/{operationId}/recommendations/
#   Contains profiles tailored to your exact topology
# - results/mlx-pep-cli/{operationId}/client-configs/
#   Contains router config for load balancing

# 6. Deploy with reproducible results
# Apply profiles and client configs to production
```

---

## Topology Manifest Management

### Creating from Scratch

1. **Start with smoke results:**
   ```bash
   mlx-pep assess <model> --suite smoke
   # Review output topology recommendations
   ```

2. **Create manifest template:**
   ```bash
   cp docs/topology-manifests/template.json \
      topology-manifests/mymodel-prod.json
   ```

3. **Edit manifest:**
   - Set `instances`: List all running services
   - Set `routes`: Define routing policies
   - Tune `capacity` based on load testing

4. **Validate manifest:**
   ```bash
   # Run with --topology-manifest to validate
   mlx-pep assess <model> --suite smoke \
     --topology-manifest ./topology-manifests/mymodel-prod.json
   ```

### Versioning Topologies

Store topology manifests in git with versioning:

```
topology-manifests/
├── template.json                    # Template for new manifests
├── prod-llama-7b-v1.json           # Production config (v1)
├── prod-llama-7b-v2.json           # Production config (v2, optimized)
├── staging-llama-13b.json          # Staging environment
└── dev-mixed-model.json            # Development multi-model
```

**Workflow with Git:**
```bash
# 1. Create new version
cp prod-llama-7b-v1.json prod-llama-7b-v2.json

# 2. Edit and test
mlx-pep assess meta-llama/Llama-2-7b \
  --suite full \
  --topology-manifest ./topology-manifests/prod-llama-7b-v2.json

# 3. Commit if better results
git add topology-manifests/prod-llama-7b-v2.json
git commit -m "Topology: Add vllm-2 instance for load balancing"
```

---

## CLI Reference

### assess Command with Topology

```bash
mlx-pep assess <hf-id> [OPTIONS]
```

**Options:**
- `<hf-id>` — Hugging Face model ID (required)
- `--assistant-model-id X` — Assistant model for MTP mode
- `--suite smoke|full` — Benchmark suite (default: full)
- `--publish` — Publish results to service
- `--topology-manifest PATH` — Path to topology manifest JSON

**Examples:**

```bash
# Single instance (default)
mlx-pep assess meta-llama/Llama-2-7b --suite full

# Multi-instance with topology
mlx-pep assess meta-llama/Llama-2-7b \
  --suite full \
  --topology-manifest ./prod-topology.json

# With MTP and topology
mlx-pep assess meta-llama/Llama-2-70b \
  --suite full \
  --assistant-model-id meta-llama/Llama-2-7b \
  --topology-manifest ./prod-topology.json

# Smoke analysis only
mlx-pep assess meta-llama/Llama-2-70b --suite smoke
```

---

## Troubleshooting

### Topology Manifest Not Found

```
Error: topology manifest file not found: ./prod-topology.json
```

**Fix:**
- Check file path is absolute or relative to current directory
- Verify file exists: `ls -la ./prod-topology.json`

### Instance Connection Failed

```
[ProfilingRunner] Instance 'vllm-0' at http://localhost:8000 failed health check
```

**Fix:**
1. Verify instances are running: `curl http://localhost:8000/health`
2. Check firewall/networking
3. Verify URLs in topology manifest are correct
4. Check instance logs for errors

### Validation Errors in Manifest

```
Invalid topology manifest: missing required field 'capacity.concurrent_requests'
```

**Fix:**
- Refer to schema reference above
- All required fields must be present
- Use template.json as starting point

### Results Cached from Previous Run

If rerunning with same model/suite, results may be cached:

```bash
# Force new run by using --topology-manifest flag
# (triggers new operation ID)
mlx-pep assess <model> --suite full \
  --topology-manifest ./topology.json
```

---

## Best Practices

### 1. Version Control Topologies
```bash
# Store in git with meaningful names
topology-manifests/prod-llama-70b-v{N}.json
```

### 2. Document Instance Specs
Add comments to topology manifest:
```json
{
  "instances": [
    {
      "id": "vllm-0",
      "_comment": "GPU node: 4x A100, 512GB RAM, CUDA 12.1",
      "type": "vllm",
      ...
    }
  ]
}
```

### 3. Establish Baseline
```bash
# Create canonical topology for comparison
mlx-pep assess <model> --suite full \
  --topology-manifest ./topology-manifests/baseline.json
```

### 4. Progressive Optimization
```bash
# Smoke → Analyze → v1 → v2 → Production
mlx-pep assess <model> --suite smoke              # Analyze
mlx-pep assess <model> --suite full \
  --topology-manifest ./v1.json                   # Test v1
mlx-pep assess <model> --suite full \
  --topology-manifest ./v2.json                   # Test v2
# Compare results, commit winner
```

### 5. CI/CD Integration
```yaml
# .github/workflows/benchmark.yml
- name: Full Matrix Benchmark
  run: |
    mlx-pep assess ${{ env.MODEL_ID }} \
      --suite full \
      --topology-manifest ./topology-manifests/prod.json
```

---

## Next Steps

1. **Create your first topology manifest** using the template
2. **Run smoke suite** to analyze requirements
3. **Launch multi-instance topology** in dev
4. **Run full benchmark** with `--topology-manifest` flag
5. **Compare results** against single-instance baseline
6. **Deploy to production** with reproducible topology

For more details, see:
- [Profile Schema](profile-schema.md)
- [Service Deployment](service-deployment.md)
- [Implementation Guide](implementation-guide-mlx-lm.md)
