# Frequently Asked Questions

**Updated:** 2026-08-15

---

## Assessment & Profiling

### What's the difference between smoke and full suite?

**Smoke Suite** (~30 seconds)
- Quick sanity check
- 3-5 representative prompts
- 1 concurrency level
- Minimal quantization options
- Best for: first-time checks, CI/CD gates, rapid iteration

**Full Suite** (~5 minutes)
- Comprehensive benchmarking
- Complete prompt matrix (dozens of variations)
- 4+ concurrency levels (1, 2, 4, 8 threads)
- All quantization + optimization combos
- Best for: final tuning, production decisions, detailed profiles

### Why do I see three profiles (high, balanced, efficient)?

mlx-pep generates three profiles targeting different use cases:

| Profile | Priority | Tokens/Sec | Use Case |
|---------|----------|-----------|----------|
| **High** | Performance | Max possible | Real-time chat, low-latency endpoints |
| **Balanced** | Default | Sweet spot | General-purpose (recommended) |
| **Efficient** | Latency | Slower but stable | Batch processing, async workflows |

Each profile has different settings (quantization level, thread count, batch size). You choose which to apply to your harness.

### Can I run multiple models at once?

Not yet. mlx-pep assesses one model per run. **Fast-follow feature:**
- Batch assessment mode: `mlx-pep batch --models model1,model2,model3`

For now, run assessments sequentially:
```bash
mlx-pep assess model1 --suite smoke && \
mlx-pep assess model2 --suite smoke && \
mlx-pep assess model3 --suite smoke
```

### What if my oMLX server crashes mid-assessment?

The assessment is aborted and no profiles are saved. **Restart oMLX** and run again:

```bash
# Restart oMLX (depends on your setup)
# Example with docker:
docker restart omlx-server

# Re-run the assessment (starts fresh)
mlx-pep assess <model> --suite smoke
```

Completed runs are not affected — they're already saved in `~/.mlx-pep/profiles/`.

---

## Platform Support

### Can I run mlx-pep on Linux or Windows?

