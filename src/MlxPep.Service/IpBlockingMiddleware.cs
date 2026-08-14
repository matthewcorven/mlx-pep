using System.Net;
using Microsoft.Extensions.Options;

/// <summary>
/// Middleware for blocking requests based on IP address, CIDR range, or hostname.
/// Each blocking type (IP, CIDR, hostname) is independently toggleable via configuration.
/// Configuration is monitored for changes, allowing hot-reload without restart.
/// Blocked requests receive 403 Forbidden response.
/// </summary>
public class IpBlockingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IpBlockingMiddleware> _logger;
    private readonly IOptionsMonitor<BlockingConfig> _optionsMonitor;

    public IpBlockingMiddleware(
        RequestDelegate next,
        ILogger<IpBlockingMiddleware> logger,
        IOptionsMonitor<BlockingConfig> optionsMonitor)
    {
        _next = next;
        _logger = logger;
        _optionsMonitor = optionsMonitor;

        // Validate configuration on startup
        ValidateConfiguration(optionsMonitor.CurrentValue);
    }

    private void ValidateConfiguration(BlockingConfig config)
    {
        if (config.EnableIpBlocking)
        {
            foreach (var ip in config.BlockedIps)
            {
                if (!IPAddress.TryParse(ip, out _))
                {
                    _logger.LogWarning("Invalid IP address in BlockedIps configuration: {InvalidIp}", ip);
                }
            }
        }

        if (config.EnableCidrBlocking)
        {
            foreach (var cidr in config.BlockedCidrs)
            {
                if (!IsValidCidr(cidr))
                {
                    _logger.LogWarning("Invalid CIDR range in BlockedCidrs configuration: {InvalidCidr}", cidr);
                }
            }
        }
    }

    private static bool IsValidCidr(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2)
            return false;
        if (!IPAddress.TryParse(parts[0], out var network))
            return false;
        if (!int.TryParse(parts[1], out var prefixLength))
            return false;
        var maxPrefixLength = network.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
        return prefixLength >= 0 && prefixLength <= maxPrefixLength;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var config = _optionsMonitor.CurrentValue;

        // Early exit if all blocking types are disabled
        if (!config.EnableIpBlocking && !config.EnableCidrBlocking && !config.EnableHostnameBlocking)
        {
            _logger.LogDebug("IP blocking disabled, allowing request");
            await _next(context);
            return;
        }

        var clientIp = GetClientIpAddress(context);
        var hostname = context.Request.Host.Host;

        _logger.LogDebug("Checking block rules for IP={IP}, Hostname={Hostname}", clientIp, hostname);

        // Check IP blocking
        if (config.EnableIpBlocking && config.BlockedIps.Count > 0)
        {
            if (IsIpBlocked(clientIp, config.BlockedIps))
            {
                _logger.LogWarning("Request blocked by IP rule: IP={IP}", clientIp);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Forbidden: Your IP address is blocked" });
                return;
            }
        }

        // Check CIDR blocking
        if (config.EnableCidrBlocking && config.BlockedCidrs.Count > 0)
        {
            if (IsCidrBlocked(clientIp, config.BlockedCidrs))
            {
                _logger.LogWarning("Request blocked by CIDR rule: IP={IP}", clientIp);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Forbidden: Your IP range is blocked" });
                return;
            }
        }

        // Check hostname blocking
        if (config.EnableHostnameBlocking && config.BlockedHostnames.Count > 0)
        {
            if (IsHostnameBlocked(hostname, config.BlockedHostnames))
            {
                _logger.LogWarning("Request blocked by hostname rule: Hostname={Hostname}", hostname);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Forbidden: This hostname is blocked" });
                return;
            }
        }

        _logger.LogDebug("Request passed all blocking checks: IP={IP}, Hostname={Hostname}", clientIp, hostname);
        await _next(context);
    }

    /// <summary>
    /// Checks if the given IP address is in the blocklist.
    /// </summary>
    private static bool IsIpBlocked(string clientIp, List<string> blockedIps)
    {
        return blockedIps.Any(blocked => string.Equals(clientIp, blocked, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if the given IP address falls within any of the blocked CIDR ranges.
    /// </summary>
    private static bool IsCidrBlocked(string clientIp, List<string> blockedCidrs)
    {
        if (!IPAddress.TryParse(clientIp, out var ip))
        {
            return false; // Invalid IP format, don't block
        }

        foreach (var cidrBlock in blockedCidrs)
        {
            if (IsIpInCidr(ip, cidrBlock))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if an IP address falls within a CIDR range.
    /// CIDR format: "192.168.1.0/24" or "2001:db8::/32"
    /// </summary>
    private static bool IsIpInCidr(IPAddress ip, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2)
            {
                return false; // Invalid CIDR format
            }

            if (!IPAddress.TryParse(parts[0], out var network))
            {
                return false; // Invalid network address
            }

            if (!int.TryParse(parts[1], out var prefixLength))
            {
                return false; // Invalid prefix length
            }

            // IPv4 and IPv6 have different maximum prefix lengths
            var maxPrefixLength = network.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
            if (prefixLength < 0 || prefixLength > maxPrefixLength)
            {
                return false; // Invalid prefix length for address family
            }

            // Get the network address bytes
            var networkBytes = network.GetAddressBytes();
            var ipBytes = ip.GetAddressBytes();

            // Address families must match
            if (networkBytes.Length != ipBytes.Length)
            {
                return false;
            }

            // Calculate how many bytes we need to check fully
            var fullBytes = prefixLength / 8;
            var remainingBits = prefixLength % 8;

            // Check full bytes
            for (int i = 0; i < fullBytes; i++)
            {
                if (networkBytes[i] != ipBytes[i])
                {
                    return false;
                }
            }

            // Check remaining bits in the next byte
            if (remainingBits > 0)
            {
                byte mask = (byte)(0xFF << (8 - remainingBits));
                if ((networkBytes[fullBytes] & mask) != (ipBytes[fullBytes] & mask))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false; // Malformed CIDR, don't block
        }
    }

    /// <summary>
    /// Checks if the given hostname matches any of the blocked hostnames.
    /// Supports wildcards (e.g., "*.malicious.com").
    /// </summary>
    private static bool IsHostnameBlocked(string hostname, List<string> blockedHostnames)
    {
        return blockedHostnames.Any(blocked => HostnameMatches(hostname, blocked));
    }

    /// <summary>
    /// Matches a hostname against a pattern with optional wildcard.
    /// Patterns: "example.com" (exact match), "*.example.com" (subdomains only, NOT parent)
    /// Wildcard patterns only match one level deep (e.g., "sub.example.com" matches "*.example.com", 
    /// but "example.com" does NOT).
    /// </summary>
    private static bool HostnameMatches(string hostname, string pattern)
    {
        // Exact match
        if (string.Equals(hostname, pattern, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Wildcard match (subdomains only, NOT parent domain)
        if (pattern.StartsWith("*.", StringComparison.OrdinalIgnoreCase))
        {
            var domain = pattern[2..]; // Remove "*."
            // ONLY match if hostname ends with ".domain" (subdomain), not "domain" itself
            return hostname.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Gets the client IP address from the request, respecting X-Forwarded-For header if present.
    /// </summary>
    private static string GetClientIpAddress(HttpContext context)
    {
        // Check for X-Forwarded-For header (used behind proxies/load balancers)
        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            var ip = xForwardedFor.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
