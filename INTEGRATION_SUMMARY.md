# Model-Assessor Integration Summary

## Changes Made

### 1. **Created Python Environment Manager** (`MlxPep.Core/Python/PythonEnvironmentManager.cs`)
   - Locates model-assessor directory at runtime
   - Provides methods to get scripts path and root path
   - Walks up from assembly location to find repo root

### 2. **Updated ProfilingRunner** (`MlxPep.Core/Profiling/ProfilingRunner.cs`)
   - Now checks for model-assessor local directory first (via `PythonEnvironmentManager`)
   - Invokes `omlx_bench_harness.py` script directly instead of non-existent `model_assessor.cli` module
   - Sets working directory to model-assessor root so relative config paths work
   - Loads `.env` file and expands `~` to home directory
   - Enhanced logging for debugging integration issues

### 3. **Updated Build Configuration** (`src/MlxPep.Cli/MlxPep.Cli.csproj`)
   - Added `CopyRuntimeArtifacts` target that runs after build
   - Copies `.env` file to build output directory
   - Copies entire model-assessor directory to build output
   - Works for Debug, Release, and publish configurations

### 4. **Updated .gitignore**
   - Added `.env` and `src/model-assessor/` to local development section
   - Prevents committing local-only symlink

### 5. **Created DEVELOPMENT.md**
   - Complete setup instructions for developers
   - Explains symlink creation and structure
   - Documents .env configuration
   - Provides debugging guidance

## Directory Structure at Runtime

```
bin/Debug/net10.0/
├── mlx-pep.dll / mlx-pep (executable)
├── MlxPep.Core.dll
├── .env                              # Loaded by ProfilingRunner
└── model-assessor/                   # Copied from src/
    ├── config/
    │   ├── benchmark_profiles.json
    │   ├── prompt_templates.json
    │   └── smoke_suite.json
    ├── docs/
    ├── scripts/
    │   ├── omlx_bench_harness.py    # The real profiler
    │   └── ...
    └── ...
```

## How It Works

1. **Initialization**: When `mlx-pep assess` command runs:
   - CLI loads `ProfilingRunner`
   - `ProfilingRunner.IsAvailableAsync()` checks if model-assessor directory exists
   - Verifies `omlx_bench_harness.py` is present

2. **Profiling**: When assessment runs:
   - Resolves model-assessor scripts path via `PythonEnvironmentManager`
   - Loads `.env` file and passes environment variables to subprocess
   - Sets working directory to model-assessor root (for config file access)
   - Invokes: `python3 /path/to/omlx_bench_harness.py assess <model-id> --suite <suite> --output json`
   - Parses JSON manifest from benchmark script output
   - Returns structured profile recommendations

3. **Configuration**: `.env` provides:
   - `HF_HUB_CACHE=~/.cache/huggingface/hub` — Hugging Face model cache (avoids re-downloads)
   - `OMLX_API_KEY` — Authentication for oMLX server
   - `OMLX_BASE_URL` — oMLX server URL

## Key Improvements

- ✅ **Real profiling**: No more dummy fixture profiles
- ✅ **Active development**: model-assessor changes immediately reflected
- ✅ **HF cache reuse**: Models already downloaded won't re-download
- ✅ **Portable builds**: `.env` and model-assessor copied to output
- ✅ **Debuggable**: Rich Debug.WriteLine logs for troubleshooting

## Validation Steps

1. Build the project:
   ```bash
   dotnet build
   ```

2. Verify output structure:
   ```bash
   ls -la src/MlxPep.Cli/bin/Debug/net10.0/.env
   ls -la src/MlxPep.Cli/bin/Debug/net10.0/model-assessor/scripts/omlx_bench_harness.py
   ```

3. Test the assess command (requires oMLX server running):
   ```bash
   cd src/MlxPep.Cli/bin/Debug/net10.0
   ./mlx-pep assess <model-id>
   ```

4. If needed, enable debugging:
   ```bash
   # Review .env is loaded
   cat .env
   
   # Check script is callable
   python3 model-assessor/scripts/omlx_bench_harness.py --version
   ```

## Next Steps

- Ensure oMLX server is running for profiling tests
- Copy model-assessor `.env.example` to mlx-pep `.env` and configure credentials
- Verify first profile generation completes without falling back to fixtures
