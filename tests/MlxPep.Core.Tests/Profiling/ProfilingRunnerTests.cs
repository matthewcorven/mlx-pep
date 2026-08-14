namespace MlxPep.Core.Tests.Profiling;

using System;
using System.Threading.Tasks;
using Xunit;
using MlxPep.Core.Profiling;

public class ProfilingRunnerTests
{
    private readonly ProfilingRunner _runner = new();

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalseWhenPythonNotAvailable()
    {
        // Act
        var available = await _runner.IsAvailableAsync();

        // Assert - in test environment, model-assessor won't be available
        Assert.False(available);
    }

    [Fact]
    public async Task RunProfilingAsync_WithNullModelHfId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _runner.RunProfilingAsync(null!));
    }

    [Fact]
    public async Task RunProfilingAsync_WithEmptyModelHfId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _runner.RunProfilingAsync(string.Empty));
    }

    [Fact]
    public async Task RunProfilingAsync_WithWhitespaceModelHfId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _runner.RunProfilingAsync("   "));
    }

    [Fact]
    public async Task RunProfilingAsync_WithValidModelId_HandlesGracefully()
    {
        // When model-assessor is not available, should throw InvalidOperationException or timeout
        // (implementation doesn't validate suite parameter, so it just tries to run the subprocess)
        var exception = await Record.ExceptionAsync(
            () => _runner.RunProfilingAsync("test/model", null, "full"));
        
        // Should have some exception (either timeout or process failure)
        Assert.NotNull(exception);
    }
}
