/// <summary>
/// Configuration for IP/CIDR/hostname blocking middleware.
/// Each blocking type can be independently enabled/disabled.
/// </summary>
public class BlockingConfig
{
    /// <summary>
    /// Enable blocking by specific IP addresses.
    /// </summary>
    public bool EnableIpBlocking { get; set; } = false;

    /// <summary>
    /// Enable blocking by CIDR ranges.
    /// </summary>
    public bool EnableCidrBlocking { get; set; } = false;

    /// <summary>
    /// Enable blocking by hostname.
    /// </summary>
    public bool EnableHostnameBlocking { get; set; } = false;

    /// <summary>
    /// List of IP addresses to block (e.g., "192.168.1.100").
    /// Only used if EnableIpBlocking is true.
    /// </summary>
    public List<string> BlockedIps { get; set; } = new();

    /// <summary>
    /// List of CIDR ranges to block (e.g., "192.168.1.0/24").
    /// Only used if EnableCidrBlocking is true.
    /// </summary>
    public List<string> BlockedCidrs { get; set; } = new();

    /// <summary>
    /// List of hostnames to block (e.g., "attacker.com", "*.malicious.net").
    /// Only used if EnableHostnameBlocking is true.
    /// </summary>
    public List<string> BlockedHostnames { get; set; } = new();
}
