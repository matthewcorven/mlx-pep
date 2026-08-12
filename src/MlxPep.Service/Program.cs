using Azure.Storage.Blobs;
using MlxPep.Core;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Initialize Azure Blob Storage client
var connectionString = builder.Configuration.GetConnectionString("AzureBlobStorage");
BlobContainerClient? blobContainer = null;

if (!string.IsNullOrEmpty(connectionString))
{
    try
    {
        var blobServiceClient = new BlobServiceClient(connectionString);
        blobContainer = blobServiceClient.GetBlobContainerClient("profiles");
        builder.Services.AddSingleton(blobContainer);
    }
    catch (Exception)
    {
        // Log will happen after app is built
    }
}

var app = builder.Build();

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
if (blobContainer == null)
{
    logger.LogWarning("Azure Blob Storage not configured. Profile storage will be unavailable.");
}
else
{
    logger.LogInformation("Azure Blob Storage initialized for profile storage");
}

// Health check endpoint
app.MapGet("/health", () =>
{
    logger.LogDebug("Health check endpoint called");
    return Results.Ok(new { status = "healthy" });
})
.WithName("GetHealth")
.WithDescription("Health check endpoint that returns service status");

// Profile CRUD endpoints
var profiles = app.MapGroup("/api/v1/profiles")
    .WithName("Profiles")
    .WithDescription("Community profile management endpoints");

// GET /api/v1/profiles - List profiles with optional filtering
profiles.MapGet("/", ListProfiles)
    .WithName("ListProfiles")
    .WithDescription("List all available profiles, optionally filtered by modelHfId or tier");

// GET /api/v1/profiles/{id} - Get a specific profile
profiles.MapGet("/{id}", GetProfile)
    .WithName("GetProfile")
    .WithDescription("Get a specific profile by ID");

// POST /api/v1/profiles - Publish a new profile
profiles.MapPost("/", PublishProfile)
    .WithName("PublishProfile")
    .WithDescription("Publish a new profile to Azure Blob Storage");

// PUT /api/v1/profiles/{id} - Update an existing profile
profiles.MapPut("/{id}", UpdateProfile)
    .WithName("UpdateProfile")
    .WithDescription("Update an existing profile");

// DELETE /api/v1/profiles/{id} - Delete a profile
profiles.MapDelete("/{id}", DeleteProfile)
    .WithName("DeleteProfile")
    .WithDescription("Delete a profile from storage");

app.Run("http://localhost:5000");

// Profile CRUD Handlers

