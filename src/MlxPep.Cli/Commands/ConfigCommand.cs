using System.Text.Json;
using System.Text.Json.Serialization;

namespace MlxPep.Cli.Commands;

/// <summary>
/// Handler for `mlx-pep config` command.
/// Displays current environment configuration for OMLX connectivity.
/// API keys are masked for security; URLs are displayed in full.
/// </summary>
public class ConfigCommand
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        try
        {
            context.Verbose("ConfigCommand", "Starting config command.");

            var baseUrl = Environment.GetEnvironmentVariable("OMLX_BASE_URL");
            var apiKey = Environment.GetEnvironmentVariable("OMLX_API_KEY");

            if (context.JsonOutput)
            {
                context.Verbose("ConfigCommand", "JSON output branch selected for config command.");
                var json = FormatAsJson(baseUrl, apiKey);
                Console.WriteLine(json);
            }
            else
            {
                context.Verbose("ConfigCommand", "Text output branch selected for config command.");
                var table = FormatAsTable(baseUrl, apiKey);
                Console.WriteLine(table);
            }

            return CommandResult.Success();
        }
        catch (Exception ex)
        {
            context.Verbose("ConfigCommand", $"Config command failed with {ex.GetType().Name}: {ex.Message}");
            return CommandResult.Failure($"Config check failed: {ex.Message}");
        }
        finally
        {
            context.Verbose("ConfigCommand", "Config command finished execution path.");
        }
    }

    private string FormatAsJson(string? baseUrl, string? apiKey)
    {
        var config = new Dictionary<string, object?>
        {
            { "OMLX_BASE_URL", baseUrl ?? "(not set)" },
            { "OMLX_API_KEY", MaskApiKey(apiKey) }
        };

        var result = new Dictionary<string, object>
        {
            { "command", "config" },
            { "timestamp", DateTime.UtcNow.ToString("O") },
            { "environment", config }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(result, options);
    }

    private string FormatAsTable(string? baseUrl, string? apiKey)
    {
        var lines = new List<string>();
        lines.Add("mlx-pep configuration");
        lines.Add("");
        lines.Add("Environment Variables:");
        lines.Add("");
        
        // Display OMLX_BASE_URL
        var displayUrl = baseUrl ?? "(not set)";
        lines.Add($"  OMLX_BASE_URL:  {displayUrl}");
        
        // Display OMLX_API_KEY (masked)
        var displayKey = MaskApiKey(apiKey);
        lines.Add($"  OMLX_API_KEY:   {displayKey}");
        
        lines.Add("");

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            lines.Add("⚠️  Some environment variables are not set.");
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                lines.Add("  • OMLX_BASE_URL will default to http://127.0.0.1:8000");
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                lines.Add("  • OMLX_API_KEY is required for API access. Set it before running commands.");
            }
        }
        else
        {
            lines.Add("✓ All required environment variables are configured.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Masks an API key for safe display.
    /// Shows first 4 and last 4 characters, with ellipsis in between.
    /// Returns appropriate message if key is null or too short.
    /// </summary>
    private string MaskApiKey(string? apiKey)
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
}
