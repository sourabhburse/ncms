using NCMS.Shared.Domain;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Per-device entry within a ConfigureTask. Mirrors <see cref="UpgradeTaskDevice"/>.
///
/// State machine: NotStarted → ConfigInProgress → ConfigComplete
///                                              → TerminationConfig (failure / timeout)
///                NotStarted → TerminationConfig (offline at dispatch, or task cancelled)
/// </summary>
public sealed class ConfigureTaskDevice : BaseEntity<Guid>
{
    private ConfigureTaskDevice() { }

    public Guid ConfigureTaskId { get; private set; }
    public Guid DeviceId { get; private set; }
    public DeviceConfigStatus Status { get; private set; } = DeviceConfigStatus.NotStarted;

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
    public ConfigureTask ConfigureTask { get; private set; } = default!;
    public Device Device { get; private set; } = default!;

    public static ConfigureTaskDevice Create(Guid configureTaskId, Guid deviceId, int maxAttempts = 3)
    {
        if (configureTaskId == Guid.Empty) throw new ArgumentException("ConfigureTaskId is required.", nameof(configureTaskId));
        if (deviceId == Guid.Empty) throw new ArgumentException("DeviceId is required.", nameof(deviceId));

        return new ConfigureTaskDevice
        {
            Id = Guid.NewGuid(),
            ConfigureTaskId = configureTaskId,
            DeviceId = deviceId,
            Status = DeviceConfigStatus.NotStarted,
            AttemptCount = 0,
            MaxAttempts = maxAttempts
        };
    }

    public void MarkDispatched()
    {
        if (Status is not (DeviceConfigStatus.NotStarted or DeviceConfigStatus.ConfigInProgress))
            throw new InvalidOperationException($"Cannot dispatch a config in state {Status}.");
        Status = DeviceConfigStatus.ConfigInProgress;
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
        if (Status != DeviceConfigStatus.ConfigInProgress)
            throw new InvalidOperationException($"Cannot record a failed attempt from state {Status}.");
        AttemptCount++;
    }

    public void MarkAcknowledged()
    {
        if (Status != DeviceConfigStatus.ConfigInProgress)
            throw new InvalidOperationException($"Cannot acknowledge from state {Status}.");
        AcknowledgedAt = DateTimeOffset.UtcNow;
    }

    public void MarkComplete()
    {
        if (Status != DeviceConfigStatus.ConfigInProgress)
            throw new InvalidOperationException($"Cannot complete from state {Status}.");
        Status = DeviceConfigStatus.ConfigComplete;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string reason, string? errorCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != DeviceConfigStatus.ConfigInProgress)
            throw new InvalidOperationException($"Cannot fail from state {Status}.");
        Status = DeviceConfigStatus.TerminationConfig;
        FailureReason = reason;
        ErrorMessage = reason;
        ErrorCode = errorCode;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkTimedOut(string reason)
    {
        if (Status != DeviceConfigStatus.ConfigInProgress)
            throw new InvalidOperationException($"Cannot time out from state {Status}.");
        Status = DeviceConfigStatus.TerminationConfig;
        FailureReason = reason ?? "Config timeout elapsed.";
        ErrorMessage = FailureReason;
        ErrorCode = "TIMEOUT";
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Terminate (abort) this device while it is still waiting to configure — i.e. not yet
    /// dispatched, or dispatched but not yet acknowledged (e.g. the device is offline).
    /// </summary>
    public void Skip(string reason)
    {
        var waitingToConfig =
            Status == DeviceConfigStatus.NotStarted ||
            (Status == DeviceConfigStatus.ConfigInProgress && AcknowledgedAt is null);

        if (!waitingToConfig)
            throw new InvalidOperationException(
                $"Cannot terminate a config that is {Status} and already configuring.");

        Status = DeviceConfigStatus.TerminationConfig;
        FailureReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