**Current Status:**
- ✅ macOS (Apple Silicon) — fully supported
- ⚠️ Linux — technically possible (.NET 10 runs on Linux) but **untested**
- ❌ Windows — not supported (Apple Silicon benchmarks don't apply)
- ❌ Intel Macs — benchmarks are Apple Silicon-specific

**Roadmap:** ARM64 Linux support planned for Q4 2026.

### Why only Apple Silicon?

mlx-pep profiles are optimized for MLX (a framework for Apple Silicon). Profiles wouldn't apply to Intel CPUs or Linux hardware. The benchmarks measure Apple Silicon-specific metrics (Neural Engine, unified memory).

### Can I use this on my M1 MacBook Air with 8GB memory?

**Short answer:** Yes, but with limitations.

- Smoke suite: ✅ Works
- Full suite: ⚠️ May swap to disk (slower)
- Large models (30B+): ❌ Won't fit in memory

**Workaround:** Use smaller models (7B, 13B) or increase swap space.

---

## Profiles & Harness Integration

### What happens when I apply a profile?

`mlx-pep apply` reads your saved profile and generates **harness-specific configuration**:

| Harness | Changes | Example |
|---------|---------|---------|
| VS Code | `.vscode/settings.json` + keybindings | Model ID, inference args, token limits |
| Copilot CLI | Config file (~/.copilot/config.json) | Model settings, temperature, top-p |
| Claude Code | Environment variables + system prompt | Model ID, API endpoint |
| OpenCode | Extension settings | Quantization level, batch size |

**Important:** Profiles are *recommendations*, not magical configuration. You review them in dry-run mode first.

### Can I mix profiles from different models?

Not recommended. Each profile is tuned for its specific model. Mixing could cause:
- Token/sec estimates to be wrong
- Memory usage to exceed limits
- Quality degradation

If you need multi-model support, contact the team or file an issue.

### What if my harness doesn't support a profile setting?

Apply in **dry-run mode** first to see what would be applied:

```bash
mlx-pep apply profiles.jsonl --harness vscode --dry-run
```

The output shows which settings are supported and which require manual configuration.

---

## Publishing & Sharing Profiles

### Can I publish a profile for others to use?

**Fast-follow feature** — not yet supported. Future workflow:

```bash
mlx-pep publish ~/.mlx-pep/profiles/<timestamp>/<model>/ \
  --namespace community \
  --description "Llama 2 7B on M3 MacBook Pro"
```

This would upload to a public profile registry so others can:
```bash
mlx-pep apply community:llama2-7b-m3 --harness vscode
```

### How do I back up my profiles?

Profiles are saved to `~/.mlx-pep/profiles/`. Back it up like any directory:

```bash
# Copy to external drive
cp -r ~/.mlx-pep ~/Backups/mlx-pep-profiles

# Or tar + compress
tar -czf ~/mlx-pep-backup-2026-08-15.tar.gz ~/.mlx-pep
```

---

## Troubleshooting

### "oMLX server not found"

**Cause:** oMLX isn't running or the URL is wrong.

**Fix:**
```bash
# Verify server is responding
curl http://127.0.0.1:8000/api/version

# Check your OMLX_BASE_URL
echo $OMLX_BASE_URL

# If wrong, set it correctly
export OMLX_BASE_URL=http://<your-server>:8000
```

### "Model not found" / "Model download failed"

**Cause:** Model ID is wrong or Hugging Face is unreachable.

**Fix:**
1. **Check the spelling** — model IDs are case-sensitive
   ```bash
   # Correct: mlx-community/Llama-2-7b-hf
   # Wrong:  mlx-community/llama-2-7b-hf (wrong case)
   ```

2. **Verify on Hugging Face** — https://huggingface.co/models
   - Search for the model
   - Copy the exact model ID

3. **Check internet connection** — Hugging Face needs to be reachable

### "Assessment uses fixture data instead of real benchmarks"

**Cause:** oMLX server isn't accessible or API key is missing.

**Why it matters:** Fixture data is fake (for testing). Real benchmarks require a running oMLX server.

**Fix:**
```bash
# Set credentials
export OMLX_BASE_URL=http://127.0.0.1:8000
export OMLX_API_KEY=your-api-key

# Verify
curl -H "Authorization: Bearer $OMLX_API_KEY" \
  http://127.0.0.1:8000/api/version
```

### Assessment is very slow / hangs

**Common causes:**

| Symptom | Cause | Fix |
|---------|-------|-----|
| Slow start (>1 min to begin) | Model is downloading from HF | Wait or download separately: `mlx-pep models get <model>` |
| Slow during run | oMLX is busy loading model into GPU | Wait, or reduce concurrency in settings |
| Hangs after progress | Python subprocess crashed | Check `~/.mlx-pep/logs/` for error messages |
| Times out after 10 min | Full suite on large model (30B+) | Try `--suite smoke` or smaller model |

### "Profile validation failed"

**Cause:** Profile JSONL is corrupted or malformed.

**Fix:**
1. **Verify it's valid JSONL** — each line must be valid JSON
   ```bash
   head -1 profiles.jsonl | python3 -m json.tool
   ```

2. **Regenerate the profile**
   ```bash
   mlx-pep assess <model> --suite smoke
   ```

3. **Check disk space** — if assessment was interrupted, files may be truncated

---

## Performance & Optimization

### How can I make assessments run faster?

1. **Use smoke suite instead of full**
   ```bash
   mlx-pep assess <model> --suite smoke  # 30 sec
   ```

2. **Use a smaller model** — 7B is faster than 30B
   ```bash
   mlx-pep models list  # See available models
   ```

3. **Reduce quantization options** — fewer = faster (requires code change, fast-follow feature)

### Why is token/sec lower than I expected?

Common reasons:

1. **Model is quantized** — lower precision = faster (higher throughput) but lower quality
2. **GPU memory is full** — sharing with other apps or background processes
3. **Thermal throttling** — Mac cooled down to avoid overheating
4. **Full suite vs smoke** — full suite tests worst-case scenarios (slower)

**Check:** `mlx-pep results show --model <model>` and compare against published benchmarks.

### Can I run assessments in the background?

**Not yet.** mlx-pep is a foreground CLI tool. **Fast-follow:**

```bash
# Future: background mode with progress file
mlx-pep assess <model> --suite full --background \
  --progress ~/.mlx-pep/progress.txt
```

For now, use `nohup` in a separate terminal:
```bash
nohup dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- \
  assess <model> --suite full > assessment.log 2>&1 &
```

---

## Contributing & Development

### Where do I report bugs?

Open an issue on GitHub: https://github.com/matthewcorven/mlx-pep/issues

**Include:**
- Error message (full output)
- Model ID you were assessing
- Mac hardware (M1/M2/M3/M4 + memory)
- mlx-pep version: `mlx-pep --version`

### How do I contribute a new harness?

See [ARCHITECTURE.md → Extension Points](ARCHITECTURE.md#extension-points).

**Quick summary:**
1. Create a new `XyzHarnessApplier` class in `src/MlxPep.Core/`
2. Implement `IHarnessApplier`
3. Add tests
4. Submit a PR

### Can I modify profiles locally before applying?

**Workaround:**
1. Export profile to JSON
   ```bash
   mlx-pep results export <run-id> --output run.json --format json
   ```

2. Edit manually (if you know the schema)

3. Convert back to JSONL

4. Apply manually (code change required — fast-follow feature)

**Easier:** Use the harness-specific config files directly after dry-run.

---

## Roadmap & Feature Requests

### What's planned next?

See [README.md → Feature Status](../README.md#feature-status) for the full roadmap.

**High priority:**
- Q3 2026: Community profile browser (download + apply pre-tuned profiles)
- Q4 2026: Batch assessment mode (multiple models)
- Q4 2026: AWS Lambda harness (serverless integration)

### How do I request a feature?

Open a GitHub discussion: https://github.com/matthewcorven/mlx-pep/discussions

**Good requests include:**
- Use case (why you need it)
- Example workflow (how you'd use it)
- Affected users (is it just you, or a broader need?)

---

## Getting Help

### Still stuck?

1. **Docs:** Check [QUICK-START.md](QUICK-START.md) and [ARCHITECTURE.md](ARCHITECTURE.md)
2. **Examples:** See `README.md → Core Workflow` for command examples
3. **Issues:** Search GitHub issues for similar problems
4. **Contact:** Open a GitHub discussion or email the team

### Report a security issue

⚠️ **Do NOT open a public issue.** Email: security@mlx-pep.dev (TBD)
