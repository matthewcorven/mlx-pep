# Review Change Specifications for Tank — PR #60 Adversarial Review

## Changes for Tank to Apply

### 1. Add IPv6 Test Coverage (CRITICAL)

**File:** `tests/MlxPep.Service.Tests/BlockingMiddlewareTests.cs`

**Issue:** The middleware supports IPv6 addresses (code handles both IPv4 with /32 max and IPv6 with /128 max prefix lengths), but there are ZERO tests for IPv6. Production IPv6 requests would be untested.

**Fix:** Add these test methods to the `BlockingMiddlewareIntegrationTests` class:

```csharp
[Fact]
public async Task Middleware_Returns403_WhenIPv6IsBlocked()
{
    // Arrange
    var config = new BlockingConfig
    {
        EnableIpBlocking = true,
        BlockedIps = new List<string> { "2001:db8::1" }
    };

    var context = CreateHttpContext("2001:db8::1", "example.com");
    var middleware = new IpBlockingMiddleware(
        next: _ => Task.CompletedTask,
        logger: CreateLogger(),
        optionsMonitor: CreateOptionsMonitor(config)
    );

    // Act
    await middleware.InvokeAsync(context);

    // Assert
    Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
}

[Fact]
public async Task Middleware_Returns403_WhenIPv6InCIDRRange()
{
    // Arrange
    var config = new BlockingConfig
    {
        EnableCidrBlocking = true,
        BlockedCidrs = new List<string> { "2001:db8::/32" }
    };

    var context = CreateHttpContext("2001:db8::50", "example.com");
    var middleware = new IpBlockingMiddleware(
        next: _ => Task.CompletedTask,
        logger: CreateLogger(),
        optionsMonitor: CreateOptionsMonitor(config)
    );

    // Act
    await middleware.InvokeAsync(context);

    // Assert
    Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
}

[Fact]
public async Task Middleware_AllowsIPv6_WhenOutsideCIDRRange()
{
    // Arrange
    var config = new BlockingConfig
    {
        EnableCidrBlocking = true,
        BlockedCidrs = new List<string> { "2001:db8::/32" }
    };

    var context = CreateHttpContext("2001:db9::1", "example.com");
    var nextCalled = false;
    var middleware = new IpBlockingMiddleware(
        next: _ => { nextCalled = true; return Task.CompletedTask; },
        logger: CreateLogger(),
        optionsMonitor: CreateOptionsMonitor(config)
    );

    // Act
    await middleware.InvokeAsync(context);

    // Assert
    Assert.True(nextCalled);
}
```

---

### 2. Add X-Forwarded-For Proxy Header Test (CRITICAL)

**File:** `tests/MlxPep.Service.Tests/BlockingMiddlewareTests.cs`

**Issue:** The middleware respects X-Forwarded-For header (important for production proxy/load balancer environments), but there's NO test verifying this works end-to-end. An admin might enable blocking based on X-Forwarded-For without testing it.

**Fix:** Add these test methods to the `BlockingMiddlewareIntegrationTests` class:

