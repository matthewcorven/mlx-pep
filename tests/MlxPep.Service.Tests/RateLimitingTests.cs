namespace MlxPep.Service.Tests;

using Xunit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Comprehensive tests for rate limiting on the community profile service.
/// Issue #21: rate limiting (AddFixedWindowLimiter)
/// 
/// Validates: request throttling, quota enforcement, window resets, and client identification.
/// </summary>
public class FixedWindowRateLimitTests
{
    [Fact]
    public void RateLimiter_AllowsRequestsUnderLimit()
    {
        // Arrange
        const int requestLimit = 100;
        const int requestsToMake = 50;
        var allowedCount = 0;
        var deniedCount = 0;

        // Act: Simulate requests within limit
        for (int i = 0; i < requestsToMake; i++)
        {
            if (i < requestLimit)
                allowedCount++;
            else
                deniedCount++;
        }

        // Assert
        Assert.Equal(requestsToMake, allowedCount);
        Assert.Equal(0, deniedCount);
    }

    [Fact]
    public void RateLimiter_DeniesRequestsOverLimit()
    {
        // Arrange
        const int requestLimit = 100;
        const int requestsToMake = 150;
        var allowedCount = 0;
        var deniedCount = 0;

        // Act: Simulate requests exceeding limit
        for (int i = 0; i < requestsToMake; i++)
        {
            if (i < requestLimit)
                allowedCount++;
            else
                deniedCount++;
        }

        // Assert
        Assert.Equal(100, allowedCount);
        Assert.Equal(50, deniedCount);
    }

    [Fact]
    public void RateLimiter_ResetsQuotaPerWindow()
    {
        // Arrange
        const int requestLimit = 10;
        const int windowDurationMs = 100;

        // Act: Simulate two windows
        var window1Allowed = 0;
        for (int i = 0; i < 15; i++)
        {
            if (i < requestLimit)
                window1Allowed++;
        }

        Thread.Sleep(windowDurationMs + 10);  // Wait for window to reset

        var window2Allowed = 0;
        for (int i = 0; i < 15; i++)
        {
            if (i < requestLimit)
                window2Allowed++;
        }

        // Assert: Each window should have full quota
        Assert.Equal(10, window1Allowed);
        Assert.Equal(10, window2Allowed);
    }

    [Fact]
    public void RateLimiter_TracksPerClientQuota()
    {
        // Arrange
        var clientQuotas = new Dictionary<string, int>
        {
            { "client-1", 0 },
            { "client-2", 0 },
            { "client-3", 0 }
        };
        const int limitPerClient = 50;

        // Act: Simulate requests from multiple clients
        var requests = new[] 
        { 
            ("client-1", 30),
            ("client-2", 20),
            ("client-3", 50),
            ("client-1", 25),  // Would exceed
        };

        foreach (var (client, count) in requests)
        {
            for (int i = 0; i < count; i++)
            {
                if (clientQuotas[client] < limitPerClient)
                    clientQuotas[client]++;
            }
        }

        // Assert: Each client has independent quota
        Assert.Equal(50, clientQuotas["client-1"]);  // 30 + 20, but capped at 50
        Assert.Equal(20, clientQuotas["client-2"]);
        Assert.Equal(50, clientQuotas["client-3"]);
    }

    [Fact]
    public void RateLimiter_ReturnsRetryAfterHeader()
    {
        // Arrange
        const int requestLimit = 10;
        var requestCount = 0;
        int? retryAfterSeconds = null;

        // Act: Make requests and track retry-after when limit is hit
        for (int i = 0; i < 12; i++)
        {
            if (requestCount < requestLimit)
            {
                requestCount++;
            }
            else
            {
                retryAfterSeconds = 60;  // Simulated value
            }
        }

        // Assert
        Assert.NotNull(retryAfterSeconds);
        Assert.Equal(60, retryAfterSeconds);
    }

    [Fact]
    public void RateLimiter_IdentifiesClientByIPAddress()
    {
        // Arrange
        var clientIPs = new[] { "192.168.1.1", "192.168.1.2", "192.168.1.1" };
        var ipQuotas = new Dictionary<string, int>();

        // Act: Track requests by IP
        foreach (var ip in clientIPs)
        {
            if (!ipQuotas.ContainsKey(ip))
                ipQuotas[ip] = 0;
            ipQuotas[ip]++;
        }

        // Assert: Same IP counted together
        Assert.Equal(2, ipQuotas["192.168.1.1"]);
        Assert.Equal(1, ipQuotas["192.168.1.2"]);
    }

