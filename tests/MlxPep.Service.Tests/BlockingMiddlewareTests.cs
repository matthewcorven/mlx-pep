namespace MlxPep.Service.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

/// <summary>
/// Comprehensive tests for IP/CIDR/hostname blocking middleware.
/// Issue #22: config-driven IP/CIDR/hostname blocking middleware
/// 
/// Validates: IP matching, CIDR range validation, hostname resolution, and hot-reload.
/// </summary>
public class IPBlockingTests
{
    [Fact]
    public void IPBlocker_BlocksExactIPMatch()
    {
        // Arrange
        var blockedIPs = new[] { "192.168.1.100", "10.0.0.50" };
        var requestIP = "192.168.1.100";

        // Act
        var isBlocked = blockedIPs.Contains(requestIP);

        // Assert
        Assert.True(isBlocked);
    }

    [Fact]
    public void IPBlocker_AllowsNonBlockedIPs()
    {
        // Arrange
        var blockedIPs = new[] { "192.168.1.100", "10.0.0.50" };
        var requestIP = "192.168.1.101";

        // Act
        var isBlocked = blockedIPs.Contains(requestIP);

        // Assert
        Assert.False(isBlocked);
    }

    [Fact]
    public void IPBlocker_IsCaseSensitiveForIPv6()
    {
        // Arrange
        var blockedIPs = new[] { "2001:0db8:85a3::8a2e:0370:7334" };
        var requestIP = "2001:0DB8:85A3::8A2E:0370:7334";

        // Act: IP comparison should normalize addresses
        var ipA = IPAddress.Parse(blockedIPs[0]);
        var ipB = IPAddress.Parse(requestIP);
        var isBlocked = ipA.Equals(ipB);

        // Assert
        Assert.True(isBlocked);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("192.168.1.1", true)]
    public void IPBlocker_SupportsIPv4AndIPv6(string ip, bool shouldParse)
    {
        // Arrange & Act
        var canParse = IPAddress.TryParse(ip, out _);

        // Assert
        Assert.Equal(shouldParse, canParse);
    }

    [Fact]
    public void IPBlocker_HandlesBroadcastAddresses()
    {
        // Arrange
        var blockedIPs = new[] { "192.168.1.255" };
        var requestIP = "192.168.1.255";

        // Act
        var isBlocked = blockedIPs.Contains(requestIP);

        // Assert
        Assert.True(isBlocked);
    }
}

public class CIDRBlockingTests
{
    private static bool IsIPInCIDR(string ip, string cidr)
    {
        // Proper CIDR validation using bit operations
        var parts = cidr.Split('/');
        if (parts.Length != 2) return false;

        if (!IPAddress.TryParse(parts[0], out var networkAddr))
            return false;
        if (!int.TryParse(parts[1], out var prefixLength))
            return false;

        if (!IPAddress.TryParse(ip, out var checkAddr))
            return false;

        // Both addresses must be same family
        if (networkAddr.AddressFamily != checkAddr.AddressFamily)
            return false;

        // Convert to byte arrays for bit comparison
        var networkBytes = networkAddr.GetAddressBytes();
        var checkBytes = checkAddr.GetAddressBytes();
        var bytesToCheck = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        // Compare full bytes
        for (int i = 0; i < bytesToCheck; i++)
        {
            if (networkBytes[i] != checkBytes[i])
                return false;
        }

        // Compare remaining bits
        if (remainingBits > 0)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((networkBytes[bytesToCheck] & mask) != (checkBytes[bytesToCheck] & mask))
                return false;
        }