```csharp
[Fact]
public async Task Middleware_RespectsXForwardedForHeader_BlocksForwardedIP()
{
    // Arrange: Real client IP is different from socket IP, blocked IP comes via X-Forwarded-For
    var config = new BlockingConfig
    {
        EnableIpBlocking = true,
        BlockedIps = new List<string> { "203.0.113.50" }  // Blocked IP from internet
    };

    var context = new DefaultHttpContext();
    // Socket shows proxy server IP
    context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");
    // But request came through with forwarded real client IP
    context.Request.Headers.Add("X-Forwarded-For", "203.0.113.50");
    context.Request.Host = new Microsoft.AspNetCore.Http.HostString("example.com");

    var middleware = new IpBlockingMiddleware(
        next: _ => Task.CompletedTask,
        logger: CreateLogger(),
        optionsMonitor: CreateOptionsMonitor(config)
    );

    // Act
    await middleware.InvokeAsync(context);

    // Assert: Should block based on X-Forwarded-For IP, not socket IP
    Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
}

[Fact]
public async Task Middleware_RespectsXForwardedForHeader_AllowsForwardedIP()
{
    // Arrange: Forwarded IP is not in blocklist
    var config = new BlockingConfig
    {
        EnableIpBlocking = true,
        BlockedIps = new List<string> { "203.0.113.50" }
    };

    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");
    // Request came from different IP via proxy
    context.Request.Headers.Add("X-Forwarded-For", "203.0.113.51");
    context.Request.Host = new Microsoft.AspNetCore.Http.HostString("example.com");

    var nextCalled = false;
    var middleware = new IpBlockingMiddleware(
        next: _ => { nextCalled = true; return Task.CompletedTask; },
        logger: CreateLogger(),
        optionsMonitor: CreateOptionsMonitor(config)
    );

    // Act
    await middleware.InvokeAsync(context);

    // Assert: Request allowed because forwarded IP is not blocked
    Assert.True(nextCalled);
}

[Fact]
public async Task Middleware_HandlesMultipleIPsInXForwardedForHeader()
{
    // Arrange: X-Forwarded-For can have multiple IPs (client, proxy1, proxy2)
    // Middleware should use the FIRST one (the original client IP)
    var config = new BlockingConfig
    {
        EnableIpBlocking = true,
        BlockedIps = new List<string> { "203.0.113.50" }
    };

    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");
    // Multiple IPs: client is first, followed by proxies
    context.Request.Headers.Add("X-Forwarded-For", "203.0.113.50, 10.1.1.1, 10.2.2.2");
    context.Request.Host = new Microsoft.AspNetCore.Http.HostString("example.com");

    var middleware = new IpBlockingMiddleware(
        next: _ => Task.CompletedTask,
        logger: CreateLogger(),
        optionsMonitor: CreateOptionsMonitor(config)
    );

    // Act
    await middleware.InvokeAsync(context);

    // Assert: Should block based on first IP in chain (the real client)
    Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
}
```

---

### 3. Clarify and Test Hostname Wildcard Edge Case (CRITICAL)

**File:** `src/MlxPep.Service/IpBlockingMiddleware.cs` and `tests/MlxPep.Service.Tests/BlockingMiddlewareTests.cs`

**Issue:** The current hostname wildcard matching logic allows the parent domain to match the wildcard. For example, `example.com` matches `*.example.com`. This is semantically ambiguous:

Current logic:
```csharp
if (pattern.StartsWith("*.", StringComparison.OrdinalIgnoreCase))
{
    var domain = pattern[2..];
    return hostname.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(hostname, domain, StringComparison.OrdinalIgnoreCase);  // ← This line allows parent match
}
```

From a security perspective, `*.example.com` typically means "block all subdomains of example.com" but NOT `example.com` itself. This should be clarified.

**Option A - Recommended Fix (strict wildcard - subdomain only):**

```csharp
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
```

Then add a test to verify parent domain does NOT match:

```csharp
[Fact]
public async Task Middleware_WildcardHostname_DoesNotMatchParentDomain()
{
    // Arrange: Wildcard pattern should NOT match parent domain
    var config = new BlockingConfig
    {
        EnableHostnameBlocking = true,
        BlockedHostnames = new List<string> { "*.blocked.com" }
    };

    var context = CreateHttpContext("192.168.1.100", "blocked.com");  // Parent domain
    var nextCalled = false;
    var middleware = new IpBlockingMiddleware(
        next: _ => { nextCalled = true; return Task.CompletedTask; },
        logger: CreateLogger(),
        optionsMonitor: CreateOptionsMonitor(config)
    );

    // Act
    await middleware.InvokeAsync(context);

    // Assert: Parent domain should NOT be blocked by wildcard
    Assert.True(nextCalled);
}
```

---

### 4. Add Test for Response Body Format (MEDIUM)

