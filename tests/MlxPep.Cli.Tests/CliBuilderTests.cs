namespace MlxPep.Cli.Tests;

using MlxPep.Cli;

public class CliBuilderTests
{
    [Fact]
    public void ParseInvocation_StripsGlobalFlags_AndBuildsSharedContext()
    {
        var invocation = CliRuntime.ParseInvocation([
            "--verbose",
            "models",
            "list",
            "--json",
            "--progress"
        ]);

        Assert.Equal(["models", "list"], invocation.CommandArgs);
        Assert.True(invocation.Context.JsonOutput);
        Assert.True(invocation.Context.VerboseOutput);
        Assert.True(invocation.Context.ProgressOutput);
    }
}