namespace MlxPep.Cli.Commands;

using MlxPep.Core;

public static class InteractiveResultsBrowser
{
    public static void Run()
    {
        DisplayConfigurationHeader();

        while (true)
        {
            var store = new AssessmentRunStore();
            var runs = store.ListRuns(requireVerifiedComplete: true);
            var models = runs
                .GroupBy(run => run.ModelId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Console.Clear();
            DisplayConfigurationHeader();
            Console.WriteLine("mlx-pep results browser");
            Console.WriteLine();

            if (models.Count == 0)
            {
                Console.WriteLine("No verified local assessment runs found.");
                Console.WriteLine("Press A to run an assessment, or Q to quit.");
                var emptyInput = Console.ReadLine();
                if (string.Equals(emptyInput, "A", StringComparison.OrdinalIgnoreCase))
                {
                    RunAssessmentInteractive();
                    continue;
                }

                return;
            }

            for (var index = 0; index < models.Count; index++)
            {
                var latest = models[index].OrderByDescending(run => run.CreatedAt, StringComparer.Ordinal).First();
                Console.WriteLine($"{index + 1}. {models[index].Key}  ({models[index].Count()} complete run(s), latest {latest.CreatedAt})");
            }

            Console.WriteLine();
            Console.WriteLine("Select a model number, or type Q to quit.");
            var input = Console.ReadLine();
            if (string.Equals(input, "Q", StringComparison.OrdinalIgnoreCase))
                return;

            if (!int.TryParse(input, out var selectedIndex) || selectedIndex < 1 || selectedIndex > models.Count)
                continue;

            BrowseModel(store, models[selectedIndex - 1].Key);
        }
    }

    private static void DisplayConfigurationHeader()
    {
        var baseUrl = Environment.GetEnvironmentVariable("OMLX_BASE_URL") ?? "(not set)";
        var apiKey = Environment.GetEnvironmentVariable("OMLX_API_KEY");
        var displayKey = MaskApiKey(apiKey);

        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║ oMLX Configuration                                             ║");
        Console.WriteLine($"║ URL:  {baseUrl,-58} ║");
        Console.WriteLine($"║ Key:  {displayKey,-58} ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "(not set)";
        }

        if (apiKey.Length <= 8)
        {
            return "***" + new string('*', Math.Max(0, apiKey.Length - 3));
        }

        var first4 = apiKey[..4];
        var last4 = apiKey[^4..];
        var maskedLength = apiKey.Length - 8;

        return $"{first4}{'*' * maskedLength}{last4}";
    }

    private static void BrowseModel(AssessmentRunStore store, string modelId)
    {
        while (true)
        {
            var runs = store.ListRuns(requireVerifiedComplete: true, modelId: modelId).ToList();
            var latest = runs.First();
            Console.Clear();
            DisplayConfigurationHeader();
            Console.WriteLine($"Model: {modelId}");
            Console.WriteLine($"Latest complete run: {latest.RunId}");
            Console.WriteLine();
            Console.WriteLine("A) View results");
            Console.WriteLine("B) Save results as markdown/json");
            Console.WriteLine("C) Run assessment");
            Console.WriteLine("D) List local complete runs for this model");
            Console.WriteLine("E) Back");
            Console.WriteLine();

            var input = Console.ReadLine();
            if (string.Equals(input, "E", StringComparison.OrdinalIgnoreCase))
                return;

            if (string.Equals(input, "A", StringComparison.OrdinalIgnoreCase))
            {
                Console.Clear();
                Console.WriteLine(store.RenderRunSummaryMarkdown(latest));
                Pause();
                continue;
            }

            if (string.Equals(input, "B", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write("Format (markdown/json): ");
                var format = Console.ReadLine();
                Console.Write("Output path: ");
                var outputPath = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(outputPath))
                {
                    var exporter = new ResultsExportCommand();
                    exporter.ExecuteAsync(new CommandContext(false), outputPath, latest.RunId, format: string.IsNullOrWhiteSpace(format) ? "markdown" : format).GetAwaiter().GetResult();
                }
                Pause();
                continue;
            }

            if (string.Equals(input, "C", StringComparison.OrdinalIgnoreCase))
            {
                RunAssessmentInteractive(modelId);
                continue;
            }

            if (string.Equals(input, "D", StringComparison.OrdinalIgnoreCase))
            {
                Console.Clear();
                Console.WriteLine(store.RenderRunListMarkdown(runs));
                Pause();
            }
        }
    }

    private static void RunAssessmentInteractive(string? defaultModelId = null)
    {
        Console.Write($"HF model id [{defaultModelId ?? string.Empty}]: ");
        var hfIdInput = Console.ReadLine();
        var hfId = string.IsNullOrWhiteSpace(hfIdInput) ? defaultModelId : hfIdInput;
        if (string.IsNullOrWhiteSpace(hfId))
            return;

        Console.Write("Suite (smoke/full) [smoke]: ");
        var suite = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(suite))
            suite = "smoke";

        Console.Write("Assistant model id (optional): ");
        var assistantModelId = Console.ReadLine();

        var command = new AssessCommand();
        var result = command.ExecuteAsync(hfId, string.IsNullOrWhiteSpace(assistantModelId) ? null : assistantModelId, suite, publish: false, context: new CommandContext(false)).GetAwaiter().GetResult();
        Console.WriteLine(result.Message ?? (result.ExitCode == 0 ? "Assessment completed." : "Assessment failed."));
        Pause();
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}
