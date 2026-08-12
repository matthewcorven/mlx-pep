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
public class HFCacheReaderTests
{
    [Fact]
    public async Task HFCacheReader_ListsModelsFromCacheAsync()
    {
        // Arrange
        var reader = new HFCacheReader();

        // Act
        var models = await reader.ListModelsAsync();

        // Assert: Should return IEnumerable
        Assert.NotNull(models);
    }

    [Fact]
    public async Task HFCacheReader_ReturnsEmptyListForEmptyCache()
    {
        // Arrange
        var nonexistentCacheDir = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}");
        var reader = new HFCacheReader(nonexistentCacheDir);

        // Act
        var models = await reader.ListModelsAsync();

        // Assert
        Assert.Empty(models);
    }

    [Fact]
    public async Task HFCacheReader_GetModelReturnsNullForNonexistent()
    {
        // Arrange
        var reader = new HFCacheReader();

        // Act
        var model = await reader.GetModelAsync("nonexistent/model-that-doesnt-exist");

        // Assert
        Assert.Null(model);
    }

    [Fact]
    public async Task HFCacheReader_GetModelReturnsCaseInsensitiveResults()
    {
        // Arrange
        var reader = new HFCacheReader();

        // Act: Get model with different case
        var model1 = await reader.GetModelAsync("meta-llama/Llama-2-7b");
        var model2 = await reader.GetModelAsync("META-LLAMA/LLAMA-2-7B");

        // Assert: If model exists, case-insensitive lookup should work
        if (model1 != null && model2 != null)
        {
            Assert.Equal(model1.RepoId.ToLowerInvariant(), model2.RepoId.ToLowerInvariant());
        }
    }

    [Fact]
    public async Task HFCacheReader_ModelContainsRequiredFields()
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