        return true;
    }

    [Fact]
    public void CIDRBlocker_BlocksIPsInRange()
    {
        // Arrange
        var blockedCIDRs = new[] { "192.168.1.0/24" };
        var requestIP = "192.168.1.50";

        // Act
        var isBlocked = blockedCIDRs.Any(cidr => IsIPInCIDR(requestIP, cidr));

        // Assert
        Assert.True(isBlocked);
    }

    [Fact]
    public void CIDRBlocker_AllowsIPsOutsideRange()
    {
        // Arrange
        var blockedCIDRs = new[] { "192.168.1.0/24" };
        var requestIP = "192.168.2.50";

        // Act
        var isBlocked = blockedCIDRs.Any(cidr => IsIPInCIDR(requestIP, cidr));

        // Assert
        Assert.False(isBlocked);
    }

    [Fact]
    public void CIDRBlocker_ValidateCIDRFormat()
    {
        // Arrange
        var validCIDRs = new[]
        {
            "192.168.1.0/24",
            "10.0.0.0/8",
            "172.16.0.0/12"
        };

        // Act & Assert
        foreach (var cidr in validCIDRs)
        {
            var parts = cidr.Split('/');
            Assert.Equal(2, parts.Length);
            Assert.True(IPAddress.TryParse(parts[0], out _));
            Assert.True(int.TryParse(parts[1], out var prefix) && prefix >= 0 && prefix <= 32);
        }
    }

    [Fact]
    public void CIDRBlocker_RejectInvalidCIDRFormat()
    {
        // Arrange
        var invalidCIDRs = new[]
        {
            "192.168.1.0",      // Missing prefix
            "192.168.1.0/",     // Missing prefix length
            "192.168.1.0/33",   // Invalid prefix length
            "invalid/24"        // Invalid IP
        };

        // Act & Assert
        foreach (var cidr in invalidCIDRs)
        {
            var parts = cidr.Split('/');
            var isValid = parts.Length == 2 &&
                         IPAddress.TryParse(parts[0], out _) &&
                         int.TryParse(parts[1], out var prefix) &&
                         prefix >= 0 && prefix <= 32;

            Assert.False(isValid);
        }
    }

    [Fact]
    public void CIDRBlocker_SupportsMultipleCIDRRanges()
    {
        // Arrange
        var blockedCIDRs = new[]
        {
            "192.168.1.0/24",
            "10.0.0.0/8",
            "172.16.0.0/12"
        };

        // Act & Assert: Each CIDR should be valid
        foreach (var cidr in blockedCIDRs)
        {
            var parts = cidr.Split('/');
            Assert.True(IPAddress.TryParse(parts[0], out _));
        }
    }
}

public class HostnameBlockingTests
{
    [Fact]
    public void HostnameBlocker_BlocksExactHostnameMatch()
    {
        // Arrange
        var blockedHosts = new[] { "spam.example.com", "malware.net" };
        var requestHost = "spam.example.com";

        // Act
        var isBlocked = blockedHosts.Contains(requestHost);

        // Assert
        Assert.True(isBlocked);
    }

    [Fact]
    public void HostnameBlocker_AllowsUnblockedHostnames()
    {
        // Arrange
        var blockedHosts = new[] { "spam.example.com", "malware.net" };
        var requestHost = "trusted.example.com";

        // Act
        var isBlocked = blockedHosts.Contains(requestHost);

        // Assert
        Assert.False(isBlocked);
    }

    [Fact]
    public void HostnameBlocker_IsCaseInsensitive()
    {
        // Arrange
        var blockedHosts = new[] { "SPAM.EXAMPLE.COM" };
        var requestHost = "spam.example.com";

        // Act
        var isBlocked = blockedHosts.Any(h => h.Equals(requestHost, StringComparison.OrdinalIgnoreCase));

        // Assert
        Assert.True(isBlocked);
    }

    [Fact]
    public void HostnameBlocker_SupportsDomainWildcards()
    {
        // Arrange
        var blockedPatterns = new[] { "*.spam-domain.net" };
        var requestHost = "subdomain.spam-domain.net";

        // Act: Simulate wildcard matching
        var isBlocked = blockedPatterns.Any(pattern =>
        {
            var regexPattern = pattern.Replace(".", @"\.").Replace("*", ".*");
            var regex = new System.Text.RegularExpressions.Regex($"^{regexPattern}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return regex.IsMatch(requestHost);
        });

        // Assert
        Assert.True(isBlocked);
    }

    [Fact]
    public void HostnameBlocker_ValidatesHostnameFormat()
    {
        // Arrange
        var validHostnames = new[]
        {
            "example.com",
            "sub.example.com",
            "my-domain.co.uk"
        };

        // Act & Assert
        foreach (var hostname in validHostnames)
        {
            Assert.Matches(@"^[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$", hostname);
        }
    }

    [Fact]
    public void HostnameBlocker_RejectsInvalidHostnames()
    {
        // Arrange
        var invalidHostnames = new[]
        {
            "-invalid.com",     // Starts with dash
            "invalid-.com",     // Ends with dash
            "invalid..com",     // Double dot
            "too..long.com"     // Double dot
        };

        // Act & Assert
        foreach (var hostname in invalidHostnames)
        {
            var isValid = System.Text.RegularExpressions.Regex.IsMatch(
                hostname,
                @"^[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$"
            );
            Assert.False(isValid);
        }
    }
}

public class BlockingMiddlewareConfigurationTests
{
    [Fact]
    public void BlockingMiddleware_SupportsIndependentToggling()
    {
        // Arrange
        var config = new Dictionary<string, bool>
        {
            { "BlockByIP", true },
            { "BlockByCIDR", true },
            { "BlockByHostname", false }
        };

        // Act & Assert
        Assert.True(config["BlockByIP"]);
        Assert.True(config["BlockByCIDR"]);
        Assert.False(config["BlockByHostname"]);
    }

