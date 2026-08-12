namespace MlxPep.Core;

public record Model(
    string RepoId,
    string Revision,
    long SizeBytes,
    DateTime LastModified
)
{
    public string GetSize() => FormatBytes(SizeBytes);

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
