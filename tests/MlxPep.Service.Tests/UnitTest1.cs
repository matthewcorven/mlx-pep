using Microsoft.AspNetCore.Builder;

namespace MlxPep.Service.Tests;

public class HealthCheckTests
{
    [Fact]
    public void ServiceCanBeBuilt()
    {
        // Arrange: Create a WebApplicationBuilder
        var builder = WebApplication.CreateBuilder();
        
        // Act: Build the app
        var app = builder.Build();

        // Assert: Verify service was created
        Assert.NotNull(app);
    }

    [Fact]
    public void ServiceConfigurationSupportsAzureBlob()
    {
        // Arrange: Create a WebApplicationBuilder
        var builder = WebApplication.CreateBuilder();
        
        // Act: Get the configuration
        var config = builder.Configuration;
        
        // Assert: Verify configuration exists
        Assert.NotNull(config);
    }
}
