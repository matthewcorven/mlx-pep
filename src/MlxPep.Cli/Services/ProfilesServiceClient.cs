namespace MlxPep.Cli.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MlxPep.Core;

/// <summary>
/// HTTP client for communicating with the mlx-pep community profile service.
/// Handles list, search, and download operations against /api/v1/profiles endpoints.
/// </summary>
public class ProfilesServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger<ProfilesServiceClient> _logger;

    public ProfilesServiceClient(HttpClient httpClient, ILogger<ProfilesServiceClient>? logger = null, string? baseUrl = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _baseUrl = baseUrl ?? GetDefaultServiceUrl();
        _logger = logger ?? new NullLogger<ProfilesServiceClient>();
    }

    /// <summary>
    /// Lists all profiles from the service, optionally filtered by modelHfId or tier.
    /// </summary>
    public async Task<Result<List<Profile>>> ListProfilesAsync(string? modelHfId = null, string? tier = null)
    {
        try
        {
            var query = BuildQueryString(modelHfId, tier);
            var url = $"{_baseUrl}/api/v1/profiles{query}";

            _logger.LogDebug("Fetching profile list from {url}", url);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Service returned {statusCode} for profile list", response.StatusCode);
                return Result<List<Profile>>.Fail($"Service returned status {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(content);

            var profiles = new List<Profile>();
            if (responseData.TryGetProperty("profiles", out var profilesArray))
            {
                foreach (var profileElement in profilesArray.EnumerateArray())
                {
                    try
                    {
                        var profile = JsonSerializer.Deserialize<Profile>(profileElement.GetRawText());
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogDebug(ex, "Failed to deserialize profile");
                    }
                }
            }

            _logger.LogDebug("Retrieved {count} profiles from service", profiles.Count);
            return Result<List<Profile>>.Ok(profiles);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "HTTP error fetching profiles");
            return Result<List<Profile>>.Fail(ex);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unexpected error listing profiles");
            return Result<List<Profile>>.Fail(ex);
        }
    }

    /// <summary>
    /// Gets a specific profile by ID from the service.
    /// </summary>
    public async Task<Result<Profile>> GetProfileAsync(string profileId)
    {
        try
        {
            var url = $"{_baseUrl}/api/v1/profiles/{profileId}";

            _logger.LogDebug("Fetching profile {profileId} from {url}", profileId, url);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Profile {profileId} not found ({statusCode})", profileId, response.StatusCode);
                return Result<Profile>.Fail($"Profile {profileId} not found");
            }

            var content = await response.Content.ReadAsStringAsync();
            var profile = JsonSerializer.Deserialize<Profile>(content);

            if (profile == null)
            {
                _logger.LogDebug("Profile {profileId} deserialized to null", profileId);
                return Result<Profile>.Fail($"Profile {profileId} is invalid");
            }

            _logger.LogDebug("Retrieved profile {profileId} successfully", profileId);
            return Result<Profile>.Ok(profile);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "HTTP error fetching profile {profileId}", profileId);
            return Result<Profile>.Fail(ex);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unexpected error fetching profile {profileId}", profileId);
            return Result<Profile>.Fail(ex);
        }
    }

    private static string BuildQueryString(string? modelHfId, string? tier)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(modelHfId))
            parts.Add($"modelHfId={Uri.EscapeDataString(modelHfId)}");
        if (!string.IsNullOrEmpty(tier))
            parts.Add($"tier={Uri.EscapeDataString(tier)}");

        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }

    private static string GetDefaultServiceUrl()
    {
        return Environment.GetEnvironmentVariable("MLX_PEP_SERVICE_URL") ?? "http://localhost:5000";
    }
}
