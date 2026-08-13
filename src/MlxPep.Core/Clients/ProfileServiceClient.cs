using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MlxPep.Core.Clients;

/// <summary>
/// HTTP client for community profile service endpoints.
/// Handles list, search, and retrieval of profiles from the remote service.
/// </summary>
public class ProfileServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceUrl;
    private readonly ILogger<ProfileServiceClient> _logger;

    public ProfileServiceClient(
        HttpClient httpClient,
        string? serviceUrl = null,
        ILogger<ProfileServiceClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _serviceUrl = serviceUrl ?? Environment.GetEnvironmentVariable("MLX_PEP_SERVICE_URL") ?? "http://localhost:5000";
        _logger = logger ?? new NullLogger<ProfileServiceClient>();

        _logger.LogDebug("ProfileServiceClient initialized with service URL: {ServiceUrl}", _serviceUrl);
    }

    /// <summary>
    /// List all profiles from the service with optional filtering.
    /// </summary>
    /// <param name="modelHfId">Optional HuggingFace model ID filter</param>
    /// <param name="tier">Optional tier filter (e.g., "high-performance", "balanced", "efficient")</param>
    /// <returns>List of profiles matching the filter criteria</returns>
    public async Task<IEnumerable<Profile>> ListProfilesAsync(string? modelHfId = null, string? tier = null)
    {
        try
        {
            _logger.LogDebug("ListProfilesAsync called with modelHfId={ModelHfId}, tier={Tier}", modelHfId, tier);

            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(modelHfId))
                queryParams.Add($"modelHfId={Uri.EscapeDataString(modelHfId)}");
            if (!string.IsNullOrEmpty(tier))
                queryParams.Add($"tier={Uri.EscapeDataString(tier)}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var url = $"{_serviceUrl}/api/v1/profiles{query}";

            _logger.LogDebug("GET request to: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Response status: {Status}", response.StatusCode);

            var jsonDoc = JsonSerializer.Deserialize<JsonElement>(content);
            var profiles = new List<Profile>();

            if (jsonDoc.TryGetProperty("profiles", out var profilesArray))
            {
                foreach (var profileElement in profilesArray.EnumerateArray())
                {
                    try
                    {
                        var profile = JsonSerializer.Deserialize<Profile>(profileElement.GetRawText());
                        if (profile != null)
                            profiles.Add(profile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Error deserializing profile: {Exception}", ex.Message);
                    }
                }
            }

            _logger.LogDebug("ListProfilesAsync returned {Count} profiles", profiles.Count);
            return profiles;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error listing profiles");
            throw new InvalidOperationException($"Failed to list profiles from service: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing profiles");
            throw;
        }
    }

    /// <summary>
    /// Get a specific profile by ID from the service.
    /// </summary>
    /// <param name="profileId">The profile ID to retrieve</param>
    /// <returns>The requested profile, or null if not found</returns>
    public async Task<Profile?> GetProfileAsync(string profileId)
    {
        try
        {
            _logger.LogDebug("GetProfileAsync called for profile {ProfileId}", profileId);

            var url = $"{_serviceUrl}/api/v1/profiles/{Uri.EscapeDataString(profileId)}";

            _logger.LogDebug("GET request to: {Url}", url);

            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Profile {ProfileId} not found (404)", profileId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Response status: {Status}", response.StatusCode);

            var profile = JsonSerializer.Deserialize<Profile>(content);
            if (profile != null)
            {
                _logger.LogDebug("Profile {ProfileId} retrieved successfully", profileId);
            }

            return profile;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error retrieving profile {ProfileId}", profileId);
            throw new InvalidOperationException($"Failed to retrieve profile {profileId} from service: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving profile {ProfileId}", profileId);
            throw;
        }
    }

    /// <summary>
    /// Search profiles on the service. Currently implemented as client-side filtering
    /// after fetching all profiles; can be extended to support server-side search.
    /// </summary>
    /// <param name="query">Search query (matches against profile ID and metadata)</param>
    /// <returns>List of profiles matching the query</returns>
    public async Task<IEnumerable<Profile>> SearchProfilesAsync(string query)
    {
        try
        {
            _logger.LogDebug("SearchProfilesAsync called with query: {Query}", query);

            var allProfiles = await ListProfilesAsync();
            var queryLower = query.ToLowerInvariant();

            var results = allProfiles.Where(p =>
                (p.Id?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.ModelHfId?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Description?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Tier?.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();

            _logger.LogDebug("SearchProfilesAsync returned {Count} results for query: {Query}", results.Count, query);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching profiles with query: {Query}", query);
            throw;
        }
    }
}

/// <summary>
/// Null logger for when no logger is provided (avoids null checks).
/// </summary>
internal class NullLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
    }
}
