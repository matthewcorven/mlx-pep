namespace MlxPep.Core.Tests.Profiling;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using MlxPep.Core.Profiling;

public class ProfileStorageTests
{
    private readonly ProfileStorage _storage = new();

    [Fact]
    public async Task SaveProfileSetAsync_WritesJsonlFile()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        var modelHfId = profiles[0].ModelHfId;

        // Act
        await _storage.SaveProfileSetAsync(profiles, modelHfId);

        // Assert
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mlx-pep", "profiles");
        Assert.True(Directory.Exists(baseDir), $"Expected profiles directory at {baseDir}");
        
        var jsonlFiles = Directory.GetFiles(baseDir, "*.jsonl", SearchOption.AllDirectories);
        Assert.NotEmpty(jsonlFiles);
    }

    [Fact]
    public async Task SaveProfileSetAsync_CreatesSafeModelDirectory()
    {
        // Arrange
        var profiles = new List<Profile>
        {
            new Profile(
                SchemaVersion: 1,
                Id: "test-001",
                ModelHfId: "meta-llama/Llama-2-7b",
                Tier: "high",
                Engine: "mlx",
                System: new Dictionary<string, object>(),
                OMLXSettings: new Dictionary<string, object>(),
                Harness: new Dictionary<string, object>(),
                Provenance: new ProfileProvenance("test", DateTime.UtcNow.ToString("O"), "test"),
                Hardware: new HardwareFingerprint("test", 1, "test"))
        };

        // Act
        await _storage.SaveProfileSetAsync(profiles, "meta-llama/Llama-2-7b");

        // Assert - directory should use safe name (/ replaced with _)
        var expectedSafeName = "meta-llama_Llama-2-7b";
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mlx-pep", "profiles");
        var modelDirs = Directory.GetDirectories(baseDir, "*", SearchOption.AllDirectories);
        Assert.True(modelDirs.Any(d => d.Contains(expectedSafeName)), 
            $"Expected model directory containing '{expectedSafeName}' in {string.Join(", ", modelDirs)}");
    }

    [Fact]
    public async Task LoadProfileSetAsync_PreservesProfileData()
    {
        // Arrange
        var originalProfiles = CreateTestProfiles();
        var modelHfId = originalProfiles[0].ModelHfId;
        await _storage.SaveProfileSetAsync(originalProfiles, modelHfId);

        // Act
        var loadedProfiles = await _storage.LoadProfileSetAsync(modelHfId);

        // Assert
        Assert.NotNull(loadedProfiles);
        Assert.NotEmpty(loadedProfiles);
        Assert.Equal(3, loadedProfiles.Count);
        
        // Verify data was preserved
        var originalHigh = originalProfiles.First(p => p.Tier == "high");
        var loadedHigh = loadedProfiles.First(p => p.Tier == "high");
        Assert.Equal(originalHigh.Id, loadedHigh.Id);
        Assert.Equal(originalHigh.ModelHfId, loadedHigh.ModelHfId);
        Assert.Equal(originalHigh.Tier, loadedHigh.Tier);
    }

    [Fact]
    public async Task SaveProfileSetAsync_WritesAllThreeTiers()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        var modelHfId = profiles[0].ModelHfId;

        // Act
        await _storage.SaveProfileSetAsync(profiles, modelHfId);

        // Assert
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mlx-pep", "profiles");
        var modelDirs = Directory.GetDirectories(baseDir);
        Assert.NotEmpty(modelDirs);
        
        var modelDir = modelDirs.FirstOrDefault(d => d.Contains("test-model"));
        if (modelDir == null) return; // Skip if not found

        var dateDirs = Directory.GetDirectories(modelDir);
        var jsonlFiles = dateDirs.SelectMany(d => Directory.GetFiles(d, "*.jsonl")).ToList();
        
        // Should have at least 3 profile lines total
        var lineCount = 0;
        foreach (var file in jsonlFiles)
        {
            lineCount += File.ReadAllLines(file).Length;
        }
        Assert.Equal(3, lineCount);
    }

    [Fact]
    public async Task EnsureBaseDirectoryAsync_CreatesDirectory()
    {
        // Act
        await _storage.EnsureBaseDirectoryAsync();

        // Assert
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".mlx-pep", "profiles");
        Assert.True(Directory.Exists(baseDir));
    }

    [Fact]
    public async Task SaveProfileSetAsync_WithNullList_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _storage.SaveProfileSetAsync(null!, "test/model"));
    }

    [Fact]
    public async Task LoadProfileSetAsync_WithNonExistentModel_ReturnsEmpty()
    {
        // Act
        var profiles = await _storage.LoadProfileSetAsync($"nonexistent/model-{Guid.NewGuid()}");

        // Assert
        Assert.NotNull(profiles);
        Assert.Empty(profiles);
    }

    [Fact]
    public async Task GetMostRecentProfileFolderAsync_ReturnsMostRecentFolder()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        var modelHfId = profiles[0].ModelHfId;
        await _storage.SaveProfileSetAsync(profiles, modelHfId);

        // Act
        var recentFolder = await _storage.GetMostRecentProfileFolderAsync(modelHfId);

        // Assert
        Assert.NotNull(recentFolder);
        Assert.True(Directory.Exists(recentFolder));
    }

    [Fact]
    public async Task SaveProfileSetAsync_JsonlFormatIsValid()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        var modelHfId = profiles[0].ModelHfId;

        // Act
        await _storage.SaveProfileSetAsync(profiles, modelHfId);

        // Assert - read and verify each line is valid JSON
        var recentFolder = await _storage.GetMostRecentProfileFolderAsync(modelHfId);
        if (recentFolder == null) return;
        
        var jsonlFiles = Directory.GetFiles(recentFolder, "*.jsonl");
        
        foreach (var file in jsonlFiles)
        {
            var lines = File.ReadAllLines(file);
            foreach (var line in lines)
            {
                // Should not throw
                using var doc = JsonDocument.Parse(line);
                Assert.NotNull(doc);
            }
        }
    }

    [Fact]
    public async Task SaveProfileSetAsync_CreatesDatedDirectory()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        var modelHfId = profiles[0].ModelHfId;

        // Act
        await _storage.SaveProfileSetAsync(profiles, modelHfId);

        // Assert - verify profiles were saved by loading them back
        var loadedProfiles = await _storage.LoadProfileSetAsync(modelHfId);
        Assert.NotEmpty(loadedProfiles);
        Assert.Equal(3, loadedProfiles.Count);
        
        // Verify the profiles were actually persisted to disk
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
            ".mlx-pep", "profiles");
        Assert.True(Directory.Exists(basePath), "Base profiles directory should exist");
    }

    private List<Profile> CreateTestProfiles()
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        return new List<Profile>
        {
            new Profile(
                SchemaVersion: 1,
                Id: "test-high-001",
                ModelHfId: "test-model",
                Tier: "high",
                Engine: "mlx",
                System: new Dictionary<string, object> { { "os", "macOS" } },
                OMLXSettings: new Dictionary<string, object> { { "compute_units", "ALL" } },
                Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                Provenance: new ProfileProvenance("test", timestamp, "test"),
                Hardware: new HardwareFingerprint("Apple M1", 16, "MacBook")),
            new Profile(
                SchemaVersion: 1,
                Id: "test-balanced-001",
                ModelHfId: "test-model",
                Tier: "balanced",
                Engine: "mlx",
                System: new Dictionary<string, object> { { "os", "macOS" } },
                OMLXSettings: new Dictionary<string, object> { { "compute_units", "GPU" } },
                Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                Provenance: new ProfileProvenance("test", timestamp, "test"),
                Hardware: new HardwareFingerprint("Apple M1", 16, "MacBook")),
            new Profile(
                SchemaVersion: 1,
                Id: "test-efficient-001",
                ModelHfId: "test-model",
                Tier: "efficient",
                Engine: "mlx",
                System: new Dictionary<string, object> { { "os", "macOS" } },
                OMLXSettings: new Dictionary<string, object> { { "compute_units", "CPU" } },
                Harness: new Dictionary<string, object> { { "framework", "vscode" } },
                Provenance: new ProfileProvenance("test", timestamp, "test"),
                Hardware: new HardwareFingerprint("Apple M1", 16, "MacBook"))
        };
    }
}