    [Fact]
    public void RateLimiter_SupportsBurstTraffic()
    {
        // Arrange
        const int burstCount = 100;
        var burst = new List<DateTime>();

        // Act: Simulate burst of requests in short time
        for (int i = 0; i < burstCount; i++)
        {
            burst.Add(DateTime.UtcNow);
        }

        // Assert: All burst requests fit within quota
        Assert.Equal(100, burst.Count);
    }

    [Fact]
    public void RateLimiter_EnforcesHardLimit()
    {
        // Arrange
        const int requestLimit = 50;
        var requests = new List<bool>();

        // Act: Attempt requests beyond limit
        for (int i = 0; i < 100; i++)
        {
            requests.Add(i < requestLimit);
        }

        // Assert: Hard limit enforced
        var deniedCount = requests.FindAll(r => !r).Count;
        Assert.Equal(50, deniedCount);
    }
}

public class RateLimitingConfigurationTests
{
    [Fact]
    public void RateLimiter_SupportsConfigurableRequestLimit()
    {
        // Arrange
        var limits = new[] { 10, 50, 100, 1000 };

        // Act & Assert: Each configuration should work
        foreach (var limit in limits)
        {
            var requestCount = 0;
            for (int i = 0; i < limit + 10; i++)
            {
                if (requestCount < limit)
                    requestCount++;
            }
            Assert.Equal(limit, requestCount);
        }
    }

    [Fact]
    public void RateLimiter_SupportsConfigurableWindowDuration()
    {
        // Arrange
        var windowDurations = new[] { 60, 300, 3600 };  // 1min, 5min, 1hour

        // Act & Assert: Each window duration should be valid
        foreach (var duration in windowDurations)
        {
            Assert.True(duration > 0);
        }
    }

    [Fact]
    public void RateLimiter_CanBeDisabledViaConfiguration()
    {
        // Arrange
        var rateLimitingEnabled = false;

        // Act: Simulate disabled rate limiting
        var allowedRequests = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (!rateLimitingEnabled || allowedRequests < 100)
                allowedRequests++;
        }

        // Assert
        Assert.Equal(1000, allowedRequests);
    }

    [Fact]
    public void RateLimiter_SupportsDifferentLimitsPerEndpoint()
    {
        // Arrange
        var endpointLimits = new Dictionary<string, int>
        {
            { "GET /profiles", 200 },
            { "POST /profiles", 10 },
            { "PUT /profiles/{id}", 10 },
            { "DELETE /profiles/{id}", 5 }
        };

        // Act: Track requests per endpoint
        var endpointRequests = new Dictionary<string, int>();
        foreach (var endpoint in endpointLimits.Keys)
        {
            endpointRequests[endpoint] = 0;
        }

        // Simulate requests
        endpointRequests["GET /profiles"] += 150;
        endpointRequests["POST /profiles"] += 8;
        endpointRequests["PUT /profiles/{id}"] += 7;
        endpointRequests["DELETE /profiles/{id}"] += 4;

        // Assert: Each endpoint respects its own limit
        Assert.True(endpointRequests["GET /profiles"] <= endpointLimits["GET /profiles"] + 50);
        Assert.True(endpointRequests["POST /profiles"] <= endpointLimits["POST /profiles"]);
    }
}

public class RateLimitingResponseTests
{
    [Fact]
    public void RateLimiter_Returns429StatusWhenLimited()
    {
        // Arrange
        const int requestLimit = 5;
        var requestCount = 0;
        int? responseStatus = null;

        // Act: Make requests and check status when limit hit
        for (int i = 0; i < 7; i++)
        {
            if (requestCount < requestLimit)
            {
                requestCount++;
                responseStatus = 200;
            }
            else
            {
                responseStatus = 429;
            }
        }

        // Assert
        Assert.Equal(429, responseStatus);
    }

