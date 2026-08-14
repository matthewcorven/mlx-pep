namespace MlxPep.Cli.Tests.Services;

using System.Text.Json;
using MlxPep.Cli.Services;
using MlxPep.Core;

public class LocalProfileStoreTests
{
    private static string GetTestStoragePath()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), $"mlx-pep-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        return tmpDir;
    }

    private static void CleanupTestDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch { }
        }
    }

    private static Profile CreateTestProfile(string id = "test-profile-1", string modelHfId = "meta-llama/Llama-2-7b", string tier = "balanced")
        => new Profile(
            SchemaVersion: 1,
            Id: id,
            ModelHfId: modelHfId,
            Tier: tier,
            Engine: "omlx",
            System: new Dictionary<string, object> { { "variant", "base" } },
            OMLXSettings: new Dictionary<string, object> { { "config", "default" } },
            Harness: new Dictionary<string, object> { { "type", "mlx" } },
            Provenance: new ProfileProvenance("test-author", DateTime.UtcNow.ToString("O"), "test"),
            Hardware: new HardwareFingerprint("apple-silicon", 16, "MacBookPro18,1"),
            Sampler: null
        );

    [Fact]
    public async Task SaveProfileAsync_CreatesDirectory_WhenNotExists()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            // The LocalProfileStore constructor appends /.mlx-pep/profiles to the base path
            var store = new LocalProfileStore(null, storagePath);
            var profile = CreateTestProfile();

            var result = await store.SaveProfileAsync(profile);

            Assert.True(result.Success, $"Save failed with error: {result.Error}");
            var profilesDir = Path.Combine(storagePath, ".mlx-pep", "profiles");
            var profileFile = Path.Combine(profilesDir, $"{profile.Id}.json");
            Assert.True(File.Exists(profileFile), $"Expected file to exist at {profileFile}");
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task SaveProfileAsync_OverwritesExisting_Profile()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var store = new LocalProfileStore(null, storagePath);
            var profile = CreateTestProfile();

            var result1 = await store.SaveProfileAsync(profile);
            Assert.True(result1.Success);

            var updatedProfile = profile with { Tier = "fast" };
            var result2 = await store.SaveProfileAsync(updatedProfile);
            Assert.True(result2.Success);

            var loaded = await store.LoadProfileAsync(profile.Id);
            Assert.True(loaded.Success);
            Assert.Equal("fast", loaded.Data!.Tier);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task SaveProfileAsync_WithConcurrentWrites_AllSucceed()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var store = new LocalProfileStore(null, storagePath);
            var tasks = new List<Task<Result<bool>>>();

            for (int i = 0; i < 5; i++)
            {
                var profile = CreateTestProfile($"profile-{i}");
                tasks.Add(store.SaveProfileAsync(profile));
            }

            var results = await Task.WhenAll(tasks);

            Assert.All(results, r => Assert.True(r.Success));
            var profilesDir = Path.Combine(storagePath, ".mlx-pep", "profiles");
            Assert.Equal(5, Directory.GetFiles(profilesDir, "*.json").Length);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task SaveProfileAsync_WithInvalidPath_ReturnsFailure()
    {
        var invalidPath = "/invalid/path/that/does/not/exist/mlx-pep-profiles";
        var store = new LocalProfileStore(null, invalidPath);
        var profile = CreateTestProfile();

        var result = await store.SaveProfileAsync(profile);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task LoadProfileAsync_ExistingProfile_ReturnsProfile()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var store = new LocalProfileStore(null, storagePath);
            var profile = CreateTestProfile();

            await store.SaveProfileAsync(profile);
            var result = await store.LoadProfileAsync(profile.Id);

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(profile.Id, result.Data.Id);
            Assert.Equal(profile.ModelHfId, result.Data.ModelHfId);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task LoadProfileAsync_NonExistentProfile_ReturnsFailure()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var store = new LocalProfileStore(null, storagePath);
            var result = await store.LoadProfileAsync("nonexistent-profile");

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task ListLocalAsync_WithProfiles_ReturnsAllProfiles()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var store = new LocalProfileStore(null, storagePath);

            var profiles = new[]
            {
                CreateTestProfile("profile-1"),
                CreateTestProfile("profile-2"),
                CreateTestProfile("profile-3")
            };

            foreach (var profile in profiles)
            {
                await store.SaveProfileAsync(profile);
            }

            var result = await store.ListLocalAsync();

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal(3, result.Data.Count);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task ListLocalAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var store = new LocalProfileStore(null, storagePath);
            var result = await store.ListLocalAsync();

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Empty(result.Data);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task ListLocalAsync_WithMalformedJson_SkipsInvalidFiles()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var profilesDir = Path.Combine(storagePath, ".mlx-pep", "profiles");
            Directory.CreateDirectory(profilesDir);
            
            var validProfile = CreateTestProfile("valid");
            var validPath = Path.Combine(profilesDir, "valid.json");
            await File.WriteAllTextAsync(validPath, JsonSerializer.Serialize(validProfile));

            var invalidPath = Path.Combine(profilesDir, "invalid.json");
            await File.WriteAllTextAsync(invalidPath, "{ invalid json }");

            var store = new LocalProfileStore(null, storagePath);
            var result = await store.ListLocalAsync();

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Single(result.Data);
            Assert.Equal("valid", result.Data[0].Id);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task ProfileExists_WithExistingProfile_ReturnsTrue()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var store = new LocalProfileStore(null, storagePath);
            var profile = CreateTestProfile();

            await store.SaveProfileAsync(profile);
            var exists = store.ProfileExists(profile.Id);
            Assert.True(exists);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task ProfileExists_WithNonExistentProfile_ReturnsFalse()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var store = new LocalProfileStore(null, storagePath);
            var exists = store.ProfileExists("nonexistent-profile");
            Assert.False(exists);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }

    [Fact]
    public async Task SaveAndLoad_Roundtrip_PreservesAllFields()
    {
        var storagePath = GetTestStoragePath();
        try
        {
            var store = new LocalProfileStore(null, storagePath);
            var original = CreateTestProfile("roundtrip-test");

            var saveResult = await store.SaveProfileAsync(original);
            Assert.True(saveResult.Success);

            var loadResult = await store.LoadProfileAsync(original.Id);
            Assert.True(loadResult.Success);
            var loaded = loadResult.Data!;

            Assert.Equal(original.Id, loaded.Id);
            Assert.Equal(original.ModelHfId, loaded.ModelHfId);
            Assert.Equal(original.Tier, loaded.Tier);
            Assert.Equal(original.Engine, loaded.Engine);
            Assert.Equal(original.SchemaVersion, loaded.SchemaVersion);
        }
        finally
        {
            CleanupTestDirectory(storagePath);
        }
    }
}
