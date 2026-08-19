using NCMS.Shared.Domain;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// An orchestration record representing "apply this config profile to these N devices".
/// Mirrors <see cref="UpgradeTask"/>.
///
/// State machine:  NotStarted → InProgress → Completed
///                 NotStarted → Cancelled
///                 InProgress → Cancelled (partial; in-flight devices reach timeout)
/// </summary>
public sealed class ConfigureTask : BaseEntity<Guid>
{
    private ConfigureTask() { }

    /// <summary>Human-facing task number shown in the list (timestamp-derived).</summary>
    public string TaskNumber { get; private set; } = default!;

    public Guid ProfileId { get; private set; }

    /// <summary>Snapshot of the profile name at task creation time.</summary>
    public string ProfileName { get; private set; } = default!;

    public string CreatedBy { get; private set; } = "system";
    public ConfigureTaskStatus Status { get; private set; } = ConfigureTaskStatus.NotStarted;

    /// <summary>
    /// Maximum time allowed for an individual device configuration before the
    /// per-device record is marked TerminationConfig.
    /// </summary>
    public TimeSpan? ConfigTimeout { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    // Navigation
    public ConfigProfile Profile { get; private set; } = default!;
    public ICollection<ConfigureTaskDevice> Devices { get; } = new List<ConfigureTaskDevice>();

    public static ConfigureTask Create(
        ConfigProfile profile,
        string? createdBy = null,
        TimeSpan? configTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ConfigureTask
        {
            Id = Guid.NewGuid(),
            TaskNumber = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString() + Random.Shared.Next(100, 999),
            ProfileId = profile.Id,
            ProfileName = profile.ProfileName,
            CreatedBy = createdBy?.Trim() ?? "system",
            ConfigTimeout = configTimeout,
            Status = ConfigureTaskStatus.NotStarted,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Recomputes <see cref="Status"/> as a projection of the child device statuses.
    /// Single write path for the derived lifecycle states (NotStarted → InProgress →
    /// Completed). Must be called within the same transaction as any child
    /// <see cref="ConfigureTaskDevice"/> status transition. Mirrors
    /// <see cref="UpgradeTask.Reconcile"/>.
    ///
    /// <para><see cref="ConfigureTaskStatus.Cancelled"/> is a sticky operator-intent
    /// terminal state and is never overwritten here.</para>
    ///
    /// <param name="devices">The complete set of this task's device rows.</param>
    /// </summary>
    public void Reconcile(IReadOnlyCollection<ConfigureTaskDevice> devices)
    {
        if (Status == ConfigureTaskStatus.Cancelled) return;
        if (devices.Count == 0) return;

        // ── Extension point (large-scale) ──────────────────────────────────────────
        // Derived by scanning child rows today; swap for aggregate counter reads later.
        var hasActiveDevice = devices.Any(d =>
            d.Status is DeviceConfigStatus.NotStarted or DeviceConfigStatus.ConfigInProgress);
        var allUndispatched = devices.All(d => d.Status == DeviceConfigStatus.NotStarted);
        // ───────────────────────────────────────────────────────────────────────────

        var next = !hasActiveDevice
            ? ConfigureTaskStatus.Completed
            : allUndispatched
                ? ConfigureTaskStatus.NotStarted
                : ConfigureTaskStatus.InProgress;

        ApplyStatus(next);
    }

    private void ApplyStatus(ConfigureTaskStatus next)
    {
        if (Status == next) return;
        Status = next;
        if (next == ConfigureTaskStatus.InProgress) StartedAt ??= DateTimeOffset.UtcNow;
        else if (next == ConfigureTaskStatus.Completed) CompletedAt ??= DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status is ConfigureTaskStatus.Completed or ConfigureTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel a task that is already {Status}.");
        Status = ConfigureTaskStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
