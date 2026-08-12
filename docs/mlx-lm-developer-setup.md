# mlx-lm Development Environment Setup

**For:** Contributors implementing mlx-pep's mlx-lm integration (Phase 1, MVP+1)  
**Status:** Preparation (before Phase 1 kickoff)  
**Updated:** 2026-08-11

---

## Prerequisites

### Hardware
- **Apple Silicon Mac** (M1 / M2 / M3 / M4 series) — required for testing
  - 16GB unified memory minimum (8GB + swap acceptable for small models)
  - 8GB recommended per 30B model parameters

### Operating System
- macOS 13.4+ (Ventura, Sonoma, Sequoia)
- Xcode Command Line Tools (for Python build tools)
  ```bash
  xcode-select --install
  ```

### Software Stack
- Python 3.11+ (required for mlx-lm; 3.12+ recommended for performance)
  ```bash
  python3 --version  # Verify: Python 3.11.x or higher
  ```
- .NET 9.0+ SDK (mlx-pep runs on .NET)
  ```bash
  dotnet --version
  ```
- Git (CLI already has this; verify: `git --version`)

---

## Step 1: Clone Repository & Create Feature Branch

```bash
# Clone mlx-pep if not already done
git clone https://github.com/matthewcorven/mlx-pep.git
cd mlx-pep

# Create feature branch for mlx-lm work
git checkout -b feature/mlx-lm-integration
```

---

## Step 2: Install Python Runtime Dependencies

### Option A: Virtual Environment (Recommended)
```bash
# Create isolated Python environment for mlx-lm testing
python3 -m venv .venv-mlx
source .venv-mlx/bin/activate

# Upgrade pip
pip install --upgrade pip

# Install mlx-lm and dependencies
pip install mlx-lm>=0.19.0 mlx mlx-data
```

### Option B: System Python
```bash
# If you prefer direct system install (not recommended; can conflict with other projects)
pip install mlx-lm>=0.19.0 mlx mlx-data --user
```

### Verification
```bash
# Check mlx-lm is installed correctly
python3 -m mlx_lm.generate --help

# Verify mlx version
python3 -c "import mlx; print(f'MLX version: {mlx.__version__}')"

# List available models (queries mlx-community registry)
python3 -c "from mlx_lm.utils import load_model; help(load_model)"
```

---

## Step 3: Download a Small Test Model

For development & testing, use a small 4-bit quantized model (1-2 GB):

```bash
# Option A: Mistral-7B-4bit (most common test size)
python3 << 'EOF'
from mlx_lm.models.base import create_model_inputs
from mlx_lm.utils import load_model

# This auto-downloads to ~/.cache/huggingface/hub/
model, tokenizer = load_model("mlx-community/Mistral-7B-Instruct-v0.1-4bit")
print(f"✓ Model loaded: {model}")
EOF
```

Or use `huggingface-cli` directly:
```bash
pip install huggingface-hub
huggingface-cli download mlx-community/Mistral-7B-Instruct-v0.1-4bit
```

---

## Step 4: Build & Test .NET Projects

### Restore & Build
```bash
cd mlx-pep  # Repository root

# Restore NuGet packages
dotnet restore

# Build solution
dotnet build

# Verify no errors
echo $?  # Should print 0
```

### Run Existing Tests
```bash
# Run all unit tests (should pass)
dotnet test --filter "Category!=Integration" --verbosity normal

# Run only oMLX tests (to verify baseline)
dotnet test --filter "Category=OMLx" --verbosity normal
```

---

## Step 5: Set Up IDE for .NET Development

### VS Code (Recommended for CLI work)
```bash
# Install C# Dev Kit extension
code --install-extension ms-dotnettools.csdevkit

# Install Omnisharp (language server)
code --install-extension ms-dotnettools.omnisharp
```

### Visual Studio for Mac (Alternative)
```bash
# Download from https://visualstudio.microsoft.com/vs/mac/
# Open mlx-pep.sln to load all projects
```

---

## Step 6: Prepare for mlx-lm Integration

### Create MlxLmProfilingRunner Tests (Scaffold)

Before implementing, create test file stubs:

```bash
# Create test directory if missing
mkdir -p src/mlx-pep.Tests/Profilers/Runners

# Create test file (stub)
touch src/mlx-pep.Tests/Profilers/Runners/MlxLmProfilingRunnerTests.cs
```

