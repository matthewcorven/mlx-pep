namespace MlxPep.Core.Tests.Fixtures;

/// <summary>
/// Mock fixtures for model-assessor output.
/// Issue #17: Provides sample assessor recommendation manifests for profiling tests.
/// These fixtures represent the JSON contract between mlx-pep and model-assessor.
/// </summary>
public static class AssessorFixtures
{
    /// <summary>
    /// Sample assessor recommendation manifest for a 7B model on M3 Pro.
    /// This is the output from `model-assessor profile` that mlx-pep consumes.
    /// The output drives generation of three tiered profiles (high/balanced/efficient).
    /// </summary>
    public const string Llama7bRecommendationManifest = @"{
  ""model_id"": ""meta-llama/Llama-2-7b"",
  ""hardware_fingerprint"": {
    ""chip"": ""Apple M3 Pro"",
    ""memory_gb"": 18,
    ""model_identifier"": ""MacBookPro18,1""
  },
  ""assessment_results"": {
    ""suite"": ""full"",
    ""duration_seconds"": 45,
    ""timestamp"": ""2026-08-13T00:00:00Z""
  },
  ""tier_recommendations"": {
    ""high"": {
      ""reason"": ""Maximum performance for typical workloads"",
      ""system"": {
        ""iogpu.wired_limit_mb"": 6144
      },
      ""omlx"": {
        ""memory_guard_tier"": ""high"",
        ""memory_guard_ceiling_gb"": 16
      },
      ""harness"": {
        ""vscode"": {
          ""maxInputTokens"": 64000,
          ""maxOutputTokens"": 3072
        },
        ""copilotCli"": {
          ""maxPromptTokens"": 64000
        }
      },
      ""sampler"": {
        ""temperature"": 0.7,
        ""topP"": 0.95,
        ""topK"": 20,
        ""repetitionPenalty"": 1.02,
        ""contextTokens"": 4096
      }
    },
    ""balanced"": {
      ""reason"": ""Recommended for most users; balanced performance and resource usage"",
      ""system"": {
        ""iogpu.wired_limit_mb"": 4096
      },
      ""omlx"": {
        ""memory_guard_tier"": ""balanced"",
        ""memory_guard_ceiling_gb"": 12
      },
      ""harness"": {
        ""vscode"": {
          ""maxInputTokens"": 32000,
          ""maxOutputTokens"": 2048
        },
        ""copilotCli"": {
          ""maxPromptTokens"": 32000
        }
      },
      ""sampler"": {
        ""temperature"": 0.7,
        ""topP"": 0.95,
        ""topK"": 20,
        ""repetitionPenalty"": 1.02,
        ""contextTokens"": 2048
      }
    },
    ""efficient"": {
      ""reason"": ""Minimum resource footprint; suitable for resource-constrained scenarios"",
      ""system"": {
        ""iogpu.wired_limit_mb"": 2048
      },
      ""omlx"": {
        ""memory_guard_tier"": ""efficient"",
        ""memory_guard_ceiling_gb"": 8
      },
      ""harness"": {
        ""vscode"": {
          ""maxInputTokens"": 16000,
          ""maxOutputTokens"": 1024
        },
        ""copilotCli"": {
          ""maxPromptTokens"": 16000
        }
      },
      ""sampler"": {
        ""temperature"": 0.7,
        ""topP"": 0.95,
        ""topK"": 20,
        ""repetitionPenalty"": 1.02,
        ""contextTokens"": 1024
      }
    }
  }
}";

    /// <summary>
    /// Sample assessor output for a 35B model (heavier resource requirements).
    /// Demonstrates tier recommendations across wider range of configurations.
    /// </summary>
    public const string Ornith35bRecommendationManifest = @"{
  ""model_id"": ""wang-yang/Ornith-1.0-35B-MTPLX"",
  ""hardware_fingerprint"": {
    ""chip"": ""Apple M4 Max"",
    ""memory_gb"": 128,
    ""model_identifier"": ""Mac16,5""
  },
  ""assessment_results"": {
    ""suite"": ""full"",
    ""duration_seconds"": 120,
    ""timestamp"": ""2026-08-13T00:00:00Z""
  },
  ""tier_recommendations"": {
    ""high"": {
      ""reason"": ""Maximum performance for intensive workloads"",
      ""system"": {
        ""iogpu.wired_limit_mb"": 122880
      },
      ""omlx"": {
        ""memory_guard_tier"": ""high"",
        ""memory_guard_ceiling_gb"": 108
      },
      ""harness"": {
        ""vscode"": {
          ""maxInputTokens"": 128000,
          ""maxOutputTokens"": 8192
        },
        ""copilotCli"": {
          ""maxPromptTokens"": 128000
        }
      },
      ""sampler"": {
        ""temperature"": 0.7,
        ""topP"": 0.95,
        ""topK"": 40,
        ""repetitionPenalty"": 1.02,
        ""contextTokens"": 8192
      }
    },
    ""balanced"": {
      ""reason"": ""Recommended for most users"",
      ""system"": {
        ""iogpu.wired_limit_mb"": 81920
      },
      ""omlx"": {
        ""memory_guard_tier"": ""balanced"",
        ""memory_guard_ceiling_gb"": 72
      },
      ""harness"": {
        ""vscode"": {
          ""maxInputTokens"": 64000,
          ""maxOutputTokens"": 4096
        },
        ""copilotCli"": {
          ""maxPromptTokens"": 64000
        }
      },
      ""sampler"": {
        ""temperature"": 0.7,
        ""topP"": 0.95,
        ""topK"": 30,
        ""repetitionPenalty"": 1.02,
        ""contextTokens"": 4096
      }
    },
    ""efficient"": {
      ""reason"": ""Reduced memory footprint"",
      ""system"": {
        ""iogpu.wired_limit_mb"": 40960
      },
      ""omlx"": {
        ""memory_guard_tier"": ""efficient"",
        ""memory_guard_ceiling_gb"": 40
      },
      ""harness"": {
        ""vscode"": {
          ""maxInputTokens"": 32000,
          ""maxOutputTokens"": 2048
        },
        ""copilotCli"": {
          ""maxPromptTokens"": 32000
        }
      },
      ""sampler"": {
        ""temperature"": 0.7,
        ""topP"": 0.95,
        ""topK"": 20,
        ""repetitionPenalty"": 1.02,
        ""contextTokens"": 2048
      }
    }
  }
}";

    /// <summary>
    /// Minimal valid recommendation manifest (smoke suite on small model).
    /// Used for quick validation tests without heavy computation simulation.
    /// </summary>
    public const string SmokeSuiteMinimalManifest = @"{
  ""model_id"": ""TinyLlama/TinyLlama-1.1B"",
  ""hardware_fingerprint"": {
    ""chip"": ""Apple M2"",
    ""memory_gb"": 16,
    ""model_identifier"": ""MacBookPro18,2""
  },
  ""assessment_results"": {
    ""suite"": ""smoke"",
    ""duration_seconds"": 10,
    ""timestamp"": ""2026-08-13T00:00:00Z""
  },
  ""tier_recommendations"": {
    ""high"": {
      ""reason"": ""High performance"",
      ""system"": { ""iogpu.wired_limit_mb"": 2048 },
      ""omlx"": { ""memory_guard_tier"": ""high"", ""memory_guard_ceiling_gb"": 14 },
      ""harness"": { ""vscode"": { ""maxInputTokens"": 16000 }, ""copilotCli"": { ""maxPromptTokens"": 16000 } },
      ""sampler"": { ""temperature"": 0.7 }
    },
    ""balanced"": {
      ""reason"": ""Balanced mode"",
      ""system"": { ""iogpu.wired_limit_mb"": 1024 },
      ""omlx"": { ""memory_guard_tier"": ""balanced"", ""memory_guard_ceiling_gb"": 10 },
      ""harness"": { ""vscode"": { ""maxInputTokens"": 8000 }, ""copilotCli"": { ""maxPromptTokens"": 8000 } },
      ""sampler"": { ""temperature"": 0.7 }
    },
    ""efficient"": {
      ""reason"": ""Efficient mode"",
      ""system"": { ""iogpu.wired_limit_mb"": 512 },
      ""omlx"": { ""memory_guard_tier"": ""efficient"", ""memory_guard_ceiling_gb"": 6 },
      ""harness"": { ""vscode"": { ""maxInputTokens"": 4000 }, ""copilotCli"": { ""maxPromptTokens"": 4000 } },
      ""sampler"": { ""temperature"": 0.7 }
    }
  }
}";

    /// <summary>
    /// Malformed manifest: missing required tier.
    /// Used to test error handling and validation.
    /// </summary>
    public const string MalformedMissingTier = @"{
  ""model_id"": ""meta-llama/Llama-2-7b"",
  ""hardware_fingerprint"": {
    ""chip"": ""Apple M3 Pro"",
    ""memory_gb"": 18,
    ""model_identifier"": ""MacBookPro18,1""
  },
  ""assessment_results"": {
    ""suite"": ""full"",
    ""duration_seconds"": 45,
    ""timestamp"": ""2026-08-13T00:00:00Z""
  },
  ""tier_recommendations"": {
    ""high"": {
      ""reason"": ""Maximum performance"",
      ""system"": { ""iogpu.wired_limit_mb"": 6144 },
      ""omlx"": { ""memory_guard_tier"": ""high"" }
    }
  }
}";

    /// <summary>
    /// Get the fixture by suite type and size.
    /// </summary>
    public static string GetFixture(string suite, string size) =>
        (suite, size) switch
        {
            ("smoke", "small") => SmokeSuiteMinimalManifest,
            ("full", "small") => Llama7bRecommendationManifest,
            ("full", "large") => Ornith35bRecommendationManifest,
            _ => throw new ArgumentException($"Unknown fixture: suite={suite}, size={size}")
        };
}