**File:** `tests/MlxPep.Service.Tests/BlockingMiddlewareTests.cs`

**Issue:** PR description says response body is `{"message": "Forbidden: Request blocked by IP blocking policy"}`, but the middleware uses different messages per blocking type. No test validates the JSON structure.

**Fix:** Add a test to verify response body structure:

```csharp
[Fact]
public async Task Middleware_Returns403_WithJsonResponseBody()
{
    // Arrange
    var config = new BlockingConfig
    {
        EnableIpBlocking = true,
        BlockedIps = new List<string> { "192.168.1.100" }
    };

    var context = CreateHttpContext("192.168.1.100", "example.com");
    var middleware = new IpBlockingMiddleware(
        next: _ => Task.CompletedTask,
        logger: CreateLogger(),
        optionsMonitor: CreateOptionsMonitor(config)
    );

    // Act
    await middleware.InvokeAsync(context);

    // Assert
    Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    Assert.Equal("application/json", context.Response.ContentType);
    
    // Verify response body contains "message" field
    context.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
    using var reader = new System.IO.StreamReader(context.Response.Body);
    var body = await reader.ReadToEndAsync();
    Assert.Contains("message", body);
    Assert.Contains("Forbidden", body);
}
```

---

### 5. Add Configuration Validation Logging (MEDIUM)

**File:** `src/MlxPep.Service/IpBlockingMiddleware.cs`

**Issue:** Malformed IP/CIDR entries are silently ignored (return false from validation). An admin could misconfigure blocking policies with no indication.

**Fix:** Add validation warnings in the middleware constructor or first invocation:

```csharp
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
```

---

### 6. Add "Unknown" IP Edge Case Test (LOW)

**File:** `tests/MlxPep.Service.Tests/BlockingMiddlewareTests.cs`

**Issue:** When `RemoteIpAddress` is null, middleware returns string `"unknown"`. No test covers this edge case.

**Fix:** Add test:

```csharp
[Fact]
public async Task Middleware_HandlesNullRemoteIpAddress()
{
    // Arrange: Connection with no remote IP (edge case)
    var config = new BlockingConfig
    {
        EnableIpBlocking = true,
        BlockedIps = new List<string> { "unknown" }  // Unlikely but possible
    };

    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = null;  // No IP address available
    context.Request.Host = new Microsoft.AspNetCore.Http.HostString("example.com");

    var nextCalled = false;
    var middleware = new IpBlockingMiddleware(
        next: _ => { nextCalled = true; return Task.CompletedTask; },
        logger: CreateLogger(),
        optionsMonitor: CreateOptionsMonitor(config)
    );

    // Act
    await middleware.InvokeAsync(context);

    // Assert: Should allow the request (safety behavior - don't block unknown IPs)
    Assert.True(nextCalled);
}
```

---

## Summary of Changes

| Priority | Issue | Fix |
|----------|-------|-----|
| **CRITICAL** | No IPv6 test coverage | Add 3 IPv6 integration tests |
| **CRITICAL** | No X-Forwarded-For test | Add 3 proxy header integration tests |
| **CRITICAL** | Wildcard hostname ambiguity | Clarify logic (remove parent domain match) + add test |
| **MEDIUM** | Response body not tested | Add JSON structure validation test |
| **MEDIUM** | Silent config validation failures | Add startup validation logging |
| **LOW** | Null RemoteIpAddress edge case | Add edge case test |

---

## Expected Outcome After Fixes

- ✅ IPv6 fully tested in real middleware invocation
- ✅ X-Forwarded-For proxy header behavior verified end-to-end
- ✅ Hostname wildcard semantics clarified and locked down
- ✅ Configuration errors surfaced at startup (not silent)
- ✅ API contract (response body JSON) verified
- ✅ Edge cases (null IPs) handled safely with tests

**Revised Completeness After Fixes:** 98%+ — Issue #22 fully satisfied with production-ready coverage.
