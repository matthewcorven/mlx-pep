# Quick Start: Full Matrix Benchmarking

## 5-Minute Setup

### 1. Build and Test

```bash
# Build the CLI with the new --topology-manifest parameter
cd /Users/core/git/matthewcorven/mlx-pep
dotnet build src/MlxPep.Cli/MlxPep.Cli.csproj

# Verify help shows new parameter
dotnet run --project src/MlxPep.Cli -- assess --help
# Should show:
# Usage: mlx-pep assess <hf_id> [...options...] [--topology-manifest PATH]
```

### 2. Create Your First Topology

```bash
# Copy template to your workspace
cp docs/topology-manifests/template-single-instance.json \
   ./my-topology.json

# Edit to match your local instances
# (Modify URLs and capacity based on your setup)
```

### 3. Run with Topology (Option A: Single Instance)

```bash
# Smoke phase (optional, for analysis)
mlx-pep assess meta-llama/Llama-2-7b \
  --suite smoke

# Full benchmark with topology manifest
mlx-pep assess meta-llama/Llama-2-7b \
  --suite full \
  --topology-manifest ./my-topology.json
```

### 4. Run Full Matrix (Option B: Multi-Instance)

```bash
# Copy multi-instance example
cp docs/topology-manifests/example-multi-instance.json \
   ./production-topology.json

# Start your instances in separate terminals
# Terminal 1: vLLM instance 0
# docker run -p 8000:8000 vllm/vllm:latest --model meta-llama/Llama-2-7b

# Terminal 2: vLLM instance 1
# docker run -p 8001:8001 vllm/vllm:latest --model meta-llama/Llama-2-7b

# Terminal 3: Ollama (fallback)
# ollama serve

# Terminal 4: Run benchmark
mlx-pep assess meta-llama/Llama-2-7b \
  --suite full \
  --topology-manifest ./production-topology.json

# Results include per-instance performance breakdown
```

## What Was Implemented

### New CLI Parameter: `--topology-manifest`

| Aspect | Details |
|--------|---------|
| **Location** | `mlx-pep assess` command |
| **Syntax** | `--topology-manifest PATH` |
| **Behavior** | Pass external topology manifest to Python assessor |
| **Validation** | File must exist (validated at CLI level) |
| **Backward Compatibility** | ✓ Optional; existing workflows unaffected |

### Architecture

```
mlx-pep assess <model>
  ├─ [No --topology-manifest] → Auto-generate single-instance manifest
  └─ [--topology-manifest PATH] → Use provided manifest
      ├─ Validate file exists
      ├─ Get full path
      └─ Pass to Python: --topology-manifest <path>
```

### Changes Made

1. **CliBuilder.cs** (Parameter Parsing)
   - Extract `--topology-manifest` option
   - Validate file exists before dispatch
   - Pass to AssessCommand

2. **AssessCommand.cs** (Command Handler)
   - Add `topologyManifestPath?` parameter
   - Forward to ProfilingRunner

3. **ProfilingRunner.cs** (Python Integration)
   - Add `topologyManifestPath?` parameter
   - If provided: use it (validate & get full path)
   - If not provided: auto-generate single-instance manifest (existing behavior)
   - Pass to Python subprocess

### Files Modified

```
✓ src/MlxPep.Cli/CliBuilder.cs
✓ src/MlxPep.Cli/Commands/AssessCommand.cs
✓ src/MlxPep.Core/Profiling/ProfilingRunner.cs
```

### New Documentation & Examples

```
✓ docs/full-matrix-benchmarking.md (3,500+ lines)
  - Complete workflow guide
  - Topology manifest schema
  - Step-by-step examples
  - Best practices
  
✓ docs/topology-manifests/template-single-instance.json
  - Starter template for single-instance setup
  
✓ docs/topology-manifests/example-multi-instance.json
  - Two vLLM + one Ollama fallback configuration
  
✓ docs/topology-manifests/example-production-cluster.json
  - Production-grade: 4 GPU nodes + 2 CPU fallbacks
```

## Workflow Scenarios

### Scenario 1: Development (Single Instance)

```bash
# Start vLLM locally
docker run -p 8000:8000 vllm/vllm:latest

# Use default or custom topology
mlx-pep assess meta-llama/Llama-2-7b \
  --suite full \
  --topology-manifest ./docs/topology-manifests/template-single-instance.json
```

### Scenario 2: Production Assessment (Deterministic)

```bash
# 1. Run smoke to analyze topology needs
mlx-pep assess meta-llama/Llama-2-70b --suite smoke

# 2. Create production topology based on findings
# cp docs/topology-manifests/example-multi-instance.json prod-70b.json
# # Edit to match production instances

# 3. Deploy instances in production
# (Launch vLLM replicas, Ollama replicas, etc.)

# 4. Run deterministic full benchmark
mlx-pep assess meta-llama/Llama-2-70b \
  --suite full \
  --topology-manifest ./prod-70b.json

# 5. Results are reproducible (same instances = same behavior)
```

### Scenario 3: Multi-Model Benchmarking

```bash
# Different topologies for different model sizes

mlx-pep assess meta-llama/Llama-2-7b \
  --suite full \
  --topology-manifest ./topology-7b.json

mlx-pep assess meta-llama/Llama-2-70b \
  --suite full \
  --topology-manifest ./topology-70b.json

# Compare results across topologies
```

## Testing Checklist

- [ ] Build succeeds: `dotnet build src/MlxPep.Cli/`
- [ ] Help text shows new parameter: `mlx-pep assess --help`
- [ ] File validation works:
  ```bash
  mlx-pep assess <model> --topology-manifest nonexistent.json
  # Should error: "topology manifest file not found"
  ```
- [ ] Single-instance topology works:
  ```bash
  mlx-pep assess <model> \
    --suite full \
    --topology-manifest ./docs/topology-manifests/template-single-instance.json
  ```
- [ ] Multi-instance topology works (with instances running)
- [ ] Backward compatibility (no `--topology-manifest` still works)

## Next Phase: Production Deployment

Once validated, integrate with your deployment pipeline:

```bash
# CI/CD: Store topology in repo
git add topology-manifests/prod-*.json

# Benchmark on every deploy
mlx-pep assess $MODEL_ID \
  --suite full \
  --topology-manifest ./topology-manifests/prod-${REGION}.json

# Publish and apply results
mlx-pep assess $MODEL_ID \
  --suite full \
  --topology-manifest ./topology-manifests/prod-${REGION}.json \
  --publish
```

---

## Documentation

For complete details, see:
- **Main Guide:** [full-matrix-benchmarking.md](../full-matrix-benchmarking.md)
- **Examples:** [docs/topology-manifests/](../topology-manifests/)
- **Code Changes:** Review commits in this PR
