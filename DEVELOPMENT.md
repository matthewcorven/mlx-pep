# Development Guide

**For:** Contributors, developers, anyone working on mlx-pep code

**Note:** If you're just getting started, see [docs/QUICK-START.md](docs/QUICK-START.md) for consumer setup first.

---

## Prerequisites

- .NET 10.0+
- Python 3.8+
- Git
- `pytest` for Python validation via a project virtual environment
- (Same hardware/OS requirements as [docs/QUICK-START.md](docs/QUICK-START.md#prerequisites-checklist))

---

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

---

## Contributing

### Typical Development Tasks

#### Add Support for a New Harness

Example: you want mlx-pep to apply profiles to a new editor.

1. Create `src/MlxPep.Core/NewHarnessApplier.cs` implementing `IHarnessApplier`
2. Add a test in `tests/MlxPep.Core.Tests/NewHarnessApplierTests.cs`
3. Update `src/MlxPep.Cli/CliBuilder.cs` to handle the new harness in `Apply()`
4. Add example usage to this file and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
5. Submit a PR

#### Add a New Benchmark Tier

Example: add a `micro` suite faster than smoke.

1. Add script to `src/model-assessor/scripts/`
2. Update `run_smoke_suite.sh` or create `run_micro_suite.sh`
3. Update `ProfilingRunner` to accept the new suite parameter
4. Add tests
5. Document in [docs/FAQ.md](docs/FAQ.md)

#### Extend Profiles

Add new fields to the profile JSONL schema:

1. Update `src/MlxPep.Core/Profile.cs` (the data model)
2. Update `ProfileValidator.cs` (add validation rules)
3. Update `ProfileJsonSerializerContext.cs` (JSON serialization)
4. Update [docs/profile-schema.md](docs/profile-schema.md)
5. Update all `*HarnessApplier` implementations to handle new field (if needed)
6. Add tests

### Running Tests

```bash
# Create and activate a project-local virtual environment
python3 -m venv .venv
source .venv/bin/activate

# Install developer Python tooling
python -m pip install --upgrade pip
python -m pip install -r requirements-dev.txt

# Run Python tests
python -m pytest src/model-assessor/tests -q

# Run all .NET tests
dotnet test

# Run specific test file
dotnet test tests/MlxPep.Core.Tests/ProfileValidatorTests.cs

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Code Style & Conventions

- Follow [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use nullable reference types (`#nullable enable` at top of file)
- Add XML doc comments to public APIs
- Keep methods focused (single responsibility)
- Log at `Debug` level for conditional paths (see copilot-instructions.md)

### Submitting PRs

1. Create a feature branch: `git checkout -b feature/description`
2. Make changes, commit with clear messages
3. Add/update tests
4. Run full test suite: `dotnet test`
5. Push and open PR
6. Address review feedback
7. Maintainers will merge when ready

### Debugging Integration Issues

If model-assessor isn't found or profiles fall back to fixtures:

```bash
# Enable verbose logging
export MLXPEP_DEBUG=1

# Run with diagnostics
dotnet run --project src/MlxPep.Cli/MlxPep.Cli.csproj -- \
  assess <model> --suite smoke --verbose

# Check symlink/directory
ls -la src/model-assessor/
ls -la src/model-assessor/scripts/omlx_bench_harness.py
```

---

## Getting Help

- **Architecture questions:** See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- **Profile schema:** See [docs/profile-schema.md](docs/profile-schema.md)
- **Deployment:** See [docs/PUBLISH-FLOW.md](docs/PUBLISH-FLOW.md)
- **Issues/PRs:** Open on GitHub or ask in discussions

---

## Additional Resources

- [Quick Start Guide](docs/QUICK-START.md)
- [Architecture Overview](docs/ARCHITECTURE.md)
- [FAQ](docs/FAQ.md)
- [PRD](docs/PRD.md) — strategic vision