Example test scaffold:
```csharp
namespace MlxPep.Tests.Profilers.Runners;

public class MlxLmProfilingRunnerTests
{
    [Fact]
    public async Task VerifyInstalledAsync_WithMlxLmInstalled_ReturnsTrue()
    {
        // Arrange
        var runner = new MlxLmProfilingRunner();
        
        // Act
        var installed = await runner.VerifyInstalledAsync();
        
        // Assert
        Assert.True(installed, "mlx-lm should be installed in development environment");
    }
}
```

### Environment Variables for Testing

Create `.env.test` (not committed):
```bash
# .env.test (local-only, add to .gitignore)
MLX_LM_PYTHON_PATH=/usr/bin/python3
MLX_LM_TIMEOUT_SECONDS=300
MLX_LM_TEST_MODEL=mlx-community/Mistral-7B-Instruct-v0.1-4bit
```

Load in test setup:
```csharp
[SetUpFixture]
public class TestEnvironment
{
    [OneTimeSetUp]
    public void Setup()
    {
        var envPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", ".env.test");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                    Environment.SetEnvironmentVariable(parts[0], parts[1]);
            }
        }
    }
}
```

---

## Step 7: Understand the Code Structure

### Current Project Layout
```
mlx-pep/
├── src/
│   ├── mlx-pep/               # Main CLI application
│   │   └── Commands/          # assess, doctor, apply, etc.
│   ├── mlx-pep.Core/          # Profile, schema, validation
│   │   └── Profiles/          # Profile data model & versioning
│   ├── mlx-pep.Profilers/     # Profiling interfaces & runners
│   │   ├── IProfilingRunner/  # Abstract interface (to be created)
│   │   └── OMLx/              # Existing oMLX implementation
│   └── mlx-pep.Tests/         # Unit & integration tests
├── docs/
│   ├── profile-schema.md      # Current schema definition (to be updated)
│   └── research/
│       └── runtimes.md        # Runtime research document
└── .squad/
    ├── decisions/
    │   └── adr-001-multi-runtime-strategy.md
    └── agents/neo/
        ├── charter.md
        ├── mlx-lm-checklist.md
        └── implementation-guide-mlx-lm.md
```

### Key Files to Know

1. **Profile.cs** (to modify)
   - Current location: `src/mlx-pep.Core/Profiles/Profile.cs`
   - Change: Add `Engine` enum, nullable config objects
   - Scope: Backward-compatible JSON serialization

2. **AssessCommand.cs** (to modify)
   - Current location: `src/mlx-pep/Commands/AssessCommand.cs`
   - Change: Add `--engine` option, use `ProfilingRunnerFactory`
   - Scope: CLI argument parsing and runner instantiation

3. **DoctorCommand.cs** (to modify)
   - Current location: `src/mlx-pep/Commands/DoctorCommand.cs`
   - Change: Add runtime detection loop, report versions
   - Scope: Runtime availability detection

4. **OMLxProfilingRunner.cs** (reference implementation)
   - Current location: `src/mlx-pep.Profilers/OMLx/OMLxProfilingRunner.cs`
   - Reference for: `IProfilingRunner` interface pattern
   - Study: subprocess spawning, JSON parsing, error handling

---

## Step 8: Common Development Workflows

### Run mlx-lm Directly (for testing/debug)

```bash
source .venv-mlx/bin/activate  # Activate Python venv

# Test model load
python3 -m mlx_lm.generate --model mlx-community/Mistral-7B-Instruct-v0.1-4bit \
  --prompt "Hello, world" --max-tokens 10

# Benchmark a model (generates tokens/sec metric)
python3 -c "
from mlx_lm.utils import load_model
from time import perf_counter
import mlx.core as mx

model, tokenizer = load_model('mlx-community/Mistral-7B-Instruct-v0.1-4bit')
tokens = tokenizer.encode('Hello world')
print(f'Loaded model. Token count: {len(tokens)}')
"
```

### Debug mlx-lm Integration in .NET

```csharp
// In test or main program:
var runner = new MlxLmProfilingRunner();

// Check installation
var installed = await runner.VerifyInstalledAsync();
Console.WriteLine($"mlx-lm installed: {installed}");

// Get version
var version = await runner.GetVersionAsync();
Console.WriteLine($"mlx-lm version: {version}");

// Profile a model (async)
var result = await runner.ProfileAsync(new ProfileRequest
{
    ModelId = "mlx-community/Mistral-7B-Instruct-v0.1-4bit",
    MaxTokens = 100,
    Engine = Engine.MlxLm
});
```