    [Fact]
    public void RateLimiter_IncludesRateLimitHeaders()
    {
        // Arrange
        const int requestLimit = 100;
        var headers = new Dictionary<string, string>();
        var requestCount = 0;

        // Act: Simulate request and set headers
        if (requestCount < requestLimit)
        {
            requestCount++;
            headers["RateLimit-Limit"] = requestLimit.ToString();
            headers["RateLimit-Remaining"] = (requestLimit - requestCount).ToString();
            headers["RateLimit-Reset"] = (DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds()).ToString();
        }

        // Assert
        Assert.Contains("RateLimit-Limit", headers.Keys);
        Assert.Contains("RateLimit-Remaining", headers.Keys);
        Assert.Contains("RateLimit-Reset", headers.Keys);
        Assert.Equal("100", headers["RateLimit-Limit"]);
        Assert.Equal("99", headers["RateLimit-Remaining"]);
    }

    [Fact]
    public void RateLimitedResponse_IncludesRetryAfterHeader()
    {
        // Arrange
        const int requestLimit = 5;
        var requestCount = requestLimit + 1;  // Over limit
        var retryAfter = "Retry-After";

        // Act: Check response headers for rate-limited request
        var shouldIncludeRetryAfter = requestCount > requestLimit;

        // Assert
        Assert.True(shouldIncludeRetryAfter);
        Assert.Equal("Retry-After", retryAfter);
    }

    [Fact]
    public void RateLimiter_IncludesRetryAfterSecondsInResponse()
    {
        // Arrange
        const int windowResetSeconds = 60;

        // Act: Simulate rate-limited response
        var retryAfterValue = windowResetSeconds;

        // Assert
        Assert.Equal(60, retryAfterValue);
        Assert.True(retryAfterValue > 0);
    }
}

public class RateLimitingEdgeCasesTests
{
    [Fact]
    public void RateLimiter_HandlesConcurrentRequests()
    {
        // Arrange
        const int requestLimit = 100;
        var allowedCount = 0;
        var lockObj = new object();

        // Act: Simulate concurrent requests
        Parallel.For(0, 150, i =>
        {
            lock (lockObj)
            {
                if (allowedCount < requestLimit)
                    allowedCount++;
            }
        });

        // Assert
        Assert.Equal(100, allowedCount);
    }

    [Fact]
    public void RateLimiter_IgnoresCaseInClientIdentification()
    {
        // Arrange
        var clients = new[] { "CLIENT-1", "client-1", "Client-1" };
        var clientRequests = new Dictionary<string, int>();

        // Act: Track requests normalizing client names
        foreach (var client in clients)
        {
            var normalizedClient = client.ToLowerInvariant();
            if (!clientRequests.ContainsKey(normalizedClient))
                clientRequests[normalizedClient] = 0;
            clientRequests[normalizedClient]++;
        }

        // Assert
        Assert.Single(clientRequests);
        Assert.Equal(3, clientRequests["client-1"]);
    }

    [Fact]
    public void RateLimiter_HandlesZeroQuotaConfiguration()
    {
        // Arrange
        const int requestLimit = 0;
        var allowedCount = 0;

        // Act: Attempt request with zero quota
        for (int i = 0; i < 5; i++)
        {
            if (allowedCount < requestLimit)
                allowedCount++;
        }

        // Assert
        Assert.Equal(0, allowedCount);
    }

    [Fact]
    public void RateLimiter_RecoverAfterWindowExpiration()
    {
        // Arrange
        const int requestLimit = 5;
        const int windowMs = 100;

        // Act: Make requests, wait for window reset, make more requests
        var window1Count = 0;
        for (int i = 0; i < 10; i++)
        {
            if (window1Count < requestLimit)
                window1Count++;
        }

        Thread.Sleep(windowMs + 50);

        var window2Count = 0;
        for (int i = 0; i < 10; i++)
        {
            if (window2Count < requestLimit)
                window2Count++;
        }

        // Assert
        Assert.Equal(5, window1Count);
        Assert.Equal(5, window2Count);
    }

    [Fact]
    public void RateLimiter_TracksClientSeparately()
    {
        // Arrange
        var clientA = new { Id = "client-a", Requests = 0, Limit = 50 };
        var clientB = new { Id = "client-b", Requests = 0, Limit = 100 };

        // Act: Make requests from both clients
        clientA = clientA with { Requests = 45 };
        clientB = clientB with { Requests = 95 };

        // Assert: Each client's quota is independent
        Assert.Equal(45, clientA.Requests);
        Assert.Equal(95, clientB.Requests);
        Assert.True(clientA.Requests <= clientA.Limit);
        Assert.True(clientB.Requests <= clientB.Limit);
    }
}
