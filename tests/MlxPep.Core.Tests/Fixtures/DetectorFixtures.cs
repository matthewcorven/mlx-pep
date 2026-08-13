namespace MlxPep.Core.Tests.Fixtures;

/// <summary>
/// Test fixtures for detector unit tests.
/// Issue #10: Provides mocked subprocess output and log data for deterministic testing.
/// </summary>
public static class DetectorFixtures
{
    /// <summary>
    /// Sample system_profiler SPHardwareDataType SPStorageDataType output.
    /// Captured from real macOS Sonoma on Apple Silicon M3 Pro.
    /// </summary>
    public const string SystemProfilerHardwareOutput = @"Hardware Overview:

      Model Name: MacBook Pro
      Model Identifier: MacBookPro18,1
      Model Year: 2023
      Chip: Apple M3 Pro
      Total Number of Cores: 12 (8 performance and 4 efficiency)
      Memory: 18 GB
      System Firmware Version: 13.5.2
      OS Loader Version: 13.5.2
      Serial Number (system): XYZ123ABC456

Storage:

      /dev/disk0 (internal, physical):
          APPLE SSD SM0512F Media:
          Size: 512 GB
          BSD Name: disk0
          Content: Apple_partition_scheme
          +---------+--------+-------+
              #:    TYPE NAME             SIZE       IDENTIFIER
              0:    GUID_partition_scheme                  *512.1 GB   disk0
              1:                         EFI                209.7 MB   disk0s1
              2:            Apple_APFS Container disk1      512.0 GB   disk0s2
          +---------+--------+-------+
          Capacity: 512 GB
          Free: 245.3 GB
          Used: 266.7 GB";

    /// <summary>
    /// Sample sysctl iogpu.wired_limit_mb output.
    /// </summary>
    public const string SysctlWiredLimitOutput = "iogpu.wired_limit_mb: 6144";

    /// <summary>
    /// Empty sysctl output (no wired limit value).
    /// </summary>
    public const string SysctlWiredLimitEmpty = "";

    /// <summary>
    /// Sample oMLX config.json fixture.
    /// </summary>
    public const string OmlxConfigJson = @"{
  ""base_path"": ""/Users/testuser/oMLX/models"",
  ""port"": 8000,
  ""model_dir"": ""huggingface""
}";

    /// <summary>
    /// Sample oMLX server.log (last 50 lines showing guard tier, ceiling, metal cap, wired limit).
    /// Shows multiple log entries with progressively updated values.
    /// </summary>
    public const string OmlxServerLog = @"[2026-08-13 00:15:23] Model loaded: meta-llama/Llama-2-7b
[2026-08-13 00:15:24] Starting inference server on port 8000
[2026-08-13 00:15:30] System probe: Memory guard tier: balanced
[2026-08-13 00:15:31] Detected GPU: Metal backend
[2026-08-13 00:15:32] Hardware config: ceiling=8.0GB, metal_cap=4.0GB, iogpu.wired_limit_mb=6144
[2026-08-13 00:15:33] Server ready
[2026-08-13 00:30:00] Request /inference (user_id: abc123)
[2026-08-13 00:35:00] Response sent, latency: 245ms
[2026-08-13 00:40:15] Memory guard tier: high
[2026-08-13 00:40:16] Adjusting ceiling=6.0GB based on load
[2026-08-13 00:40:17] Metal cap (2.5GB) insufficient for request
[2026-08-13 00:40:18] Updating iogpu.wired_limit_mb=4096
[2026-08-13 00:45:00] Request /inference (user_id: def456)
[2026-08-13 00:50:00] Response sent, latency: 156ms";

    /// <summary>
    /// Empty oMLX server.log (no entries).
    /// </summary>
    public const string OmlxServerLogEmpty = "";

    /// <summary>
    /// oMLX server.log with partial data (missing guard tier).
    /// </summary>
    public const string OmlxServerLogNoGuardTier = @"[2026-08-13 00:15:23] Model loaded: meta-llama/Llama-2-7b
[2026-08-13 00:15:24] Starting inference server on port 8000
[2026-08-13 00:15:32] Hardware config: ceiling=8.0GB, metal_cap=4.0GB, iogpu.wired_limit_mb=6144
[2026-08-13 00:15:33] Server ready";

    /// <summary>
    /// oMLX server.log with minimal data (only guard tier).
    /// </summary>
    public const string OmlxServerLogPartial = @"[2026-08-13 00:15:30] System probe: Memory guard tier: balanced
[2026-08-13 00:15:31] Detected GPU: Metal backend";

    // Expected parsing results from fixtures
    /// <summary>
    /// Expected values when parsing SystemProfilerHardwareOutput.
    /// </summary>
    public static readonly (string ModelName, string ModelIdentifier, string Chip, int MemoryGb,
        double? StorageFreeGb, int? StorageCapacityTb, int WiredLimitMb) ExpectedHardwareInfo =
        ("MacBook Pro", "MacBookPro18,1", "Apple M3 Pro", 18, 245.3, 0, 6144);  // 512 GB ≈ 0 TB when truncated to int

    /// <summary>
    /// Expected values when parsing OmlxServerLog.
    /// Note: Shows latest values in log (reverse-scan finds these last).
    /// </summary>
    public static readonly (string GuardTier, double? CeilingGb, double? MetalCapGb, int? WiredLimitMb)
        ExpectedOmlxLogValues = ("high", 6.0, 2.5, 4096);  // Latest values in log
}

