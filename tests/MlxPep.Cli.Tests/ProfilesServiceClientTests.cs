namespace MlxPep.Cli.Tests.Services;

using System.Net;
using System.Net.Http;
using System.Text.Json;
using MlxPep.Cli.Services;
using MlxPep.Core;

public class ProfilesServiceClientTests
{
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

    private static HttpClient CreateMockHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpMessageHandler(handler);
        return new HttpClient(mockHandler);
    }

    [Fact]
    public async Task ListProfilesAsync_WithSuccessfulResponse_ReturnsOkResult()
    {
        var testProfile = CreateTestProfile();
        var responseJson = JsonSerializer.Serialize(new { profiles = new[] { testProfile } });

        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.ListProfilesAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data);
    }

    [Fact]
    public async Task ListProfilesAsync_WithHttpError_ReturnsFailResult()
    {
        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.ListProfilesAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ListProfilesAsync_WithTimeout_ReturnsFailResult()
    {
        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(100);
            throw new HttpRequestException("Operation timed out");
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.ListProfilesAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ListProfilesAsync_WithEmptyProfilesList_ReturnsOkWithEmptyList()
    {
        var responseJson = JsonSerializer.Serialize(new { profiles = new Profile[] { } });

        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.ListProfilesAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task ListProfilesAsync_WithInvalidJson_ReturnsFailResult()
    {
        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("invalid json", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.ListProfilesAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ListProfilesAsync_With404NotFound_ReturnsFailResult()
    {
        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.ListProfilesAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetProfileAsync_WithSuccessfulResponse_ReturnsOkResult()
    {
        var testProfile = CreateTestProfile();
        var responseJson = JsonSerializer.Serialize(testProfile);

        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.GetProfileAsync("test-profile-1");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("test-profile-1", result.Data.Id);
    }

    [Fact]
    public async Task GetProfileAsync_WithNotFound_ReturnsFailResult()
    {
        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.GetProfileAsync("nonexistent-profile");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetProfileAsync_WithInvalidJson_ReturnsFailResult()
    {
        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ invalid", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.GetProfileAsync("test-profile");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetProfileAsync_WithServerError_ReturnsFailResult()
    {
        var mockHttpClient = CreateMockHttpClient(async request =>
        {
            await Task.Delay(10);
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var client = new ProfilesServiceClient(mockHttpClient, null);
        var result = await client.GetProfileAsync("test-profile");

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }
}

/// <summary>
/// Mock HTTP message handler for testing without real HTTP calls.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _handler(request);
    }
}
