namespace MlxPep.Core.Tests;

using Xunit;

public class ProfilingRunnerTests
{
    [Fact]
    public async Task FixtureProfilingRunner_ReturnsManifestWithThreeTiers()
    {
        // Arrange
        var runner = new FixtureProfilingRunner();

        // Act
        var manifest = await runner.RunProfilingAsync("meta-llama/Llama-2-7b");

        // Assert
        Assert.NotNull(manifest);
        Assert.Equal("meta-llama/Llama-2-7b", manifest.ModelHfId);
        Assert.NotNull(manifest.Recommendations);
        Assert.Equal(3, manifest.Recommendations.Count);
    }

    [Fact]
    public async Task FixtureProfilingRunner_IncludesHighPerformanceTier()
    {
        // Arrange
        var runner = new FixtureProfilingRunner();

        // Act
        var manifest = await runner.RunProfilingAsync("meta-llama/Llama-2-7b");

        // Assert
        var highPerf = manifest.Recommendations.FirstOrDefault(r => r.Tier == "high-performance");
        Assert.NotNull(highPerf);
        Assert.NotNull(highPerf.OMLXSettings);
        Assert.True(highPerf.OMLXSettings.ContainsKey("compute_units"));
        Assert.Equal("ALL", highPerf.OMLXSettings["compute_units"]);
    }

    [Fact]
    public async Task FixtureProfilingRunner_IncludesBalancedTier()
    {
        // Arrange
        var runner = new FixtureProfilingRunner();

        // Act
        var manifest = await runner.RunProfilingAsync("meta-llama/Llama-2-7b");

        // Assert
        var balanced = manifest.Recommendations.FirstOrDefault(r => r.Tier == "balanced");
        Assert.NotNull(balanced);
        Assert.NotNull(balanced.OMLXSettings);
        Assert.True(balanced.OMLXSettings.ContainsKey("compute_units"));
        Assert.Equal("GPU", balanced.OMLXSettings["compute_units"]);
    }

    [Fact]
    public async Task FixtureProfilingRunner_IncludesEfficientTier()
    {
        // Arrange
        var runner = new FixtureProfilingRunner();

        // Act
        var manifest = await runner.RunProfilingAsync("meta-llama/Llama-2-7b");

        // Assert
        var efficient = manifest.Recommendations.FirstOrDefault(r => r.Tier == "efficient");
        Assert.NotNull(efficient);
        Assert.NotNull(efficient.OMLXSettings);
        Assert.True(efficient.OMLXSettings.ContainsKey("compute_units"));
        Assert.Equal("CPU_AND_GPU", efficient.OMLXSettings["compute_units"]);
    }

    [Fact]
    public async Task FixtureProfilingRunner_IncludesHarnessSettings()
    {
        // Arrange
        var runner = new FixtureProfilingRunner();

        // Act
        var manifest = await runner.RunProfilingAsync("meta-llama/Llama-2-7b");

        // Assert
        foreach (var rec in manifest.Recommendations)
        {
            Assert.NotNull(rec.HarnessSettings);
            Assert.True(rec.HarnessSettings.ContainsKey("maxInputTokens"));
            Assert.True(rec.HarnessSettings.ContainsKey("maxOutputTokens"));
        }
    }

    [Fact]
    public async Task FixtureProfilingRunner_IncludesEvidence()
    {
        // Arrange
        var runner = new FixtureProfilingRunner();

        // Act
        var manifest = await runner.RunProfilingAsync("meta-llama/Llama-2-7b");

        // Assert
        var highPerf = manifest.Recommendations.First(r => r.Tier == "high-performance");
        Assert.NotNull(highPerf.Evidence);
        Assert.True(highPerf.Evidence.Throughput > 0);
        Assert.True(highPerf.Evidence.Latency > 0);
        Assert.True(highPerf.Evidence.MemoryPeak > 0);
    }

    [Fact]
    public async Task FixtureProfilingRunner_AcceptsOptionalParameters()
    {
        // Arrange
        var runner = new FixtureProfilingRunner();

        // Act
        var manifest = await runner.RunProfilingAsync("meta-llama/Llama-2-7b", "gpt-4", "full");

        // Assert
        Assert.NotNull(manifest);
        Assert.Equal(3, manifest.Recommendations.Count);
    }

    [Fact]
    public async Task FixtureProfilingRunner_HighPerfHasBestThroughput()
    {
        // Arrange
        var runner = new FixtureProfilingRunner();

        // Act
        var manifest = await runner.RunProfilingAsync("meta-llama/Llama-2-7b");

        // Assert
        var high = manifest.Recommendations.First(r => r.Tier == "high-performance").Evidence!.Throughput;
        var balanced = manifest.Recommendations.First(r => r.Tier == "balanced").Evidence!.Throughput;
        var efficient = manifest.Recommendations.First(r => r.Tier == "efficient").Evidence!.Throughput;

        Assert.True(high > balanced && balanced > efficient);
    }
}
