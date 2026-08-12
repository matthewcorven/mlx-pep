using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;

namespace MlxPep.Service.Tests;

public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRoot_WithoutAuth_Returns200()
    {
        var client = _factory.CreateClient();
        
        var response = await client.GetAsync("/");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateProfile_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("{\"name\":\"Test\"}", System.Text.Encoding.UTF8, "application/json");
        
        var response = await client.PostAsync("/profiles", content);
        
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("{\"name\":\"Updated\"}", System.Text.Encoding.UTF8, "application/json");
        
        var response = await client.PutAsync("/profiles/1", content);
        
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProfile_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        
        var response = await client.DeleteAsync("/profiles/1");
        
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateProfile_WithInvalidToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", "invalid-token-that-does-not-parse");
        
        var content = new StringContent("{\"name\":\"Test\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/profiles", content);
        
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoot_ReturnsExpectedContent()
    {
        var client = _factory.CreateClient();
        
        var response = await client.GetAsync("/");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }
}