---

## Step 9: Troubleshooting

### mlx-lm Installation Issues

**Problem:** `pip install mlx-lm` fails with ARM64 architecture error
```bash
# Solution: Use pre-built wheels for Apple Silicon
pip install mlx-lm --only-binary mlx-lm --no-cache-dir
```

**Problem:** `python3 -m mlx_lm.generate` returns "module not found"
```bash
# Solution: Verify virtual environment is activated
source .venv-mlx/bin/activate
python3 -c "import mlx_lm; print(mlx_lm.__file__)"
```

**Problem:** Model download fails (network timeout)
```bash
# Solution: Pre-download manually and set HF_HOME
export HF_HOME=~/.cache/huggingface
huggingface-cli login  # Provide token if using gated models
huggingface-cli download mlx-community/Mistral-7B-Instruct-v0.1-4bit
```

### .NET Build Issues

**Problem:** `dotnet build` fails with "Unable to find framework"
```bash
# Solution: Install correct .NET SDK
dotnet --list-sdks
dotnet sdk list  # macOS

# Install missing version
# From https://dotnet.microsoft.com/download/dotnet
```

**Problem:** Unit tests fail with "Could not find process"
```bash
# Solution: Ensure Python subprocess is findable
which python3  # Should return /path/to/python3
# Update MlxLmProfilingRunner to use absolute path if needed
```

---

## Step 10: Running Integration Tests

### Full Integration Test Suite

```bash
# Set environment (required for mlx-lm tests)
export MLX_LM_PYTHON_PATH=$(which python3)

# Run integration tests only
dotnet test --filter "Category=Integration" \
  --verbosity normal \
  --logger "console;verbosity=detailed"

# Run mlx-lm integration tests specifically
dotnet test --filter "Category=Integration & Category=MlxLm" \
  --verbosity normal
```

### Test Coverage Report

```bash
# Install coverage tool
dotnet add package coverlet.collector

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=cobertura

# View report
# Coverage reports saved to: TestResults/coverage.cobertura.xml
```

---

## Next Steps After Setup

1. ✅ Verify all tools installed and tests passing
2. ⏳ Wait for adversarial review of ADR-001
3. ⏳ Get Morpheus approval on implementation plan
4. 🚀 **Start Phase 1:** Schema changes (Profile.cs enum)
5. 🚀 **Phase 1 Step 2:** IProfilingRunner interface + factory
6. 🚀 **Phase 1 Step 3:** MlxLmProfilingRunner implementation
7. 🚀 **Phase 1 Step 4:** CLI `--engine` option
8. 🚀 **Phase 1 Step 5:** doctor command runtime detection
9. 🚀 **Phase 1 Step 6:** Unit & integration tests
10. 🚀 **Phase 1 Step 7:** Documentation (engines.md, schema.md, README)

---

## Resources

### mlx-lm Documentation
- **GitHub:** https://github.com/ml-explore/mlx-lm
- **Models:** https://huggingface.co/mlx-community (500+ quantized models)
- **Benchmarks:** https://github.com/ml-explore/mlx-lm/blob/main/README.md#performance

### MLX Framework
- **GitHub:** https://github.com/ml-explore/mlx
- **Docs:** https://ml-explore.github.io/mlx/build/html/index.html
- **API Reference:** https://ml-explore.github.io/mlx/build/html/python/api.html

### Development Guides
- See: `.squad/decisions/adr-001-multi-runtime-strategy.md` (rationale)
- See: `docs/implementation-guide-mlx-lm.md` (detailed technical roadmap)
- See: `.squad/agents/neo/mlx-lm-checklist.md` (phase-by-phase checklist)

### Testing Frameworks
- **NUnit** (or **xUnit**): Unit testing framework
- **Moq**: Mocking subprocess calls
- **Coverlet**: Code coverage reporting

---

**Created by:** Neo (Data/AI/Search specialist)  
**For Issue:** #25 runtimes: mlx-lm / llama.cpp / vLLM support  
**Phase:** MVP+1 (mlx-lm focus)  
**Status:** Preparation (awaiting review + approval)

**Questions?** Refer to `docs/implementation-guide-mlx-lm.md` or ask in #squad channel.
