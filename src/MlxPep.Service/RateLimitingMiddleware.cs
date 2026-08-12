using System.Collections.Concurrent;
using System.Net;

class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitConfig _config;
    private readonly ConcurrentDictionary<string, RateLimitWindow> _windows;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, RateLimitConfig config)
    {
        _next = next;
        _logger = logger;
        _config = config;
        _windows = new ConcurrentDictionary<string, RateLimitWindow>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_config.BypassLocalhost && IsLocalhost(context))
        {
            _logger.LogDebug("Rate limiting bypassed for localhost");
            await _next(context);
            return;
        }

        var endpoint = NormalizeEndpoint(context.Request.Path.ToString());
        var ipAddress = GetClientIpAddress(context);
        var key = $"{endpoint}:{ipAddress}";

        var limit = _config.EndpointLimits.TryGetValue(endpoint, out var endpointLimit)
            ? endpointLimit
            : _config.DefaultLimit;

        var now = DateTime.UtcNow;

        var window = _windows.AddOrUpdate(key,
            _ => new RateLimitWindow { ResetTime = now.AddSeconds(_config.WindowSizeSeconds), RequestCount = 0 },
            (_, existing) =>
            {
                if (now >= existing.ResetTime)
                {
                    _logger.LogDebug("Rate limit window reset for {Key}", key);
                    return new RateLimitWindow { ResetTime = now.AddSeconds(_config.WindowSizeSeconds), RequestCount = 0 };
                }
                return existing;
            });

        if (window.RequestCount >= limit)
        {
            _logger.LogWarning("Rate limit exceeded for {Key} ({RequestCount}/{Limit})", key, window.RequestCount, limit);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            AddRateLimitHeaders(context, limit, 0, window.ResetTime);
            await context.Response.WriteAsJsonAsync(new { message = "Rate limit exceeded. Too many requests." });
            return;
        }

        var incrementedWindow = new RateLimitWindow { RequestCount = window.RequestCount + 1, ResetTime = window.ResetTime };
        _windows[key] = incrementedWindow;
        var remaining = limit - incrementedWindow.RequestCount;
        _logger.LogDebug("Rate limit check passed for {Key} ({RequestCount}/{Limit})", key, incrementedWindow.RequestCount, limit);

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-RateLimit-Limit"))
            {
                AddRateLimitHeaders(context, limit, remaining, window.ResetTime);
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private void AddRateLimitHeaders(HttpContext context, int limit, int remaining, DateTime resetTime)
    {
        var resetUnixTime = ((DateTimeOffset)resetTime).ToUnixTimeSeconds();
        var secondsUntilReset = Math.Max(0, (int)(resetTime - DateTime.UtcNow).TotalSeconds);

        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = resetUnixTime.ToString();
        if (remaining == 0)
        {
            context.Response.Headers["Retry-After"] = secondsUntilReset.ToString();
        }
    }

    private string GetClientIpAddress(HttpContext context)
    {
        if (_config.RespectXForwardedFor)
        {
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }
        }
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static bool IsLocalhost(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        return remoteIp == null || IPAddress.IsLoopback(remoteIp);
    }

    private string NormalizeEndpoint(string path)
    {
        if (path.StartsWith("/api/v1/profiles/") && path.Length > "/api/v1/profiles/".Length)
        {
            return "/api/v1/profiles";
        }
        return path;
    }

    private class RateLimitWindow
    {
        public int RequestCount { get; set; }
        public DateTime ResetTime { get; set; }
    }
}
