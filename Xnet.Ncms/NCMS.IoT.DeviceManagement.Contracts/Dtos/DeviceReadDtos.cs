namespace NCMS.IoT.DeviceManagement.Contracts.Dtos;

// Read-side DTOs consumed by the management UI (NCMS.IoT.Host) and exposed over the
// API for parity. Pure projections — no behaviour.

public sealed record DeviceListItemDto(
    Guid Id,
    string? SerialNumber,
    string? HardwareModel,
    string? Name,
    string? FirmwareVersion,
    string? AgentVersion,
    bool IsOnline,
    string Status,
    string? WanIpAddress,
    IReadOnlyDictionary<string, string> MacAddresses,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset ActivatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DeviceDetailDto(
    Guid Id,
    string? SerialNumber,
    string? HardwareModel,
    string? Name,
    string? FirmwareVersion,
    string? AgentVersion,
    bool IsOnline,
    string Status,
    string? WanIpAddress,
    IReadOnlyDictionary<string, string> MacAddresses,
    decimal? Latitude,
    decimal? Longitude,
    string? Notes,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset ActivatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid TenantId,
    IReadOnlyList<DeviceCertificateSummaryDto> Certificates,
    IReadOnlyList<DeviceEventDto> RecentEvents);

public sealed record DeviceCertificateSummaryDto(
    string Thumbprint,
    string SubjectName,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    bool IsActive);

public sealed record DeviceEventDto(
    long Id,
    DateTimeOffset Timestamp,
    string EventType,
    string? Severity,
    string PayloadJson);

public sealed record HardwareInventoryDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string SerialNumber,
    bool IsProvisioned,
    string Status,
    string IdentityPolicy,
    IReadOnlyDictionary<string, string?> IdentityClaims,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProductSelectDto(Guid Id, string CategoryName, string TypeName, string Name)
{
    public string DisplayLabel => $"{TypeName}/{CategoryName}/ {Name}";
}

public sealed record TelemetryRecordDto(
    long Id,
    Guid DeviceId,
    string SerialNumber,
    DateTimeOffset Timestamp,
    string Topic,
    byte QosLevel,
    string PayloadJson);

public sealed record TelemetryPagedResult(
    List<TelemetryRecordDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record HardwareVariantDto(
    Guid Id,
    string DeviceTypeCode,
    string Name,
    string PcbRevision,
    string ChipsetId);

public sealed record DeviceTelemetryDto(
    Guid Id,
    Guid DeviceId,
    DateTimeOffset Timestamp,
    double CpuUsagePercent,
    double RamUsageMb,
    double RamTotalMb,
    double StorageUsedMb,
    double StorageTotalMb,
    long UptimeSeconds,
    string? WanIp,
    double? TemperatureCelsius,
    double? SignalStrengthRssi,
    double? SignalQualityRsrp);

public sealed record DeviceTelemetryPageResult(
    List<DeviceTelemetryDto> Items,
    int Page,
    int PageSize);

public sealed record DashboardSummaryDto(
    int TotalDevices,
    int OnlineDevices,
    int TotalHardware,
    int UnprovisionedHardware,
    int PublishedPackages,
    int ActiveCampaigns,
    int RecentTelemetryCount,
    int OfflineDevices,
    int InactiveDevices);

public sealed record DashboardTrendPointDto(
    DateOnly Date,
    int NewDevices,
    double OnlineRatePercent);
