namespace MlxPep.Cli.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MlxPep.Core;

/// <summary>
/// HTTP client for communicating with the mlx-pep community profile service.
/// Handles list, search, and download operations against /api/v1/profiles endpoints.
/// </summary>
public class ProfilesServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ProfilesServiceClient(string? baseUrl = null)
    {
        _baseUrl = baseUrl ?? GetDefaultServiceUrl();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <summary>
    /// Lists all profiles from the service, optionally filtered by modelHfId or tier.
    /// </summary>
    public async Task<List<Profile>> ListProfilesAsync(string? modelHfId = null, string? tier = null)
    {
        try
        {
            var query = BuildQueryString(modelHfId, tier);
            var url = $"{_baseUrl}/api/v1/profiles{query}";

            Console.WriteLine($"[DEBUG] Fetching profile list from {url}");

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[DEBUG] Service returned {response.StatusCode} for profile list");
                return new List<Profile>();
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
                        Console.WriteLine($"[DEBUG] Failed to deserialize profile: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"[DEBUG] Retrieved {profiles.Count} profiles from service");
            return profiles;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[DEBUG] HTTP error fetching profiles: {ex.Message}");
            return new List<Profile>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Unexpected error listing profiles: {ex.Message}");
            return new List<Profile>();
        }
    }

    /// <summary>
    /// Gets a specific profile by ID from the service.
    /// </summary>
    public async Task<Profile?> GetProfileAsync(string profileId)
    {
        try
        {
            var url = $"{_baseUrl}/api/v1/profiles/{profileId}";

            Console.WriteLine($"[DEBUG] Fetching profile {profileId} from {url}");

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[DEBUG] Profile {profileId} not found ({response.StatusCode})");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var profile = JsonSerializer.Deserialize<Profile>(content);

            Console.WriteLine($"[DEBUG] Retrieved profile {profileId} successfully");
            return profile;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[DEBUG] HTTP error fetching profile {profileId}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Unexpected error fetching profile {profileId}: {ex.Message}");
            return null;
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
