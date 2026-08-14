# Development Setup

## Prerequisites

- .NET 10.0+
- Python 3.8+
- Git

## Setting Up Model-Assessor Integration

The `mlx-pep assess` command requires the sibling `model-assessor` repository for real model profiling. Follow these steps to set up development environment:

### 1. Clone model-assessor

```bash
cd /Users/core/git/matthewcorven
git clone https://github.com/matthewcorven/model-assessor.git
```

### 2. Create symlink in mlx-pep

```bash
cd mlx-pep/src
ln -s ../../model-assessor model-assessor
```

### 3. Verify the symlink

```bash
ls -la src/model-assessor
# Should show: model-assessor -> ../../model-assessor
```

### 4. Build mlx-pep

```bash
cd /Users/core/git/matthewcorven/mlx-pep
dotnet build
```

This will:
- Copy `.env` to build output
- Copy model-assessor directory to build output (Debug/Release)
- Link `PythonEnvironmentManager` to locate scripts at runtime

### 5. Set up oMLX and environment

The `assess` command requires a running oMLX server and proper environment configuration:

```bash
# Copy from model-assessor
cp src/model-assessor/.env.example .env

# Edit .env with your settings:
# - OMLX_API_KEY=<your-token>
# - OMLX_BASE_URL=http://127.0.0.1:8000 (or your oMLX URL)
# - HF_HUB_CACHE=~/.cache/huggingface/hub
```

### 6. Test the integration

```bash
cd src/MlxPep.Cli/bin/Debug/net10.0

# This should NOT fall back to fixture profiles
./mlx-pep assess <model-id>

# Verify:
# - omlx_bench_harness.py is found
# - .env is loaded (HF_HUB_CACHE, OMLX settings)
# - Profiling runs (not just fixture generation)
```

## Runtime Directory Structure

After building, the output directory contains:

```
bin/Debug/net10.0/
├── mlx-pep.dll
├── .env                           # Environment configuration
└── model-assessor/                # Symlink or copied directory
    ├── config/
    ├── docs/
    ├── fixtures/
    ├── scripts/
    │   ├── omlx_bench_harness.py # The actual benchmark runner
    │   └── ...
    └── ...
```

## Debugging ProfilingRunner

Enable verbose logging to see model-assessor integration details:

```bash
cd src/MlxPep.Cli/bin/Debug/net10.0
DOTNET_ROOT=/path/to/dotnet ./mlx-pep assess <model-id> --verbose
```

Check the debug output for:
- `[ProfilingRunner] Model-assessor directory located`
- `[ProfilingRunner] omlx_bench_harness.py found`
- `[ProfilingRunner] Set working directory to ...`
- `[ProfilingRunner] Successfully parsed manifest`

If you see fallback to fixture profiles instead, check:
1. Symlink exists: `ls -la src/model-assessor`
2. Scripts directory exists: `ls -la src/model-assessor/scripts/`
3. Harness exists: `ls -la src/model-assessor/scripts/omlx_bench_harness.py`
4. `.env` is loaded with proper oMLX credentials
5. oMLX server is running at the configured URL

## HF Cache Configuration

`.env` should include:

```
HF_HUB_CACHE=~/.cache/huggingface/hub
```

This is automatically loaded by `ProfilingRunner` and passed to the benchmark subprocess. Models already downloaded will be found in this cache instead of re-downloading.

## Build Output Artifacts

When you run `dotnet build`:

1. **`.env` copied** — `CopyToOutputDirectory="PreserveNewest"` in MlxPep.Cli.csproj
2. **`model-assessor` copied** — `CopyModelAssessor` MSBuild target copies entire directory
3. **Both included in Release publish** — They're referenced as output artifacts

This ensures the published binary can find model-assessor and `.env` at runtime.