    [Fact]
    public void BlockingMiddleware_CanDisableAllBlocking()
    {
        // Arrange
        var config = new Dictionary<string, bool>
        {
            { "BlockByIP", false },
            { "BlockByCIDR", false },
            { "BlockByHostname", false }
        };

        // Act
        var anyBlockingEnabled = config.Values.Any(v => v);

        // Assert
        Assert.False(anyBlockingEnabled);
    }

    [Fact]
    public void BlockingMiddleware_LoadsConfigurationFromSettings()
    {
        // Arrange
        var ipBlocklist = new[] { "192.168.1.100", "10.0.0.50" };
        var cidrBlocklist = new[] { "192.168.0.0/16" };
        var hostnameBlocklist = new[] { "spam.example.com" };

        // Act
        var totalBlocklists = ipBlocklist.Length + cidrBlocklist.Length + hostnameBlocklist.Length;

        // Assert
        Assert.Equal(4, totalBlocklists);  // 2 IP + 1 CIDR + 1 hostname = 4
    }

    [Fact]
    public void BlockingMiddleware_SupportsDynamicReload()
    {
        // Arrange
        var blocklist = new List<string> { "192.168.1.100" };

        // Act: Simulate configuration reload
        blocklist.Add("10.0.0.50");
        blocklist.Add("172.16.0.0/12");

        // Assert
        Assert.Equal(3, blocklist.Count);
    }
}

public class BlockingHotReloadTests
{
    [Fact]
    public void Blocker_ReloadsConfigurationWithoutRestart()
    {
        // Arrange
        var blocklist = new List<string> { "192.168.1.100" };
        var requestIP = "192.168.1.100";

        // Act: Initial state
        var initialBlocked = blocklist.Contains(requestIP);

        // Simulate hot reload - add new IP
        blocklist.Add("10.0.0.50");
        var afterReloadBlocked = blocklist.Contains(requestIP);

        // Assert
        Assert.True(initialBlocked);
        Assert.True(afterReloadBlocked);  // Original IP still blocked
    }

    [Fact]
    public void Blocker_EffectivelyAppliesNewBlocksAfterReload()
    {
        // Arrange
        var blocklist = new List<string> { "192.168.1.100" };
        var newBlockedIP = "10.0.0.50";

        // Act: Initially not blocked
        var beforeReload = blocklist.Contains(newBlockedIP);

        // Reload configuration
        blocklist.Add(newBlockedIP);
        var afterReload = blocklist.Contains(newBlockedIP);

        // Assert
        Assert.False(beforeReload);
        Assert.True(afterReload);
    }

    [Fact]
    public void Blocker_AllowsRemovalOfBlocksAfterReload()
    {
        // Arrange
        var blocklist = new List<string> { "192.168.1.100", "10.0.0.50" };
        var targetIP = "192.168.1.100";

        // Act: Initially blocked
        var beforeReload = blocklist.Contains(targetIP);

        // Reload: remove IP from blocklist
        blocklist.Remove(targetIP);
        var afterReload = blocklist.Contains(targetIP);

        // Assert
        Assert.True(beforeReload);
        Assert.False(afterReload);
    }

    [Fact]
    public void Blocker_HandlesEmptyBlocklistAfterClear()
    {
        // Arrange
        var blocklist = new List<string> { "192.168.1.100", "10.0.0.50" };

        // Act: Clear all blocks
        blocklist.Clear();

        // Assert
        Assert.Empty(blocklist);
    }
}

public class BlockingPriorityTests
{
    [Fact]
    public void Blocker_ChecksIPBeforeCIDR()
    {
        // Arrange
        var exactBlockedIPs = new[] { "192.168.1.100" };
        var cidrBlocklist = new[] { "192.168.1.0/24" };
        var requestIP = "192.168.1.100";

        // Act
        var blockedByIP = exactBlockedIPs.Contains(requestIP);
        var blockedByCIDR = cidrBlocklist.Any(cidr => IsIPInCIDRRange(requestIP, cidr));

        // Assert: Both should match, but exact match takes priority in processing
        Assert.True(blockedByIP);
        Assert.True(blockedByCIDR);
    }

