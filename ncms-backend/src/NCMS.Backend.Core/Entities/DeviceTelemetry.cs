using System;

namespace NCMS.Backend.Core.Entities;

public sealed class DeviceTelemetry
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Resource metrics
    public double CpuUsagePercent { get; set; }
    public double RamUsageMb { get; set; }
    public double RamTotalMb { get; set; }
    public double StorageUsedMb { get; set; }
    public double StorageTotalMb { get; set; }
    public long UptimeSeconds { get; set; }

    // Network & Environment
    public string? WanIp { get; set; }
    public double? TemperatureCelsius { get; set; }
    public double? SignalStrengthRssi { get; set; }
    public double? SignalQualityRsrp { get; set; }
}
