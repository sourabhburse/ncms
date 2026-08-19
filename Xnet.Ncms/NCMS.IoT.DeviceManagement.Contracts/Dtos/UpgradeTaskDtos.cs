using System.Text.Json.Serialization;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Contracts.Dtos;

public sealed record UpgradeTaskListItemDto(
    Guid Id,
    string TaskName,
    string UpgradedVersion,
    int SucceededDevices,
    int TotalDevices,
    UpgradeTaskStatus TaskStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndTime);

public sealed record UpgradeTaskProgressDto(
    int Total,
    int Pending,
    int InProgress,
    int Succeeded,
    int Failed,
    int TimedOut,
    int Skipped);

public sealed record UpgradeTaskDetailDto(
    Guid Id,
    string TaskName,
    string FirmwareName,
    string FirmwareVersion,
    int DeviceTotal,
    decimal UpgradeDurationHours,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    UpgradeTaskStatus TaskStatus,
    string? CreatedBy,
    UpgradeTaskProgressDto Progress,
    List<UpgradeTaskDeviceDto> Devices);

public sealed record UpgradeTaskDeviceDto(
    int Index,
    Guid DeviceId,
    string DeviceCode,
    string DeviceName,
    string ProductModel,
    string? OriginalVersion,
    string? CurrentVersion,
    string TargetVersion,
    EscalationState EscalationState,
    int AttemptCount,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorCode,
    string? ErrorMessage);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EscalationState
{
    WaitingToDownload,
    Downloading,
    UpgradeSucceeded,
    UpgradeFailure,
    UpgradeTimeout,
    Terminated,
    Aborted
}

public sealed record CreateUpgradeTaskRequest(
    Guid FirmwarePackageId,
    List<Guid> DeviceIds,
    string TaskName,
    decimal UpgradeDurationHours);

public sealed record FirmwareDeviceSearchResult(
    Guid DeviceId,
    string DeviceName,
    string ProductModel,
    string? CurrentFirmwareVersion,
    string DeviceStatus);

public sealed record FirmwareDeviceSearchPagedResult(
    List<FirmwareDeviceSearchResult> Items,
    int Total);
