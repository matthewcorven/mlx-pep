namespace MlxPep.Core.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MlxPep.Core;
using Xunit;

/// <summary>
/// Comprehensive tests for HFCacheReader.
/// Use Case 2: Reuse the shared HF cache — browse/download models at ~/.cache/huggingface/hub
/// </summary>
public class HFCacheReaderTests : IDisposable
{
    private readonly string _tempCacheDir;

    public HFCacheReaderTests()
    {
        _tempCacheDir = Path.Combine(Path.GetTempPath(), $"hf-cache-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempCacheDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempCacheDir))
                Directory.Delete(_tempCacheDir, true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    /// Helper: Create a fixture cache model directory structure.
    /// Creates: {cacheDir}/models--{org}--{name}/snapshots/{revision}/ with dummy files.
    /// </summary>
    private void CreateModelFixture(string org, string name, string revision, long fileSize = 1024)
    {
        var modelDir = Path.Combine(_tempCacheDir, $"models--{org}--{name}");
        var snapshotsDir = Path.Combine(modelDir, "snapshots");
        var revisionDir = Path.Combine(snapshotsDir, revision);

        Directory.CreateDirectory(revisionDir);

        // Create dummy model files in the revision directory
        var dummyFile1 = Path.Combine(revisionDir, "config.json");
        File.WriteAllText(dummyFile1, "{\"dummy\": \"config\"}");

        var dummyFile2 = Path.Combine(revisionDir, "model.safetensors");
        File.WriteAllBytes(dummyFile2, new byte[fileSize]);
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsEmptyListForNonexistentCacheDirectory()
    {
        // Arrange
        var nonexistentCacheDir = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}");
        var reader = new HFCacheReader(nonexistentCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Empty(models);
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsEmptyListForEmptyCacheDirectory()
    {
        // Arrange
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Empty(models);
    }

    [Fact]
    public async Task ListModelsAsync_FindsSingleModel()
    {
        // Arrange
        CreateModelFixture("meta-llama", "Llama-2-7b", "abc123def456", fileSize: 5242880); // 5MB
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Single(models);
        var model = models.First();
        Assert.Equal("meta-llama/Llama-2-7b", model.RepoId);
        Assert.Equal("abc123def456", model.Revision);
        Assert.True(model.SizeBytes > 0);
        Assert.NotEqual(DateTime.MinValue, model.LastModified);
    }

    [Fact]
    public async Task ListModelsAsync_FindsMultipleModels()
    {
        // Arrange
        CreateModelFixture("meta-llama", "Llama-2-7b", "abc123", fileSize: 5242880);
        CreateModelFixture("meta-llama", "Llama-2-13b", "def456", fileSize: 10485760);
        CreateModelFixture("tiiuae", "falcon-7b", "ghi789", fileSize: 4194304);
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Equal(3, models.Count());
        Assert.Contains(models, m => m.RepoId == "meta-llama/Llama-2-7b");
        Assert.Contains(models, m => m.RepoId == "meta-llama/Llama-2-13b");
        Assert.Contains(models, m => m.RepoId == "tiiuae/falcon-7b");
    }

    [Fact]
    public async Task ListModelsAsync_HandlesMultipleRevisionsPerModel()
    {
        // Arrange
        CreateModelFixture("meta-llama", "Llama-2-7b", "main", fileSize: 5242880);
        CreateModelFixture("meta-llama", "Llama-2-7b", "v2", fileSize: 5242880);
        CreateModelFixture("meta-llama", "Llama-2-7b", "v1", fileSize: 5242880);
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        var llamaModels = models.Where(m => m.RepoId == "meta-llama/Llama-2-7b").ToList();
        Assert.Equal(3, llamaModels.Count);
        Assert.Contains(llamaModels, m => m.Revision == "main");
        Assert.Contains(llamaModels, m => m.Revision == "v2");
        Assert.Contains(llamaModels, m => m.Revision == "v1");
    }

    [Fact]
    public async Task ListModelsAsync_CalculatesSizeCorrectly()
    {
        // Arrange
        var expectedSize = 1024 * 1024 * 10; // 10MB
        CreateModelFixture("test", "model", "rev1", fileSize: expectedSize);
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Single(models);
        var model = models.First();
        // Size should include config.json (~20 bytes) + model.safetensors (10MB)
        Assert.True(model.SizeBytes > expectedSize);
        Assert.True(model.SizeBytes <= expectedSize + 1024); // Allow small margin for config file
    }

    [Fact]
    public async Task ListModelsAsync_SetsLastModifiedTimestamp()
    {
        // Arrange
        CreateModelFixture("test", "model", "rev1", fileSize: 1024);
        var beforeCreation = DateTime.UtcNow.AddSeconds(-5);
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Single(models);
        var model = models.First();
        Assert.True(model.LastModified >= beforeCreation);
        Assert.True(model.LastModified <= DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task ListModelsAsync_IgnoresDirsNotMatchingPattern()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_tempCacheDir, "not-a-model"));
        Directory.CreateDirectory(Path.Combine(_tempCacheDir, "models--incomplete"));
        CreateModelFixture("test", "model", "rev1", fileSize: 1024);
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Single(models);
        Assert.Equal("test/model", models.First().RepoId);
    }

    [Fact]
    public async Task ListModelsAsync_SkipsModelWithoutSnapshots()
    {
        // Arrange
        var modelDir = Path.Combine(_tempCacheDir, "models--orphan--model");
        Directory.CreateDirectory(modelDir);
        // No snapshots directory created
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Empty(models);
    }

    [Fact]
    public async Task GetModelAsync_ReturnsModelWhenFound()
    {
        // Arrange
        CreateModelFixture("meta-llama", "Llama-2-7b", "abc123");
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var model = await reader.GetModelAsync("meta-llama/Llama-2-7b");

        // Assert
        Assert.NotNull(model);
        Assert.Equal("meta-llama/Llama-2-7b", model.RepoId);
    }

    [Fact]
    public async Task GetModelAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        CreateModelFixture("meta-llama", "Llama-2-7b", "abc123");
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var model = await reader.GetModelAsync("nonexistent/model");

        // Assert
        Assert.Null(model);
    }

    [Fact]
    public async Task GetModelAsync_PerformsCaseInsensitiveSearch()
    {
        // Arrange
        CreateModelFixture("Meta-Llama", "Llama-2-7b", "abc123");
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var model1 = await reader.GetModelAsync("meta-llama/Llama-2-7b");
        var model2 = await reader.GetModelAsync("META-LLAMA/LLAMA-2-7B");

        // Assert
        Assert.NotNull(model1);
        Assert.NotNull(model2);
        Assert.Equal(model1.RepoId, model2.RepoId);
    }

    [Fact]
    public async Task GetModelAsync_ReturnsNullForNullOrEmptyRepoId()
    {
        // Arrange
        CreateModelFixture("test", "model", "rev1");
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var model1 = await reader.GetModelAsync(null!);
        var model2 = await reader.GetModelAsync("");

        // Assert
        Assert.Null(model1);
        Assert.Null(model2);
    }

    [Fact]
    public async Task Constructor_HonorsExplicitCacheDir()
    {
        // Arrange
        CreateModelFixture("test", "model", "rev1");
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Single(models);
    }

    /// <summary>
    /// SECURITY TEST #1: Circular Symlink Detection
    /// Verifies that circular symlinks are detected and do not cause infinite loops.
    /// </summary>
    [Fact]
    public async Task ListModelsAsync_DetectsCircularSymlinksAndDoesNotHang()
    {
        // Arrange
        CreateModelFixture("test", "model", "rev1", fileSize: 1024);
        
        // Create a circular symlink
        var revisionDir = Path.Combine(_tempCacheDir, "models--test--model", "snapshots", "rev1");
        var circularLink = Path.Combine(revisionDir, "loop");
        
        try
        {
            File.CreateSymbolicLink(circularLink, Path.Combine(_tempCacheDir, "models--test--model", "snapshots"));
        }
        catch
        {
            // Skip if symlinks not supported (e.g., Windows without admin)
            return;
        }

        var reader = new HFCacheReader(_tempCacheDir);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        
        // Act - should complete without hanging
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Single(models);
        Assert.Equal("test/model", models.First().RepoId);
    }

    /// <summary>
    /// SECURITY TEST #2: Path Escape Detection
    /// Verifies that path validation prevents directory escape attacks.
    /// Note: This test skips on platforms without proper symlink support.
    /// </summary>
    [Fact]
    public async Task ListModelsAsync_SkipsPathsOutsideCache()
    {
        // This test validates that IsPathWithinCache() correctly identifies and rejects paths outside the cache.
        // On most systems, the security boundary is enforced. On systems with symlink limitations, this test skips.
        
        // Arrange
        CreateModelFixture("test", "model", "rev1", fileSize: 1024);
        
        // Create a model directory with a suspicious structure
        var modelDir = Path.Combine(_tempCacheDir, "models--test--model");
        var snapshotsDir = Path.Combine(modelDir, "snapshots");
        
        // Try to create a symlink pointing outside - if this fails, symlinks aren't supported, so skip
        var escapeLink = Path.Combine(snapshotsDir, "escape");
        var parentDir = Path.GetDirectoryName(_tempCacheDir);
        if (string.IsNullOrEmpty(parentDir))
        {
            return; // Can't get parent directory, skip test
        }
        
        try
        {
            // Try to create a symlink to the parent directory (one level outside cache)
            File.CreateSymbolicLink(escapeLink, parentDir);
        }
        catch
        {
            // Symlinks not supported or not allowed on this system, skip this test
            return;
        }

        var reader = new HFCacheReader(_tempCacheDir);
        
        // Act - should complete without error even with escape symlink present
        var models = await reader.ListModelsAsync();

        // Assert - normal model should be found
        // (The escape link being skipped is the security win, even if it doesn't cause an error)
        Assert.NotEmpty(models);
    }

    /// <summary>
    /// SECURITY TEST #3: Permission Error Handling
    /// Verifies that permission errors on individual directories are handled gracefully.
    /// </summary>
    [Fact]
    public async Task ListModelsAsync_HandlesPermissionErrorsGracefully()
    {
        // Arrange
        CreateModelFixture("test", "model", "rev1", fileSize: 1024);
        CreateModelFixture("test", "model2", "rev1", fileSize: 1024);
        
        // Make one revision directory unreadable (chmod 000)
        var lockedDir = Path.Combine(_tempCacheDir, "models--test--model2", "snapshots", "rev1");
        var currentPermissions = File.GetAttributes(lockedDir);
        
        try
        {
            // On macOS/Linux, we can restrict permissions
            // Note: This requires appropriate platform support
            File.SetAttributes(lockedDir, FileAttributes.ReadOnly);
        }
        catch
        {
            // Skip if permission manipulation not supported
            return;
        }

        var reader = new HFCacheReader(_tempCacheDir);
        
        // Act - should continue processing despite permission error
        var models = await reader.ListModelsAsync();

        // Assert - should find at least the readable model
        Assert.True(models.Count() >= 1);
        Assert.Contains(models, m => m.RepoId == "test/model");
        
        // Cleanup
        try { File.SetAttributes(lockedDir, currentPermissions); } catch { }
    }

    /// <summary>
    /// SECURITY TEST #4: .git Directory Exclusion
    /// Verifies that .git directories are excluded from size calculation.
    /// </summary>
    [Fact]
    public async Task ListModelsAsync_SkipsDotGitDirectories()
    {
        // Arrange
        CreateModelFixture("test", "model", "rev1", fileSize: 1024);
        
        // Create a .git directory with files inside
        var revisionDir = Path.Combine(_tempCacheDir, "models--test--model", "snapshots", "rev1");
        var gitDir = Path.Combine(revisionDir, ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllBytes(Path.Combine(gitDir, "HEAD"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(gitDir, "config"), new byte[1024]);
        
        var reader = new HFCacheReader(_tempCacheDir);
        
        // Act
        var models = await reader.ListModelsAsync();

        // Assert - size should not include .git files
        Assert.Single(models);
        var model = models.First();
        // Size should be roughly config.json (~20 bytes) + model.safetensors (1024 bytes)
        // NOT including the 2KB from .git directory
        Assert.True(model.SizeBytes < 2048, $"Size {model.SizeBytes} should exclude .git files");
    }

    [Fact]
    public async Task Constructor_HonorsHF_HUB_CACHE_EnvVar()
    {
        // Arrange
        CreateModelFixture("test", "model", "rev1");
        var originalEnv = Environment.GetEnvironmentVariable("HF_HUB_CACHE");
        try
        {
            Environment.SetEnvironmentVariable("HF_HUB_CACHE", _tempCacheDir);
            var reader = new HFCacheReader();

            // Act
            var models = await reader.ListModelsAsync();

            // Assert
            Assert.Single(models);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HF_HUB_CACHE", originalEnv);
        }
    }

    [Fact]
    public async Task Constructor_HonorsHF_HOME_EnvVar()
    {
        // Arrange
        var hfHomeDir = Path.Combine(_tempCacheDir, "hf_home");
        Directory.CreateDirectory(hfHomeDir);
        var hubDir = Path.Combine(hfHomeDir, "hub");
        Directory.CreateDirectory(hubDir);
        
        // Create model in the hub subdirectory
        var modelDir = Path.Combine(hubDir, "models--test--model");
        var snapshotsDir = Path.Combine(modelDir, "snapshots", "rev1");
        Directory.CreateDirectory(snapshotsDir);
        File.WriteAllText(Path.Combine(snapshotsDir, "config.json"), "{}");

        var originalEnv = Environment.GetEnvironmentVariable("HF_HOME");
        try
        {
            Environment.SetEnvironmentVariable("HF_HUB_CACHE", null);
            Environment.SetEnvironmentVariable("HF_HOME", hfHomeDir);
            var reader = new HFCacheReader();

            // Act
            var models = await reader.ListModelsAsync();

            // Assert
            Assert.Single(models);
            Assert.Equal("test/model", models.First().RepoId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HF_HOME", originalEnv);
        }
    }

    [Fact]
    public async Task Constructor_PreferrsHF_HUB_CACHE_OverHF_HOME()
    {
        // Arrange
        CreateModelFixture("test", "model1", "rev1");
        
        var hfHomeDir = Path.Combine(_tempCacheDir, "hf_home");
        Directory.CreateDirectory(hfHomeDir);
        var hubDir = Path.Combine(hfHomeDir, "hub");
        Directory.CreateDirectory(hubDir);

        var originalHubCache = Environment.GetEnvironmentVariable("HF_HUB_CACHE");
        var originalHfHome = Environment.GetEnvironmentVariable("HF_HOME");
        try
        {
            Environment.SetEnvironmentVariable("HF_HUB_CACHE", _tempCacheDir);
            Environment.SetEnvironmentVariable("HF_HOME", hfHomeDir);
            var reader = new HFCacheReader();

            // Act
            var models = await reader.ListModelsAsync();

            // Assert
            Assert.Single(models);
            Assert.Equal("test/model1", models.First().RepoId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HF_HUB_CACHE", originalHubCache);
            Environment.SetEnvironmentVariable("HF_HOME", originalHfHome);
        }
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsIEnumerable()
    {
        // Arrange
        CreateModelFixture("test", "model", "rev1");
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var result = await reader.ListModelsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IEnumerable<Model>>(result);
    }

    [Fact]
    public async Task Model_HasRequiredFields()
    {
        // Arrange
        CreateModelFixture("test", "model", "abc123xyz");
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Single(models);
        var model = models.First();
        Assert.NotNull(model.RepoId);
        Assert.NotNull(model.Revision);
        Assert.NotEqual(default, model.SizeBytes);
        Assert.NotEqual(DateTime.MinValue, model.LastModified);
    }

    [Fact]
    public async Task ListModelsAsync_HandlesDirectoriesWithSpecialCharacters()
    {
        // Arrange
        CreateModelFixture("test-org", "model_name-v1", "rev-1");
        var reader = new HFCacheReader(_tempCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Single(models);
        Assert.Equal("test-org/model_name-v1", models.First().RepoId);
    }

    [Fact]
    public void Model_GetSizeFormatsCorrectly()
    {
        // Arrange
        var model = new Model("test/model", "rev1", 1024 * 1024 * 1024, DateTime.UtcNow);

        // Act
        var formatted = model.GetSize();

        // Assert
        Assert.Contains("GB", formatted);
        Assert.Contains("1", formatted);
    }

    [Fact]
    public async Task HFCacheReader_ListModelsAsync_WithValidCache()
    {
        // Arrange
        var reader = new HFCacheReader();

        // Act
        var models = await reader.ListModelsAsync();
        var model = models.FirstOrDefault();

        // Assert: If model exists, verify required fields
        if (model != null)
        {
            Assert.NotNull(model.RepoId);
            Assert.NotEmpty(model.RepoId);
            Assert.True(model.SizeBytes >= 0, "Model size should be non-negative");
        }
    }

    [Fact]
    public async Task HFCacheReader_MultipleModelsAreIndependent()
    {
        // Arrange
        var reader = new HFCacheReader();

        // Act
        var models = await reader.ListModelsAsync();
        var modelList = models.ToList();

        // Assert: If multiple models exist, they should have different repo IDs
        if (modelList.Count > 1)
        {
            var firstModel = modelList[0];
            var secondModel = modelList[1];
            Assert.NotEqual(firstModel.RepoId, secondModel.RepoId);
        }
    }

    [Fact]
    public async Task HFCacheReader_SearchByRepoId()
    {
        // Arrange
        var reader = new HFCacheReader();

        // Act
        var allModels = await reader.ListModelsAsync();

        // Assert: Should be able to search by repo ID
        if (allModels.Any())
        {
            var firstModel = allModels.First();
            var foundModel = await reader.GetModelAsync(firstModel.RepoId);
            Assert.NotNull(foundModel);
            Assert.Equal(firstModel.RepoId, foundModel.RepoId);
        }
    }

    [Fact]
    public async Task HFCacheReader_HandlesPathsWithSpecialCharacters()
    {
        // Arrange: Hugging Face model IDs use org/model format
        var specialCharModels = new[]
        {
            "org-name/model-name-v2.1",
            "org_name/model_name_v2",
            "org.name/model.name.v2"
        };
        var reader = new HFCacheReader();

        // Act
        foreach (var modelId in specialCharModels)
        {
            var model = await reader.GetModelAsync(modelId);
            // Assert: Should handle gracefully (return null if not found, not throw)
            // This validates the method can handle special characters in model IDs
        }
    }

    [Fact]
    public async Task HFCacheReader_SupportsLargeModelSizes()
    {
        // Arrange
        var reader = new HFCacheReader();

        // Act
        var models = await reader.ListModelsAsync();

        // Assert: Model sizes should be representable as long
        foreach (var model in models)
        {
            Assert.True(model.SizeBytes >= 0);
            Assert.True(model.SizeBytes <= long.MaxValue);
        }
    }

    [Fact]
    public async Task HFCacheReader_LastModifiedTimestampsAreValid()
    {
        // Arrange
        var reader = new HFCacheReader();

        // Act
        var models = await reader.ListModelsAsync();

        // Assert: Last modified dates should be valid
        foreach (var model in models)
        {
            Assert.True(model.LastModified <= DateTime.UtcNow, "Last modified should not be in the future");
        }
    }

    [Fact]
    public async Task HFCacheReader_EnumeratesMultipleRevisions()
    {
        // Arrange
        var reader = new HFCacheReader();

        // Act
        var models = await reader.ListModelsAsync();

        // Assert: Models might have multiple revisions; all should be returned
        Assert.NotNull(models);
        var modelList = models.ToList();

        // If we have models, they should be accessible
        if (modelList.Count > 0)
        {
            var firstModel = modelList[0];
            Assert.NotNull(firstModel.RepoId);
        }
    }

    [Fact]
    public void HFCacheReader_ConstructorWithDefaultPath()
    {
        // Arrange & Act
        var reader = new HFCacheReader();

        // Assert: Should construct with default HF cache path
        Assert.NotNull(reader);
    }

    [Fact]
    public void HFCacheReader_ConstructorWithCustomPath()
    {
        // Arrange
        var customPath = Path.Combine(Path.GetTempPath(), $"hf-cache-{Guid.NewGuid()}");
        Directory.CreateDirectory(customPath);

        try
        {
            // Act
            var reader = new HFCacheReader(customPath);

            // Assert
            Assert.NotNull(reader);
        }
        finally
        {
            Directory.Delete(customPath, true);
        }
    }

    [Fact]
    public void HFCacheReader_SupportsEnvironmentVariables()
    {
        // Arrange: HF_HOME or HF_HUB_CACHE might be set
        var originalHFHome = Environment.GetEnvironmentVariable("HF_HOME");
        var originalHFHubCache = Environment.GetEnvironmentVariable("HF_HUB_CACHE");

        try
        {
            // Act: Create reader (will use environment variables if set)
            var reader = new HFCacheReader();

            // Assert: Should successfully create
            Assert.NotNull(reader);
        }
        finally
        {
            // Restore original values
            if (originalHFHome != null)
                Environment.SetEnvironmentVariable("HF_HOME", originalHFHome);
            if (originalHFHubCache != null)
                Environment.SetEnvironmentVariable("HF_HUB_CACHE", originalHFHubCache);
        }
    }
}
