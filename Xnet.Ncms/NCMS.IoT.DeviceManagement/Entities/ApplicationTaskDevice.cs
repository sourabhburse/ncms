using NCMS.Shared.Domain;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Per-device entry within an ApplicationTask. Mirrors <see cref="UpgradeTaskDevice"/> exactly —
/// a task always targets exactly one package version (no bundle fan-out), so unlike the earlier
/// bundle-era design this carries the target package directly instead of an ordered item
/// collection.
///
/// State machine: Pending → InProgress → Succeeded
///                                     → Failed     (install failed after exhausting retries)
///                                     → TimedOut
///                Pending → Skipped    (device offline at dispatch, or task cancelled)
/// </summary>
public sealed class ApplicationTaskDevice : BaseEntity<Guid>
{
    private ApplicationTaskDevice() { }

    public Guid ApplicationTaskId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid ApplicationPackageVersionId { get; private set; }
    public ApplicationTaskDeviceStatus Status { get; private set; } = ApplicationTaskDeviceStatus.Pending;

    public string? FailureReason { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    // Dispatch tracking
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; } = 3;
    public DateTimeOffset? DispatchedAt { get; private set; }
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    // Navigation
    public ApplicationTask ApplicationTask { get; private set; } = default!;
    public Device Device { get; private set; } = default!;
    public ApplicationPackageVersion ApplicationPackageVersion { get; private set; } = default!;

    public static ApplicationTaskDevice Create(Guid applicationTaskId, Guid deviceId, Guid applicationPackageVersionId, int maxAttempts = 3)
    {
        if (applicationTaskId == Guid.Empty) throw new ArgumentException("ApplicationTaskId is required.", nameof(applicationTaskId));
        if (deviceId == Guid.Empty) throw new ArgumentException("DeviceId is required.", nameof(deviceId));
        if (applicationPackageVersionId == Guid.Empty) throw new ArgumentException("ApplicationPackageVersionId is required.", nameof(applicationPackageVersionId));

        return new ApplicationTaskDevice
        {
            Id = Guid.NewGuid(),
            ApplicationTaskId = applicationTaskId,
            DeviceId = deviceId,
            ApplicationPackageVersionId = applicationPackageVersionId,
            Status = ApplicationTaskDeviceStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = maxAttempts
        };
    }

    public void MarkDispatched()
    {
        if (Status is not (ApplicationTaskDeviceStatus.Pending or ApplicationTaskDeviceStatus.InProgress))
            throw new InvalidOperationException($"Cannot dispatch an application task device in state {Status}.");
        Status = ApplicationTaskDeviceStatus.InProgress;
        StartedAt ??= DateTimeOffset.UtcNow;
        DispatchedAt = DateTimeOffset.UtcNow;
        AttemptCount++;
    }

    /// <summary>
    /// Counts a dispatch attempt that failed before it could even reach the device (e.g. the
    /// MQTT publish itself threw). Without this, a transient publish failure during a retry
    /// would either be silently ignored forever (AttemptCount never reaches MaxAttempts) or
    /// force an immediate terminal MarkTimedOut on the very first hiccup — this lets the
    /// existing "AttemptCount &lt; MaxAttempts" retry check work as intended.
    /// </summary>
    public void RecordFailedAttempt()
    {
        if (Status != ApplicationTaskDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot record a failed attempt from state {Status}.");
        AttemptCount++;
    }

    public void MarkAcknowledged()
    {
        if (Status != ApplicationTaskDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot acknowledge from state {Status}.");
        AcknowledgedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSucceeded()
    {
        if (Status != ApplicationTaskDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot succeed from state {Status}.");
        Status = ApplicationTaskDeviceStatus.Succeeded;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != ApplicationTaskDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot fail from state {Status}.");
        Status = ApplicationTaskDeviceStatus.Failed;
        FailureReason = reason;
        ErrorMessage = reason;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkTimedOut(string reason)
    {
        if (Status != ApplicationTaskDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot time out from state {Status}.");
        Status = ApplicationTaskDeviceStatus.TimedOut;
        FailureReason = reason ?? "Deployment timeout elapsed.";
        ErrorMessage = FailureReason;
        ErrorCode = "TIMEOUT";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Skip (terminate) this device. Allowed while it is still waiting to start — i.e. not
    /// yet dispatched (Pending), or dispatched but not yet acknowledged by the device (e.g.
    /// the device is offline). Once acknowledged and actively running, it can no longer be
    /// skipped (let it finish, fail, or time out).
    /// </summary>
    public void Skip(string reason)
    {
        var waitingToStart =
            Status == ApplicationTaskDeviceStatus.Pending ||
            (Status == ApplicationTaskDeviceStatus.InProgress && AcknowledgedAt is null);

        if (!waitingToStart)
            throw new InvalidOperationException(
                $"Cannot skip an application task device that is {Status} and already in progress.");

        Status = ApplicationTaskDeviceStatus.Skipped;
        FailureReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
