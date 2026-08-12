class RateLimitConfig
{
    public int DefaultLimit { get; set; } = 100;
    public int WindowSizeSeconds { get; set; } = 60;
    public Dictionary<string, int> EndpointLimits { get; set; } = new();
    public bool BypassLocalhost { get; set; } = true;
    public bool RespectXForwardedFor { get; set; } = true;
}
