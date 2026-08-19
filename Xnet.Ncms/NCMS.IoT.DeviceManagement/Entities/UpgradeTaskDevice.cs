using NCMS.Shared.Domain;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Per-device entry within an UpgradeTask.
/// Each device upgrades independently; this record tracks individual progress,
/// captures version snapshots, and records failure evidence.
///
/// State machine: Pending → InProgress → Succeeded
///                                     → Failed     (ErrorMessage required)
///                                     → TimedOut   (ErrorMessage required)
///                Pending → Skipped    (device offline at dispatch, or task cancelled)
/// </summary>
public sealed class UpgradeTaskDevice : BaseEntity<Guid>
{
    private UpgradeTaskDevice() { }

    public Guid UpgradeTaskId { get; private set; }
    public Guid DeviceId { get; private set; }
    public UpgradeDeviceStatus Status { get; private set; } = UpgradeDeviceStatus.Pending;

    public string PreviousFirmwareVersion { get; private set; } = default!;
    public string TargetFirmwareVersion { get; private set; } = default!;

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
    public UpgradeTask UpgradeTask { get; private set; } = default!;
    public Device Device { get; private set; } = default!;

    public static UpgradeTaskDevice Create(
        Guid upgradeTaskId,
        Guid deviceId,
        string previousFirmwareVersion,
        string targetFirmwareVersion,
        int maxAttempts = 3)
    {
        if (upgradeTaskId == Guid.Empty) throw new ArgumentException("UpgradeTaskId is required.", nameof(upgradeTaskId));
        if (deviceId == Guid.Empty) throw new ArgumentException("DeviceId is required.", nameof(deviceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFirmwareVersion);

        return new UpgradeTaskDevice
        {
            Id = Guid.NewGuid(),
            UpgradeTaskId = upgradeTaskId,
            DeviceId = deviceId,
            PreviousFirmwareVersion = previousFirmwareVersion ?? string.Empty,
            TargetFirmwareVersion = targetFirmwareVersion.Trim(),
            Status = UpgradeDeviceStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = maxAttempts
        };
    }

    public void MarkDispatched()
    {
        if (Status is not (UpgradeDeviceStatus.Pending or UpgradeDeviceStatus.InProgress))
            throw new InvalidOperationException($"Cannot dispatch a device upgrade in state {Status}.");
        Status = UpgradeDeviceStatus.InProgress;
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
        if (Status != UpgradeDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot record a failed attempt from state {Status}.");
        AttemptCount++;
    }

    public void MarkAcknowledged()
    {
        if (Status != UpgradeDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot acknowledge from state {Status}.");
        AcknowledgedAt = DateTimeOffset.UtcNow;
    }

    public void MarkInProgress()
    {
        if (Status != UpgradeDeviceStatus.Pending)
            throw new InvalidOperationException($"Cannot start a device upgrade that is already {Status}.");
        Status = UpgradeDeviceStatus.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void MarkSucceeded()
    {
        if (Status != UpgradeDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot succeed from state {Status}.");
        Status = UpgradeDeviceStatus.Succeeded;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string reason, string? errorCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != UpgradeDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot fail from state {Status}.");
        Status = UpgradeDeviceStatus.Failed;
        FailureReason = reason;
        ErrorMessage = reason;
        ErrorCode = errorCode;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkTimedOut(string reason)
    {
        if (Status != UpgradeDeviceStatus.InProgress)
            throw new InvalidOperationException($"Cannot time out from state {Status}.");
        Status = UpgradeDeviceStatus.TimedOut;
        FailureReason = reason ?? "Upgrade timeout elapsed.";
        ErrorMessage = FailureReason;
        ErrorCode = "TIMEOUT";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Skip (terminate) this device. Allowed while it is still waiting to download —
    /// i.e. not yet dispatched (Pending), or dispatched but not yet acknowledged by the
    /// device (e.g. the device is offline). Once a device has acknowledged and is actively
    /// downloading, it can no longer be skipped (let it finish, fail, or time out).
    /// </summary>
    public void Skip(string reason)
    {
        var waitingToDownload =
            Status == UpgradeDeviceStatus.Pending ||
            (Status == UpgradeDeviceStatus.InProgress && AcknowledgedAt is null);

        if (!waitingToDownload)
            throw new InvalidOperationException(
                $"Cannot skip a device upgrade that is {Status} and already downloading.");

        Status = UpgradeDeviceStatus.Skipped;
        FailureReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
