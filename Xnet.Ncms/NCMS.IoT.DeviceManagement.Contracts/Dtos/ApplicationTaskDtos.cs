using System.Text.Json.Serialization;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Contracts.Dtos;

public sealed record ApplicationTaskListItemDto(
    Guid Id,
    string TaskName,
    ApplicationTaskAction Action,
    string TargetName,
    int SucceededDevices,
    int TotalDevices,
    ApplicationTaskStatus TaskStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EndTime);

public sealed record ApplicationTaskPagedResult(
    List<ApplicationTaskListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record ApplicationTaskProgressDto(
    int Total,
    int Pending,
    int InProgress,
    int Succeeded,
    int Failed,
    int TimedOut,
    int Skipped);

public sealed record ApplicationTaskDetailDto(
    Guid Id,
    string TaskName,
    ApplicationTaskAction Action,
    string TargetName,
    int DeviceTotal,
    decimal TimeoutHours,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    ApplicationTaskStatus TaskStatus,
    string? CreatedBy,
    ApplicationTaskProgressDto Progress,
    List<ApplicationTaskDeviceDto> Devices);

public sealed record ApplicationTaskDeviceDto(
    int Index,
    Guid DeviceId,
    string DeviceCode,
    string ProductModel,
    ApplicationEscalationState EscalationState,
    int AttemptCount,
    DateTimeOffset? DispatchedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorCode,
    string? ErrorMessage,
    Guid ApplicationPackageVersionId,
    string PackageName,
    string Version);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ApplicationEscalationState
{
    WaitingToStart,
    Downloading,
    Succeeded,
    Failed,
    TimedOut,
    Terminated
}

public sealed record CreateApplicationTaskRequest(
    ApplicationTaskAction Action,
    Guid ApplicationPackageVersionId,
    List<Guid> DeviceIds,
    string TaskName,
    decimal TimeoutHours);
