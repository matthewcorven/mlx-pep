using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

// Initialize Azure Blob Storage client
var connectionString = builder.Configuration.GetConnectionString("AzureBlobStorage");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddSingleton(new BlobServiceClient(connectionString));
}

var app = builder.Build();

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
if (string.IsNullOrEmpty(connectionString))
{
    logger.LogWarning("AzureBlobStorage connection string not configured. Service will run but Blob operations will fail.");
}
else
{
    logger.LogInformation("Azure Blob Storage client initialized from connection string");
}

// Health check endpoint
app.MapGet("/health", () =>
{
    logger.LogDebug("Health check endpoint called");
    return Results.Ok(new { status = "healthy" });
})
.WithName("GetHealth")
.WithDescription("Health check endpoint that returns service status");

// Placeholder CRUD endpoints for profile management
var profiles = app.MapGroup("/api/v1/profiles")
    .WithName("Profiles")
    .WithDescription("Community profile management endpoints");

profiles.MapGet("/", () =>
{
    logger.LogDebug("ListProfiles endpoint called");
    return Results.Ok(new { profiles = Array.Empty<object>() });
})
.WithName("ListProfiles")
.WithDescription("List all available profiles");

profiles.MapGet("/{id}", (string id) =>
{
    logger.LogDebug("GetProfile endpoint called for profile {ProfileId}", id);
    return Results.NotFound(new { message = $"Profile {id} not found" });
})
.WithName("GetProfile")
.WithDescription("Get a specific profile by ID");

profiles.MapPost("/", () =>
{
    logger.LogDebug("PublishProfile endpoint called");
    return Results.Accepted();
})
.WithName("PublishProfile")
.WithDescription("Publish a new profile");

profiles.MapPut("/{id}", (string id) =>
{
    logger.LogDebug("UpdateProfile endpoint called for profile {ProfileId}", id);
    return Results.NoContent();
})
.WithName("UpdateProfile")
.WithDescription("Update an existing profile");

profiles.MapDelete("/{id}", (string id) =>
{
    logger.LogDebug("DeleteProfile endpoint called for profile {ProfileId}", id);
    return Results.NoContent();
})
.WithName("DeleteProfile")
.WithDescription("Delete a profile");

app.Run("http://localhost:5000");
