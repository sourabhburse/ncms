namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Structured device telemetry snapshot — resource utilization, connectivity, and environment metrics.
/// Distinct from <see cref="TelemetryRecord"/> which stores raw MQTT payloads.
/// </summary>
public sealed class DeviceTelemetry
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    // Resource metrics
    public double CpuUsagePercent { get; set; }
    public double RamUsageMb { get; set; }
    public double RamTotalMb { get; set; }
    public double StorageUsedMb { get; set; }
    public double StorageTotalMb { get; set; }
    public long UptimeSeconds { get; set; }

    // Network & environment
    public string? WanIp { get; set; }
    public double? TemperatureCelsius { get; set; }
    public double? SignalStrengthRssi { get; set; }
    public double? SignalQualityRsrp { get; set; }

    // Navigation
    public Device Device { get; set; } = null!;
}
