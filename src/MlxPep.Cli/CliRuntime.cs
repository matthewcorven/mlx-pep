using System.Diagnostics;
using MlxPep.Cli.Commands;

namespace MlxPep.Cli;

public sealed record ParsedCliInvocation(string[] CommandArgs, Commands.CommandContext Context);

public static class CliRuntime
{
    private static readonly object SyncRoot = new();
    private static bool _verboseTraceEnabled;

    public static ParsedCliInvocation ParseInvocation(string[] args)
    {
        var isJson = args.Contains("--json", StringComparer.Ordinal);
        var verbose = args.Contains("--verbose", StringComparer.Ordinal);
        var progress = args.Contains("--progress", StringComparer.Ordinal);
        var filteredArgs = args
            .Where(arg => !string.Equals(arg, "--json", StringComparison.Ordinal)
                && !string.Equals(arg, "--verbose", StringComparison.Ordinal)
                && !string.Equals(arg, "--progress", StringComparison.Ordinal))
            .ToArray();

        var context = new Commands.CommandContext(isJson, verbose, progress);
        return new ParsedCliInvocation(filteredArgs, context);
    }

    public static void EnsureVerboseTraceListener()
    {
        lock (SyncRoot)
        {
            if (_verboseTraceEnabled)
            {
                return;
            }

            Trace.Listeners.Add(new ConsoleErrorTraceListener());
            Trace.AutoFlush = true;
            _verboseTraceEnabled = true;
        }
    }

    public static void WriteVerbose(string source, string message)
    {
        Console.Error.WriteLine($"[verbose][{source}] {message}");
    }

    public static void WriteProgress(string operation, int stepNumber, int totalSteps, double workPercent, string detail)
    {
        var safeTotalSteps = Math.Max(totalSteps, 1);
        var safeStepNumber = Math.Max(Math.Min(stepNumber, safeTotalSteps), 1);
        var safeWorkPercent = Math.Clamp(workPercent, 0, 100);
        var completedUnits = (safeStepNumber - 1) + (safeWorkPercent / 100d);
        var overallPercent = Math.Clamp((completedUnits / safeTotalSteps) * 100d, 0, 100);

        Console.Error.WriteLine($"[progress][{operation}] overall {overallPercent,6:0.0}% ({safeStepNumber}/{safeTotalSteps}) work {safeWorkPercent,6:0.0}% {detail}");
    }
}

internal sealed class ConsoleErrorTraceListener : TraceListener
{
    public override void Write(string? message)
    {
        Console.Error.Write(message);
    }

    public override void WriteLine(string? message)
    {
        Console.Error.WriteLine(message);
    }
}

public sealed class CliProgressScope : IDisposable
{
    private readonly bool _enabled;
    private readonly string _operation;
    private readonly int _totalSteps;
    private readonly Action<ProgressUpdate>? _progressCallback;
    private int _currentStep;
    private string _currentTitle = "idle";
    private bool _disposed;

    public CliProgressScope(bool enabled, string operation, int totalSteps, Action<ProgressUpdate>? progressCallback = null)
    {
        _enabled = enabled;
        _operation = operation;
        _totalSteps = Math.Max(totalSteps, 1);
        _progressCallback = progressCallback;
    }

    public void StartStep(string title)
    {
        _currentStep = Math.Min(_currentStep + 1, _totalSteps);
        _currentTitle = title;
        NotifyProgress(0, $"{title} started");
    }

    public void ReportWork(double workPercent, string? detail = null)
    {
        if (_currentStep > 0)
        {
            NotifyProgress(workPercent, detail ?? _currentTitle);
        }
    }

    public void CompleteStep(string? detail = null)
    {
        if (_currentStep > 0)
        {
            NotifyProgress(100, detail ?? $"{_currentTitle} complete");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_currentStep > 0)
        {
            NotifyProgress(100, "operation finished");
        }
    }

    private void NotifyProgress(double workPercent, string detail)
    {
        if (!_enabled)
        {
            return;
        }

        var safeWorkPercent = Math.Clamp(workPercent, 0, 100);
        var safeStepNumber = Math.Max(Math.Min(_currentStep, _totalSteps), 1);
        var completedUnits = (safeStepNumber - 1) + (safeWorkPercent / 100d);
        var overallPercent = Math.Clamp((completedUnits / _totalSteps) * 100d, 0, 100);

        var progress = new ProgressUpdate(_operation, safeStepNumber, _totalSteps, safeWorkPercent, overallPercent, detail);
        _progressCallback?.Invoke(progress);
        CliRuntime.WriteProgress(_operation, safeStepNumber, _totalSteps, safeWorkPercent, detail);
    }
}