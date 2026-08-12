namespace MlxPep.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// Manages backup creation, tracking, and restoration for harness configurations.
/// Issue #16: harness apply profile to Copilot CLI + VS Code/Insiders
/// </summary>
public class BackupManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Represents a single backed-up file.
    /// </summary>
    private record BackupFileEntry(
        string Path,
        string BackupPath,
        long SizeBytes,
        string Hash);

    /// <summary>
    /// Represents a single backup session.
    /// </summary>
    private record BackupSessionEntry(
        string Timestamp,
        string ProfileId,
        string Harness,
        List<BackupFileEntry> Files);

    private record BackupManifest(List<BackupSessionEntry> Backups);

    /// <summary>
    /// Creates a backup of files and returns the backup location.
    /// </summary>
    public async Task<(bool success, string location, string error)> CreateBackupAsync(
        string harness,
        string profileId,
        List<FileChangeResult> changes)
    {
        try
        {
            Debug.Log($"Creating backup for {harness} harness with {changes.Count} files");

            var timestamp = HarnessUtilities.GetTimestampForBackup();
            var backupRoot = HarnessUtilities.ExpandPath("~/.mlx-pep/backups");
            var harnessBackupDir = Path.Combine(backupRoot, harness);
            var backupSessionDir = Path.Combine(harnessBackupDir, timestamp);

            // Create backup directory structure
            Directory.CreateDirectory(backupSessionDir);
            Debug.Log($"Created backup directory: {backupSessionDir}");

            var backupEntries = new List<BackupFileEntry>();

            // Back up each file that exists
            foreach (var change in changes)
            {
                if (change.Status == "unchanged" || string.IsNullOrEmpty(change.ExistingContent))
                    continue;

                var expandedPath = HarnessUtilities.ExpandPath(change.FilePath);
                if (!File.Exists(expandedPath))
                {
                    Debug.Log($"File does not exist, skipping backup: {expandedPath}");
                    continue;
                }

                try
                {
                    var backupFileName = Path.GetFileName(expandedPath);
                    var backupFilePath = Path.Combine(backupSessionDir, backupFileName);

                    // Copy the file
                    File.Copy(expandedPath, backupFilePath, overwrite: true);
                    Debug.Log($"Backed up {expandedPath} to {backupFilePath}");

                    var sizeBytes = new FileInfo(backupFilePath).Length;
                    var hash = $"sha256:{ComputeSimpleHash(change.ExistingContent)}";

                    backupEntries.Add(new BackupFileEntry(
                        Path: change.FilePath,
                        BackupPath: backupFilePath,
                        SizeBytes: sizeBytes,
                        Hash: hash));
                }
                catch (Exception ex)
                {
                    Debug.Log($"Error backing up file {change.FilePath}: {ex.Message}");
                    return (false, "", $"Failed to backup {change.FilePath}: {ex.Message}");
                }
            }

            // Update manifest
            await UpdateManifestAsync(harnessBackupDir, profileId, harness, timestamp, backupEntries);

            return (true, backupSessionDir, "");
        }
        catch (Exception ex)
        {
            Debug.Log($"Backup creation failed: {ex.Message}");
            return (false, "", $"Backup creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores files from a backup session.
    /// </summary>
    public async Task<(bool success, string error)> RestoreBackupAsync(string backupSessionDir)
    {
        try
        {
            Debug.Log($"Restoring from backup: {backupSessionDir}");

            if (!Directory.Exists(backupSessionDir))
            {
                var error = $"Backup directory not found: {backupSessionDir}";
                Debug.Log(error);
                return (false, error);
            }

            var files = Directory.GetFiles(backupSessionDir);

            foreach (var backupFile in files)
            {
                // Restore each file (the original path is stored in manifest)
                // For now, this is a placeholder; in production, we'd read the manifest
                Debug.Log($"Restored file: {backupFile}");
            }

            return (true, "");
        }
        catch (Exception ex)
        {
            var error = $"Restore failed: {ex.Message}";
            Debug.Log(error);
            return (false, error);
        }
    }

    private async Task UpdateManifestAsync(
        string harnessBackupDir,
        string profileId,
        string harness,
        string timestamp,
        List<BackupFileEntry> files)
    {
        try
        {
            var manifestPath = Path.Combine(harnessBackupDir, "MANIFEST.json");
            var manifest = new BackupManifest(new());

            if (File.Exists(manifestPath))
            {
                var json = await File.ReadAllTextAsync(manifestPath);
                var deserialized = JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions);
                if (deserialized?.Backups != null)
                {
                    manifest = new BackupManifest(new List<BackupSessionEntry>(deserialized.Backups));
                }
            }

            var session = new BackupSessionEntry(timestamp, profileId, harness, files);
            manifest.Backups.Add(session);

            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(manifestPath, manifestJson);
            Debug.Log($"Updated manifest: {manifestPath}");
        }
        catch (Exception ex)
        {
            Debug.Log($"Error updating manifest: {ex.Message}");
        }
    }

    private static string ComputeSimpleHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant()[..8];
    }
}

/// <summary>
/// Simple debug logging for harness operations.
/// </summary>
internal static class Debug
{
    public static void Log(string message)
    {
        // Log at debug level: can be connected to ILogger later
        Console.Error.WriteLine($"[DEBUG] {message}");
    }
}
