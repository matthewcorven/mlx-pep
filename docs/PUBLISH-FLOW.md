# Profile Publishing Flow

**Issue #27: profiling: publish-flow polish + community metadata**

This guide documents the complete workflow for publishing MLX profiles to the community repository.

## Table of Contents

1. [Overview](#overview)
2. [Publishing Workflow](#publishing-workflow)
3. [Community Metadata](#community-metadata)
4. [Deduplication Strategy](#deduplication-strategy)
5. [Examples](#examples)
6. [FAQ & Troubleshooting](#faq--troubleshooting)

---

## Overview

The MLX Profiles system allows community members to:
- Create and optimize profiles for their hardware
- Publish profiles to a shared community repository
- Share best practices and configuration optimizations
- Help others find performant settings for their devices

**Key concepts:**
- **Profile**: A complete MLX configuration including model settings, sampler config, and hardware fingerprint
- **Community Metadata**: Optional enrichment fields (tags, description, memory range) that enable discoverability
- **DedupKey**: Unique identifier within the community repository to prevent duplicates

---

## Publishing Workflow

### Step 1: Create Your Profile

Start with a working profile optimized for your hardware:

```python
from mlx_pep import Profile, ProfileProvenance, HardwareFingerprint

profile = Profile(
    schema_version=1,
    id="my-optimized-profile-v1",
    model_hf_id="meta-llama/Llama-2-7b",
    tier="production",
    engine="mlx",
    system={
        "os": "macOS",
        "version": "14.0",
    },
    omlx={
        "compute_units": "ALL",
        "distributed": False,
        "quantization": "int8",
    },
    harness={
        "backend": "mlx",
        "streaming": True,
    },
    provenance=ProfileProvenance(
        author="your_username",
        created_at="2024-08-11T20:30:00Z",
        source="community",
    ),
    hardware=HardwareFingerprint(
        chip="Apple M2",
        memory_gb=16,
        model_identifier="MacBookPro18,2",
    ),
)
```

### Step 2: Add Community Metadata

Enhance your profile with metadata to help others discover and understand it:

```python
from mlx_pep import CommunityMetadata

community_metadata = CommunityMetadata(
    tags=["production", "inference", "low-latency"],
    keywords=["llama", "7b", "quantized", "apple-silicon"],
    description="Optimized Llama 2 7B int8 profile for MacBook Pro M2. Achieves ~45 tokens/sec with 500ms latency.",
    min_memory_gb=8,
    max_memory_gb=32,
    hardware_family="Apple Silicon",
    dedup_key="llama-2-7b-int8-apple-m2-v1",
)

profile.community = community_metadata
```

**Metadata Fields:**

| Field | Type | Required for Publishing | Purpose |
|-------|------|-------------------------|---------|
| `tags` | List[str] | No | Categorical labels (see [Valid Tags](#valid-tags)) |
| `keywords` | List[str] | No | Searchable keywords (free-form) |
| `description` | str | No | Human-readable summary (max 500 chars) |
| `min_memory_gb` | int | No | Minimum memory requirement |
| `max_memory_gb` | int | No | Maximum tested memory |
| `hardware_family` | str | No | Hardware category (e.g., "Apple Silicon", "NVIDIA RTX 40", "AMD Radeon") |
| `dedup_key` | str | **Yes** | Unique identifier (alphanumeric + hyphens, 3-50 chars) |

### Step 3: Validate Your Profile

Validate before publishing:

```python
from mlx_pep.core import ProfileValidator

validator = ProfileValidator()
result = validator.validate_for_publishing(profile)

if not result.is_valid:
    print("Validation errors:")
    for error in result.errors:
        print(f"  - {error}")
else:
    print("Profile ready for publishing!")
```

### Step 4: Save and Share

Save your profile to a JSONL file:

```python
from mlx_pep.core import ProfileReader

reader = ProfileReader()
await reader.write_profile_set_async("my-profiles.jsonl", [profile])
```

Then submit to the community repository:
1. Fork the `mlx-pep` repository
2. Add your profile(s) to `profiles/community/`
3. Create a pull request with your profiles
4. Community maintainers will review and merge

### Step 5: Maintenance

Monitor your published profiles:

```python
# Load published profiles
profiles = await reader.read_profile_set_async("profiles/community/all-profiles.jsonl")

# Find duplicates of your profile
duplicates = reader.find_duplicates_by_dedup_key(profiles)
if "your-dedup-key" in duplicates:
    print(f"Found {len(duplicates['your-dedup-key'])} versions of your profile")
    # Choose which to keep; deduplication keeps newest by CreatedAt
```

---

## Community Metadata

### Valid Tags

Use these predefined tags to help users find your profile:

**Performance Category:**
- `production` – Tested and stable for production use
- `experimental` – Early testing, may be unstable
- `benchmark` – Designed for benchmarking (not production)

**Usage Type:**
- `inference` – Optimized for inference
- `training` – Optimized for training
- `streaming` – Supports streaming inference

**Quantization:**
- `quantized` – Uses quantization for size/speed
- `unquantized` – No quantization

**Hardware Focus:**
- `cpu` – CPU-optimized
- `gpu` – GPU-optimized
- `npu` – Neural Processing Unit optimized

**Performance Trade-offs:**
- `low-latency` – Minimizes latency
- `high-latency` – May have higher latency
- `high-throughput` – Maximizes throughput
- `memory-optimized` – Minimizes memory
- `speed-optimized` – Maximizes speed
- `accuracy-optimized` – Prioritizes accuracy

### DedupKey Requirements

The `dedup_key` must:
- Be 3-50 characters long
- Contain only alphanumeric characters and hyphens
- Be unique within your published profiles
- Be descriptive (includes model, optimization, hardware, version)

**Examples:**
- ✅ `llama-2-7b-int8-apple-m2-v1`
- ✅ `mistral-7b-fp32-cpu-v2`
- ✅ `phi-2-quantized-mac-studio`
- ❌ `dedup@key` (invalid chars)
- ❌ `ab` (too short)
- ❌ `this-is-my-super-long-deduplication-key-that-exceeds-50-characters` (too long)

---

## Deduplication Strategy

When profiles with the same `dedup_key` are submitted:

1. **Detection**: System identifies profiles with matching dedup_keys
2. **Resolution**: Keeps the **newest** profile (by `created_at`)
3. **Action**: Older versions are removed from the repository

This ensures the community repository always contains the most up-to-date version of each profile.

**Best practice:** Update your `dedup_key` version suffix when making significant changes:
- `...v1` → `...v2` (major improvements)
- `...v2-patch1` (minor fixes)

---

## Examples

### Example 1: Llama 2 7B for MacBook Pro M2

```json
{
  "schemaVersion": 1,
  "id": "llama-2-7b-m2-optimized",
  "modelHfId": "meta-llama/Llama-2-7b",
  "tier": "production",
  "engine": "mlx",
  "system": {
    "os": "macOS",
    "version": "14.6"
  },
  "omlx": {
    "compute_units": "ALL",
    "distributed": false,
    "quantization": "int8"
  },
  "harness": {
    "backend": "mlx",
    "streaming": true
  },
  "provenance": {
    "author": "alice",
    "createdAt": "2024-08-10T15:30:00Z",
    "source": "community"
  },
  "hardware": {
    "chip": "Apple M2",
    "memoryGb": 16,
    "modelIdentifier": "MacBookPro18,2"
  },
  "community": {
    "tags": ["production", "inference", "low-latency", "quantized"],
    "keywords": ["llama", "7b", "int8", "apple-silicon", "macbook-pro"],
    "description": "Optimized Llama 2 7B int8 quantized model for M2 MacBook Pro. Achieves 45 tokens/sec with <500ms latency.",
    "minMemoryGb": 8,
    "maxMemoryGb": 32,
    "hardwareFamily": "Apple Silicon",
    "dedupKey": "llama-2-7b-int8-apple-m2-v1"
  }
}
```

### Example 2: Mistral 7B for CPU

```json
{
  "schemaVersion": 1,
  "id": "mistral-7b-cpu-fp32",
  "modelHfId": "mistralai/Mistral-7B-v0.1",
  "tier": "experimental",
  "engine": "mlx",
  "system": {
    "os": "macOS",
    "version": "13.0"
  },
  "omlx": {
    "compute_units": "CPU",
    "distributed": false,
    "quantization": null
  },
  "harness": {
    "backend": "mlx",
    "streaming": false
  },
  "provenance": {
    "author": "bob",
    "createdAt": "2024-08-09T12:00:00Z",
    "source": "community"
  },
  "hardware": {
    "chip": "Intel Core i9",
    "memoryGb": 64,
    "modelIdentifier": "Mac13,6"
  },
  "community": {
    "tags": ["experimental", "inference", "cpu", "unquantized", "high-latency"],
    "keywords": ["mistral", "7b", "fp32", "cpu-only"],
    "description": "CPU-only Mistral 7B profile. Slow but useful for debugging and testing without GPU.",
    "minMemoryGb": 32,
    "maxMemoryGb": 128,
    "hardwareFamily": "Intel CPU",
    "dedupKey": "mistral-7b-fp32-cpu-v1"
  }
}
```

---

## FAQ & Troubleshooting

### Q: Can I publish profiles without community metadata?

A: No. The `dedup_key` field is required for publishing. Other metadata fields are optional but highly recommended for discoverability.

### Q: What if my dedup_key conflicts with someone else's?

A: The deduplication system will compare `created_at` timestamps. The newest profile (by creation time) takes precedence. Ensure your dedup_key accurately reflects your profile's purpose and version.

### Q: How do I update an existing published profile?

A: Submit a new profile with the same `dedup_key` but an updated `created_at` timestamp and version suffix. The system will detect it's a newer version and replace the old one.

### Q: Can I search profiles by hardware requirements?

A: Yes. Use `ProfileReader.filter_by_hardware(profiles, memory_gb=16, hardware_family="Apple Silicon")` to find compatible profiles.

### Q: What's the difference between tags and keywords?

A: **Tags** are predefined categories for consistent filtering. **Keywords** are free-form for richer search. Use both for maximum discoverability.

### Q: Is there a size limit on descriptions?

A: Yes, descriptions are limited to 500 characters. Keep them concise and informative.

### Q: Can I delete or hide my published profile?

A: Submit a pull request to remove your profile from the community repository. Community maintainers will handle the removal.

### Q: How are profiles validated?

A: Profiles are validated for:
- Required fields (id, model_hf_id, etc.)
- Memory range validity (min ≤ max)
- DedupeKey format (3-50 chars, alphanumeric + hyphens)
- Tag whitelisting (against valid tag list)
- Description length (≤ 500 chars)

See `ProfileValidator.validate_for_publishing()` for full validation rules.

---

## Integration with MLX CLI

*(Coming soon)* Future versions will integrate profile publishing with the MLX CLI:

```bash
# Validate a profile
mlx profile validate my-profile.jsonl --for-publishing

# Search published profiles
mlx profile search --tag production --hardware "Apple Silicon"

# Publish to community repo
mlx profile publish my-profile.jsonl
```

---

## Support

For questions or issues with publishing:
1. Check this documentation
2. Review existing published profiles in `profiles/community/`
3. Open an issue in the mlx-pep repository with the `area:profiling` label
4. Contact community maintainers

---

**Last updated:** 2024-08-11
**Schema version:** 1.0
**Status:** Stable