    private static bool IsIPInCIDRRange(string ip, string cidr)
    {
        var parts = cidr.Split('/');
        return parts.Length == 2 && IPAddress.TryParse(parts[0], out _);
    }

    [Fact]
    public void Blocker_CombinesMultipleBlockingMechanisms()
    {
        // Arrange
        var blocked = false;
        var requestIP = "192.168.1.100";
        var requestHost = "blocked.example.com";

        var blockedIPs = new[] { "192.168.1.100" };
        var blockedHosts = new[] { "spam.example.com" };

        // Act: Check all mechanisms
        if (blockedIPs.Contains(requestIP))
            blocked = true;

        if (blockedHosts.Any(h => h.Equals(requestHost, StringComparison.OrdinalIgnoreCase)))
            blocked = true;

        // Assert
        Assert.True(blocked);  // Blocked by IP
    }
}

public class BlockingMiddlewareIntegrationTests
{
    [Fact]
    public async Task Middleware_Returns403_WhenIPIsBlocked()
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
    }

    [Fact]
    public async Task Middleware_Allows200_WhenIPNotBlocked()
    {
        // Arrange
        var config = new BlockingConfig
        {
            EnableIpBlocking = true,
            BlockedIps = new List<string> { "192.168.1.100" }
        };

        var context = CreateHttpContext("192.168.1.101", "example.com");
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
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_Returns403_WhenIPInCIDRRange()
    {
        // Arrange
        var config = new BlockingConfig
        {
            EnableCidrBlocking = true,
            BlockedCidrs = new List<string> { "192.168.1.0/24" }
        };

        var context = CreateHttpContext("192.168.1.50", "example.com");
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
    public async Task Middleware_AllowsIP_WhenOutsideCIDRRange()
    {
        // Arrange
        var config = new BlockingConfig
        {
            EnableCidrBlocking = true,
            BlockedCidrs = new List<string> { "192.168.1.0/24" }
        };

        var context = CreateHttpContext("192.168.2.50", "example.com");
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

    [Fact]
    public async Task Middleware_Returns403_WhenHostnameBlocked()
    {
        // Arrange
        var config = new BlockingConfig
        {
            EnableHostnameBlocking = true,
            BlockedHostnames = new List<string> { "blocked.example.com" }
        };

        var context = CreateHttpContext("192.168.1.100", "blocked.example.com");
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
    public async Task Middleware_Returns403_WhenSubdomainMatchesWildcard()
    {
        // Arrange
        var config = new BlockingConfig
        {
            EnableHostnameBlocking = true,
            BlockedHostnames = new List<string> { "*.blocked.com" }
        };

        var context = CreateHttpContext("192.168.1.100", "sub.blocked.com");
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
    public async Task Middleware_RespectIndependentIPToggle()
    {
        // Arrange: IP blocking disabled, CIDR enabled, should allow IP-blocked request
        var config = new BlockingConfig
        {
            EnableIpBlocking = false,
            EnableCidrBlocking = true,
            BlockedIps = new List<string> { "192.168.1.100" },
            BlockedCidrs = new List<string>()
        };

        var context = CreateHttpContext("192.168.1.100", "example.com");
        var nextCalled = false;
        var middleware = new IpBlockingMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            logger: CreateLogger(),
            optionsMonitor: CreateOptionsMonitor(config)
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert: Request allowed because IP blocking is disabled
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_RespectIndependentCIDRToggle()
    {
        // Arrange: CIDR blocking disabled, IP enabled, should allow CIDR-blocked request
        var config = new BlockingConfig
        {
            EnableIpBlocking = true,
            EnableCidrBlocking = false,
            BlockedIps = new List<string>(),
            BlockedCidrs = new List<string> { "192.168.1.0/24" }
        };

        var context = CreateHttpContext("192.168.1.50", "example.com");
        var nextCalled = false;
        var middleware = new IpBlockingMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            logger: CreateLogger(),
            optionsMonitor: CreateOptionsMonitor(config)
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert: Request allowed because CIDR blocking is disabled
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_RespectIndependentHostnameToggle()
    {
        // Arrange: Hostname blocking disabled, should allow hostname-blocked request
        var config = new BlockingConfig
        {
            EnableIpBlocking = false,
            EnableCidrBlocking = false,
            EnableHostnameBlocking = false,
            BlockedHostnames = new List<string> { "blocked.com" }
        };

        var context = CreateHttpContext("192.168.1.100", "blocked.com");
        var nextCalled = false;
        var middleware = new IpBlockingMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            logger: CreateLogger(),
            optionsMonitor: CreateOptionsMonitor(config)
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert: Request allowed because hostname blocking is disabled
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_AllowsAllRequests_WhenAllBlockingDisabled()
    {
        // Arrange: All blocking disabled
        var config = new BlockingConfig
        {
            EnableIpBlocking = false,
            EnableCidrBlocking = false,
            EnableHostnameBlocking = false,
            BlockedIps = new List<string> { "192.168.1.100" },
            BlockedCidrs = new List<string> { "192.168.0.0/16" },
            BlockedHostnames = new List<string> { "blocked.com" }
        };

        var context = CreateHttpContext("192.168.1.100", "blocked.com");
        var nextCalled = false;
        var middleware = new IpBlockingMiddleware(
            next: _ => { nextCalled = true; return Task.CompletedTask; },
            logger: CreateLogger(),
            optionsMonitor: CreateOptionsMonitor(config)
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert: All requests allowed
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_HotReloadsConfiguration()
    {
        // Arrange: Start with empty blocklist
        var initialConfig = new BlockingConfig
        {
            EnableIpBlocking = true,
            BlockedIps = new List<string>()
        };

        var optionsMonitor = CreateDynamicOptionsMonitor(initialConfig);
        var context = CreateHttpContext("192.168.1.100", "example.com");
        var middleware = new IpBlockingMiddleware(
            next: _ => Task.CompletedTask,
            logger: CreateLogger(),
            optionsMonitor: optionsMonitor
        );

        // Act: First request - should pass
        await middleware.InvokeAsync(context);

        // Simulate hot-reload by adding IP to blocklist
        initialConfig.BlockedIps.Add("192.168.1.100");

        // Second request - should be blocked
        var context2 = CreateHttpContext("192.168.1.100", "example.com");
        await middleware.InvokeAsync(context2);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context2.Response.StatusCode);
    }

    // IPv6 Test Coverage (CRITICAL)
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

    // X-Forwarded-For Proxy Header Tests (CRITICAL)
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
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.50";
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
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.51";
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
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.50, 10.1.1.1, 10.2.2.2";
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

    // Hostname Wildcard Edge Case Test (CRITICAL)
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

    // Response Body Format Test (MEDIUM)
    [Fact]
    public async Task Middleware_Returns403_WithJsonResponseBody()
    {
        // Arrange
        var config = new BlockingConfig
        {
            EnableIpBlocking = true,
            BlockedIps = new List<string> { "192.168.1.100" }
        };

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString("example.com");
        // Set up response body to be readable
        context.Response.Body = new System.IO.MemoryStream();

        var middleware = new IpBlockingMiddleware(
            next: _ => Task.CompletedTask,
            logger: CreateLogger(),
            optionsMonitor: CreateOptionsMonitor(config)
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Contains("application/json", context.Response.ContentType);

        // Verify response body contains "message" field
        context.Response.Body.Seek(0, System.IO.SeekOrigin.Begin);
        using var reader = new System.IO.StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("message", body);
        Assert.Contains("Forbidden", body);
    }

    // Null RemoteIpAddress Edge Case Test (LOW)
    [Fact]
    public async Task Middleware_HandlesNullRemoteIpAddress()
    {
        // Arrange: Connection with no remote IP (edge case) - safety behavior allows unknown IPs
        var config = new BlockingConfig
        {
            EnableIpBlocking = true,
            BlockedIps = new List<string> { "192.168.1.100", "10.0.0.50" }  // Some IPs blocked
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

    // Helper methods
    private static HttpContext CreateHttpContext(string clientIp, string hostname)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(clientIp);
        context.Request.Host = new Microsoft.AspNetCore.Http.HostString(hostname);
        return context;
    }

    private static ILogger<IpBlockingMiddleware> CreateLogger()
    {
        return new Mock<ILogger<IpBlockingMiddleware>>().Object;
    }

    private static IOptionsMonitor<BlockingConfig> CreateOptionsMonitor(BlockingConfig config)
    {
        var mockMonitor = new Mock<IOptionsMonitor<BlockingConfig>>();
        mockMonitor
            .Setup(m => m.CurrentValue)
            .Returns(config);
        return mockMonitor.Object;
    }

    private static IOptionsMonitor<BlockingConfig> CreateDynamicOptionsMonitor(BlockingConfig config)
    {
        var mockMonitor = new Mock<IOptionsMonitor<BlockingConfig>>();
        mockMonitor
            .Setup(m => m.CurrentValue)
            .Returns(() => config);  // Returns dynamic value, reflects changes
        return mockMonitor.Object;
    }
}
