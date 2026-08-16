# Quick Start: Run Your First Assessment (5 minutes)

**For:** Non-technical users, first-time users, anyone wanting to see mlx-pep in action
**Updated:** 2026-08-15

---

## Prerequisites Checklist

Before starting, verify you have:

- ✅ **Apple Silicon Mac** (M1 / M2 / M3 / M4) — **Required**
  - 16GB unified memory minimum
  - 8GB + swap acceptable for small models (~7B parameters)
- ✅ **macOS 13.4+** (Ventura, Sonoma, or Sequoia)
- ✅ **.NET 10.0 SDK**
  ```bash
  dotnet --version  # Should show 10.0.x or higher
  ```
- ✅ **oMLX server running**
  ```bash
  # In a separate terminal, check if oMLX is accessible:
  curl http://127.0.0.1:8000/api/version
  ```

**Missing something?** See [mlx-lm Development Environment Setup](mlx-lm-developer-setup.md) for detailed prerequisites.

---

## Step 1: Clone and Build (1 minute)

```bash
# Clone the repository
git clone https://github.com/matthewcorven/mlx-pep.git
cd mlx-pep

# Build the CLI
dotnet build src/MlxPep.Cli/MlxPep.Cli.csproj
```

---

## Step 2: Set Environment (1 minute)

mlx-pep needs to know where your oMLX server is:

```bash
# Set these in your terminal session or .env file
export OMLX_BASE_URL=http://127.0.0.1:8000
export OMLX_API_KEY=your-api-key-here
export HF_HUB_CACHE=~/.cache/huggingface/hub
```

**Note:** If these aren't set, mlx-pep will use fixture data (mock results) instead of real model profiling.

---

## Step 3: Run Your First Assessment (3 minutes)

Pick a lightweight model and run the **smoke suite** (fast, ~30 seconds):

```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- \
  assess mlx-community/Llama-2-7b-hf \
  --suite smoke
```

**What's happening:**
1. mlx-pep downloads (if needed) the model from Hugging Face
2. Runs quick performance benchmarks on your Mac
3. Saves three profiles: `high`, `balanced`, `efficient`
4. Stores results in `~/.mlx-pep/profiles/`

**Expected output:**
```
✓ Assessment complete
✓ Profiles saved to: ~/.mlx-pep/profiles/2026-08-15T10-30-00Z/mlx-community/Llama-2-7b-hf/
  - high.jsonl (optimized performance)
  - balanced.jsonl (default settings)
  - efficient.jsonl (low-latency, lower quality)
```

---

## Step 4: View Results (1 minute)

See what the assessment found:

```bash
# Show latest results for the model
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- \
  results show --model mlx-community/Llama-2-7b-hf
```

**Output example:**
```
Model: mlx-community/Llama-2-7b-hf
Run Date: 2026-08-15T10-30-00Z
Status: ✓ Complete

Profile Recommendations:
┌─────────┬──────────────────┬────────────┐
│ Profile │ Tokens/Second    │ Use Case   │
├─────────┼──────────────────┼────────────┤
│ High    │ 45.2 tokens/sec  │ Real-time  │
│ Balanced│ 38.1 tokens/sec  │ Default    │
│ Efficient│ 52.3 tokens/sec │ Latency    │
└─────────┴──────────────────┴────────────┘
```

---

## Step 5: Apply to VS Code (Optional)

Mirror a saved profile into VS Code Copilot settings:

```bash
# Dry-run (shows what would be applied, doesn't change anything)
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- \
  apply ~/.mlx-pep/profiles/<timestamp>/<model>/profiles.jsonl \
  --harness vscode \
  --dry-run
```

**Supported harnesses:**
- `vscode` — GitHub Copilot in VS Code
- `copilot-cli` — GitHub Copilot CLI
- `claude-code` — Claude Code
- `opencode` — OpenCode

---

## What's Next?

### Interactive Browse
Skip the CLI and use the terminal UI instead:
```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- tui
```

### Full Assessment
Ready for comprehensive results? Run the **full suite**:
```bash
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- \
  assess mlx-community/Llama-2-7b-hf \
  --suite full  # Takes ~5 minutes
```

### Try Different Models
```bash
# Lightweight models (fast, 5-10B parameters)
mlx-community/phi-3.5-mini-instruct
mlx-community/Mistral-7B

# Larger models (slower, 30B+ parameters)
mlx-community/NVIDIA-Nemotron-3.5-Lightning-30B-A3B-4bit
```

### Troubleshooting

**"oMLX server not found"**
- Verify oMLX is running: `curl http://127.0.0.1:8000/api/version`
- Check `OMLX_BASE_URL` is set correctly

**"Model not found"**
- Check Hugging Face for the model ID: https://huggingface.co/models
- Verify the ID is spelled exactly (case-sensitive)

**"Assessment uses fixture data"**
- Verify `OMLX_API_KEY` is set (even a test value)
- Check `HF_HUB_CACHE` points to a real Hugging Face cache directory

**Stuck or slow assessment?**
- The full suite takes ~5 minutes per model
- Smoke suite should complete in ~30 seconds
- If stalled >10 minutes, kill the process and check oMLX logs

---

## Next Reading

- [Architecture Overview](ARCHITECTURE.md) — understand how mlx-pep works under the hood
- [Feature Status & Roadmap](../README.md#feature-status) — what's planned next
- [FAQ](FAQ.md) — common questions answered
