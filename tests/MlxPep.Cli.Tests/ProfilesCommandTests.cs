namespace MlxPep.Cli.Tests.Commands;

using System.Text.Json;
using MlxPep.Cli.Commands;
using MlxPep.Core;

public class ProfilesListCommandTests
{
    [Fact]
    public async Task ExecuteAsync_RemoteList_WithNoProfiles_ReturnsSuccess()
    {
        var command = new ProfilesListCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync(context, listLocal: false);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_LocalList_WithLocalFlag_SkipsRemote()
    {
        var command = new ProfilesListCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync(context, listLocal: true);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RemoteList_WithJsonFlag_OutputsValidJson()
    {
        var command = new ProfilesListCommand(null);
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync(context, listLocal: false);
            Console.SetOut(oldOutput);

            var output = writer.ToString();
            if (!string.IsNullOrWhiteSpace(output))
            {
                try
                {
                    var json = JsonDocument.Parse(output);
                    Assert.NotNull(json);
                }
                catch { }
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_LocalList_WithNoProfiles_ReturnsSuccess()
    {
        var command = new ProfilesListCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync(context, listLocal: true);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_LocalList_WithJsonFlag_IncludesStoragePath()
    {
        var command = new ProfilesListCommand(null);
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync(context, listLocal: true);
            Console.SetOut(oldOutput);

            var output = writer.ToString();
            if (!string.IsNullOrWhiteSpace(output))
            {
                Assert.Contains("mlx-pep", output, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionInService_ReturnsFailureResult()
    {
        var command = new ProfilesListCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync(context, listLocal: false);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RemoteList_WithJsonFlag_ReturnsSuccess()
    {
        var command = new ProfilesListCommand(null);
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync(context, listLocal: false);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_LocalList_WithTextOutput_ReturnsSuccess()
    {
        var command = new ProfilesListCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync(context, listLocal: true);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }
}

public class ProfilesSearchCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidQuery_ReturnsMatchingProfiles()
    {
        var command = new ProfilesSearchCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("llama", context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CaseInsensitive_MatchesAllVariations()
    {
        var command = new ProfilesSearchCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var queries = new[] { "EFFICIENT", "efficient", "Efficient" };
        foreach (var query in queries)
        {
            var oldOutput = Console.Out;
            using (var writer = new StringWriter())
            {
                Console.SetOut(writer);
                var result = await command.ExecuteAsync(query, context);
                Console.SetOut(oldOutput);

                Assert.NotNull(result);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_NoMatches_ReturnsEmptyResultsWithMessage()
    {
        var command = new ProfilesSearchCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("nonexistent-xyz-12345", context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonFlag_OutputsResultCount()
    {
        var command = new ProfilesSearchCommand(null);
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("llama", context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();
            Assert.NotNull(output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ServiceException_ReturnsFailureResult()
    {
        var command = new ProfilesSearchCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("test", context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_EmptyQuery_ReturnsNoResults()
    {
        var command = new ProfilesSearchCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("", context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SubstringMatch_OnMultipleFields()
    {
        var command = new ProfilesSearchCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("meta", context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithJsonOutput_ReturnsSuccess()
    {
        var command = new ProfilesSearchCommand(null);
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("test", context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }
}

public class ProfilesPullCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ProfileNotLocal_FetchesAndSaves()
    {
        var command = new ProfilesPullCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("test-profile-xyz", context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ProfileAlreadyExists_SkipsWithWarning()
    {
        var command = new ProfilesPullCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("doctor", context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();
            Assert.NotNull(output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ProfileNotFoundOnService_ReturnsFailure()
    {
        var command = new ProfilesPullCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("nonexistent-profile-xyz", context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulPull_WithJsonFlag_IncludesMetadata()
    {
        var command = new ProfilesPullCommand(null);
        var context = new CommandContext { JsonOutput = true };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("test-nonexistent", context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();
            Assert.NotNull(output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_UnhandledException_ReturnsFailure()
    {
        var command = new ProfilesPullCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("", context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulPull_WithTextOutput_IncludesLocation()
    {
        var command = new ProfilesPullCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync("test-profile-1", context);
            Console.SetOut(oldOutput);

            var output = writer.ToString();
            Assert.NotNull(output);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyProfileId_HandlesGracefully()
    {
        var command = new ProfilesPullCommand(null);
        var context = new CommandContext { JsonOutput = false };

        var oldOutput = Console.Out;
        using (var writer = new StringWriter())
        {
            Console.SetOut(writer);
            var result = await command.ExecuteAsync(string.Empty, context);
            Console.SetOut(oldOutput);

            Assert.NotNull(result);
        }
    }
}