static async Task<IResult> ListProfiles(
    string? modelHfId,
    string? tier,
    BlobContainerClient? blobContainer,
    ILogger<Program> logger)
{
    logger.LogDebug("ListProfiles called with filters: modelHfId={ModelHfId}, tier={Tier}", modelHfId, tier);
    
    if (blobContainer == null)
    {
        logger.LogWarning("Azure Blob Storage not available for listing profiles");
        return Results.Ok(new { profiles = new object[] { }, warning = "Blob storage not configured" });
    }

    try
    {
        var profiles = new List<Profile>();
        await foreach (var blobItem in blobContainer.GetBlobsAsync())
        {
            try
            {
                var blobClient = blobContainer.GetBlobClient(blobItem.Name);
                var download = await blobClient.DownloadAsync();
                var profile = await JsonSerializer.DeserializeAsync<Profile>(download.Value.Content);
                
                if (profile != null)
                {
                    // Apply filters if specified
                    if (!string.IsNullOrEmpty(modelHfId) && profile.ModelHfId != modelHfId)
                        continue;
                    if (!string.IsNullOrEmpty(tier) && profile.Tier != tier)
                        continue;
                    
                    profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug("Error reading profile blob {BlobName}: {Exception}", blobItem.Name, ex.Message);
            }
        }
        
        logger.LogDebug("ListProfiles returned {Count} profiles", profiles.Count);
        return Results.Ok(new { profiles });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error listing profiles");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

static async Task<IResult> GetProfile(
    string id,
    BlobContainerClient? blobContainer,
    ILogger<Program> logger)
{
    logger.LogDebug("GetProfile called for profile {ProfileId}", id);
    
    if (blobContainer == null)
    {
        logger.LogWarning("Azure Blob Storage not available for getting profile {ProfileId}", id);
        return Results.NotFound(new { message = "Blob storage not configured" });
    }

    try
    {
        var blobClient = blobContainer.GetBlobClient($"{id}.json");
        var download = await blobClient.DownloadAsync();
        var profile = await JsonSerializer.DeserializeAsync<Profile>(download.Value.Content);
        
        if (profile == null)
        {
            logger.LogDebug("Profile {ProfileId} not found or invalid JSON", id);
            return Results.NotFound(new { message = $"Profile {id} not found" });
        }
        
        logger.LogDebug("Profile {ProfileId} retrieved successfully", id);
        return Results.Ok(profile);
    }
    catch (Azure.RequestFailedException ex) when (ex.Status == 404)
    {
        logger.LogDebug("Profile {ProfileId} not found in blob storage", id);
        return Results.NotFound(new { message = $"Profile {id} not found" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error retrieving profile {ProfileId}", id);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

static async Task<IResult> PublishProfile(
    Profile profile,
    BlobContainerClient? blobContainer,
    ILogger<Program> logger)
{
    logger.LogDebug("PublishProfile called for profile {ProfileId}", profile.Id);
    
    if (blobContainer == null)
    {
        logger.LogWarning("Azure Blob Storage not available for publishing profile");
        return Results.BadRequest(new { message = "Blob storage not configured" });
    }

    try
    {
        // Validate profile
        if (string.IsNullOrEmpty(profile.Id) || string.IsNullOrEmpty(profile.ModelHfId))
        {
            logger.LogWarning("PublishProfile: Invalid profile - missing required fields");
            return Results.BadRequest(new { message = "Profile must have id and modelHfId" });
        }
        
        var blobClient = blobContainer.GetBlobClient($"{profile.Id}.json");
        var json = JsonSerializer.Serialize(profile);
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
        await writer.FlushAsync();
        stream.Position = 0;
        
        await blobClient.UploadAsync(stream, overwrite: true);
        logger.LogDebug("Profile {ProfileId} published to blob storage", profile.Id);
        return Results.Created($"/api/v1/profiles/{profile.Id}", profile);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error publishing profile {ProfileId}", profile.Id);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

static async Task<IResult> UpdateProfile(
    string id,
    Profile profile,
    BlobContainerClient? blobContainer,
    ILogger<Program> logger)
{
    logger.LogDebug("UpdateProfile called for profile {ProfileId}", id);
    
    if (blobContainer == null)
    {
        logger.LogWarning("Azure Blob Storage not available for updating profile");
        return Results.BadRequest(new { message = "Blob storage not configured" });
    }

    try
    {
        // Verify profile exists
        var blobClient = blobContainer.GetBlobClient($"{id}.json");
        var exists = await blobClient.ExistsAsync();
        if (!exists.Value)
        {
            logger.LogDebug("Profile {ProfileId} not found for update", id);
            return Results.NotFound(new { message = $"Profile {id} not found" });
        }
        
        // Update the profile with the provided ID
        var updatedProfile = profile with { Id = id };
        var json = JsonSerializer.Serialize(updatedProfile);
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(json);
        await writer.FlushAsync();
        stream.Position = 0;
        
        await blobClient.UploadAsync(stream, overwrite: true);
        logger.LogDebug("Profile {ProfileId} updated in blob storage", id);
        return Results.Ok(updatedProfile);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating profile {ProfileId}", id);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}

static async Task<IResult> DeleteProfile(
    string id,
    BlobContainerClient? blobContainer,
    ILogger<Program> logger)
{
    logger.LogDebug("DeleteProfile called for profile {ProfileId}", id);
    
    if (blobContainer == null)
    {
        logger.LogWarning("Azure Blob Storage not available for deleting profile");
        return Results.BadRequest(new { message = "Blob storage not configured" });
    }

    try
    {
        var blobClient = blobContainer.GetBlobClient($"{id}.json");
        var deleted = await blobClient.DeleteIfExistsAsync();
        
        if (!deleted.Value)
        {
            logger.LogDebug("Profile {ProfileId} not found for deletion", id);
            return Results.NotFound(new { message = $"Profile {id} not found" });
        }
        
        logger.LogDebug("Profile {ProfileId} deleted from blob storage", id);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting profile {ProfileId}", id);
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}
