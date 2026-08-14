using MlxPep.Core.Diagnostics;

namespace MlxPep.Core.Tests.Diagnostics;

/// <summary>
/// Mock probe for testing that returns pre-configured results.
/// </summary>
public class MockProbe : IDependencyProbe
{
    private readonly string? _rawOutput;
    private readonly bool _found;
    private readonly string? _error;
    private readonly Func<string, string?>? _versionParser;

    public MockProbe(bool found, string? rawOutput = null, string? error = null, Func<string, string?>? versionParser = null)
    {
        _found = found;
        _rawOutput = rawOutput;
        _error = error;
        _versionParser = versionParser;
    }

    public Task<ProbeResult> ProbeAsync()
    {
        return Task.FromResult(new ProbeResult
        {
            Found = _found,
            RawOutput = _rawOutput,
            Error = _error
        });
    }

    public string? ParseVersion(string rawOutput)
    {
        if (_versionParser != null)
            return _versionParser(rawOutput);

        // Default: extract semantic version using regex (like real probes)
        if (string.IsNullOrWhiteSpace(rawOutput))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(rawOutput, @"(\d+\.\d+\.\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
