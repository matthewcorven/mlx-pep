var builder = WebApplication.CreateBuilder(args);

// Register authentication services with JWT Bearer
builder.Services.AddAuthentication()
    .AddJwtBearer();

// Register authorization with policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("write_api", policy =>
        policy
            .RequireAuthenticatedUser()
            .RequireClaim("scope", "write_api"));

var app = builder.Build();

// GET endpoint - no authentication required
app.MapGet("/", () => "Hello World!")
    .WithName("GetRoot");

// POST endpoint - requires write_api authorization
app.MapPost("/profiles", (HttpContext context) =>
{
    // Placeholder implementation
    return Results.Created("/profiles/1", new { id = 1, name = "Test Profile" });
})
    .RequireAuthorization("write_api")
    .WithName("CreateProfile");

// PUT endpoint - requires write_api authorization
app.MapPut("/profiles/{id}", (int id, HttpContext context) =>
{
    // Placeholder implementation
    return Results.Ok(new { id, name = "Updated Profile" });
})
    .RequireAuthorization("write_api")
    .WithName("UpdateProfile");

// DELETE endpoint - requires write_api authorization
app.MapDelete("/profiles/{id}", (int id, HttpContext context) =>
{
    // Placeholder implementation
    return Results.NoContent();
})
    .RequireAuthorization("write_api")
    .WithName("DeleteProfile");

app.Run();
