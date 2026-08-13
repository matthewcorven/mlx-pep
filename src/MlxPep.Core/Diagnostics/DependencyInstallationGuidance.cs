namespace MlxPep.Core.Diagnostics;

/// <summary>
/// Generates installation guidance for missing dependencies.
/// </summary>
public static class DependencyInstallationGuidance
{
    public static string? GetGuidance(string toolName, string? scope = null)
    {
        return toolName switch
        {
            "dotnet" => GetDotnetGuidance(),
            "hf-cli" => GetHfCliGuidance(),
            "python3" => GetPython3Guidance(),
            "model-assessor" => GetModelAssessorGuidance(),
            "omlx" => GetOmlxGuidance(),
            "vscode" => GetVsCodeGuidance(),
            "vscode-insiders" => GetVsCodeInsidersGuidance(),
            "copilot-cli" => GetCopilotCliGuidance(),
            _ => null
        };
    }

    private static string GetDotnetGuidance()
    {
        return "Install .NET 10.0:\n" +
               "  macOS: brew install dotnet\n" +
               "  Or download from: https://dotnet.microsoft.com/download";
    }

    private static string GetHfCliGuidance()
    {
        return "Install Hugging Face CLI:\n" +
               "  pip install huggingface-hub\n" +
               "  Then run: huggingface-cli login";
    }

    private static string GetPython3Guidance()
    {
        return "Install Python 3:\n" +
               "  macOS: brew install python3\n" +
               "  Or download from: https://www.python.org/downloads/";
    }

    private static string GetModelAssessorGuidance()
    {
        return "Install model-assessor package:\n" +
               "  pip install model-assessor\n" +
               "  (Requires python3 and pip)";
    }

    private static string GetOmlxGuidance()
    {
        return "Install oMLX:\n" +
               "  Download from: https://github.com/mlx-community/oMLX/releases\n" +
               "  Or install via: brew install omlx (if available)";
    }

    private static string GetVsCodeGuidance()
    {
        return "Install VS Code:\n" +
               "  macOS: brew install visual-studio-code\n" +
               "  Or download from: https://code.visualstudio.com/";
    }

    private static string GetVsCodeInsidersGuidance()
    {
        return "Install VS Code Insiders:\n" +
               "  macOS: brew install visual-studio-code-insiders\n" +
               "  Or download from: https://code.visualstudio.com/insiders/";
    }

    private static string GetCopilotCliGuidance()
    {
        return "Install GitHub Copilot CLI:\n" +
               "  brew install github/gh/gh\n" +
               "  gh extension install github/gh-copilot\n" +
               "  Then run: gh auth login";
    }
}
