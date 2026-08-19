using System.Text.Json.Serialization;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Contracts.Dtos;

public sealed record ConfigureTaskListItemDto(
    Guid Id,
    string TaskNumber,
    string ConfigContent,
    string ProductModel,
    int CompletedDevices,
    int TotalDevices,
    ConfigureTaskStatus TaskStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndTime);

public sealed record ConfigureTaskPagedResult(
    List<ConfigureTaskListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record ConfigureTaskProgressDto(
    int Total,
    int NotStarted,
    int InProgress,
    int Complete,
    int Terminated);

public sealed record ConfigureTaskDetailDto(
    Guid Id,
    string TaskNumber,
    string ProfileName,
    string ProductModel,
    int DeviceTotal,
    decimal ConfigDurationHours,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    ConfigureTaskStatus TaskStatus,
    string? CreatedBy,
    ConfigureTaskProgressDto Progress,
    List<ConfigureTaskDeviceDto> Devices);

public sealed record ConfigureTaskDeviceDto(
    int Index,
    Guid DeviceId,
    string DeviceName,
    string ProductModel,
    ConfigDeviceState ConfigState,
    int AttemptCount,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigDeviceState
{
    WaitingToConfig,
    Configuring,
    ConfigSucceeded,
    Terminated
}

public sealed record CreateConfigureTaskRequest(
    Guid ProfileId,
    List<Guid> DeviceIds,
    decimal ConfigDurationHours);

public sealed record ConfigDeviceSearchResult(
    Guid DeviceId,
    string DeviceName,
    string ProductModel,
    string? CurrentFirmwareVersion,
    string DeviceStatus);

public sealed record ConfigDeviceSearchPagedResult(
    List<ConfigDeviceSearchResult> Items,
    int Total);
