using System;

namespace NCMS.Backend.Core.Dtos;

public record DeviceTelemetryResponse(
    Guid Id,
    Guid DeviceId,
    DateTime Timestamp,
    double CpuUsagePercent,
    double RamUsageMb,
    double RamTotalMb,
    double StorageUsedMb,
    double StorageTotalMb,
    long UptimeSeconds,
    string? WanIp,
    double? TemperatureCelsius,
    double? SignalStrengthRssi,
    double? SignalQualityRsrp
);
