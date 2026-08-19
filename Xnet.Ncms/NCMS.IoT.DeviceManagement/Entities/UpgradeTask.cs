using NCMS.Shared.Domain;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// An orchestration record representing "upgrade these N devices to this firmware".
/// Simpler and more targeted than FirmwareCampaign (which handles fleet-wide rollouts
/// with rollout strategies, staged percentages, and approval workflows).
/// UpgradeTask is the operator-facing, direct-targeting variant.
///
/// Creation invariants (enforced at the application layer):
///   - Firmware must be Published
///   - All targeted devices must have a FirmwareProduct record linking their Product to this Firmware
///   - All targeted devices must have Product.SupportsFirmwareUpgrade = true
///   - At least one device required
///   - No duplicate devices
///
/// State machine:  NotStarted → InProgress → Completed
///                 NotStarted → Cancelled
///                 InProgress → Cancelled (partial; in-flight devices reach timeout)
/// </summary>
public sealed class UpgradeTask : BaseEntity<Guid>
{
    private UpgradeTask() { }

    public Guid FirmwareId { get; private set; }
    public string Name { get; private set; } = default!;
    public string CreatedBy { get; private set; } = "system";
    public UpgradeTaskStatus Status { get; private set; } = UpgradeTaskStatus.NotStarted;

    /// <summary>
    /// Maximum time allowed for an individual device upgrade before the
    /// per-device UpgradeTaskDevice record is marked TimedOut.
    /// </summary>
    public TimeSpan? UpgradeTimeout { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    // Navigation
    public Firmware Firmware { get; private set; } = default!;
    public ICollection<UpgradeTaskDevice> Devices { get; } = new List<UpgradeTaskDevice>();

    public static UpgradeTask Create(Guid firmwareId, string name, string? createdBy = null, TimeSpan? upgradeTimeout = null)
    {
        if (firmwareId == Guid.Empty) throw new ArgumentException("FirmwareId is required.", nameof(firmwareId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 256) throw new ArgumentException("Name must be 256 characters or fewer.", nameof(name));

        return new UpgradeTask
        {
            Id = Guid.NewGuid(),
            FirmwareId = firmwareId,
            Name = name.Trim(),
            CreatedBy = createdBy?.Trim() ?? "system",
            UpgradeTimeout = upgradeTimeout,
            Status = UpgradeTaskStatus.NotStarted,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 256) throw new ArgumentException("Name must be 256 characters or fewer.", nameof(name));
        Name = name.Trim();
    }

    /// <summary>
    /// Recomputes <see cref="Status"/> as a projection of the child device statuses.
    /// This is the single write path for the derived lifecycle states
    /// (<see cref="UpgradeTaskStatus.NotStarted"/> → <see cref="UpgradeTaskStatus.InProgress"/>
    /// → <see cref="UpgradeTaskStatus.Completed"/>). It must be called within the same
    /// transaction as any child <see cref="UpgradeTaskDevice"/> status transition.
    ///
    /// <para><see cref="UpgradeTaskStatus.Cancelled"/> is a sticky operator-intent terminal
    /// state and is never overwritten here.</para>
    ///
    /// <param name="devices">The complete set of this task's device rows. The caller is
    /// responsible for loading them.</param>
    /// </summary>
    public void Reconcile(IReadOnlyCollection<UpgradeTaskDevice> devices)
    {
        // Operator intent wins and is terminal — never recompute over a cancellation.
        if (Status == UpgradeTaskStatus.Cancelled) return;
        if (devices.Count == 0) return; // nothing to derive from; leave status untouched

        // ── Extension point (large-scale) ──────────────────────────────────────────
        // Today the status distribution is derived by scanning the child rows. When
        // per-status aggregate counters are introduced on UpgradeTask, replace these two
        // lines with counter reads — the decision and timestamp logic below is unchanged.
        var hasActiveDevice = devices.Any(d =>
            d.Status is UpgradeDeviceStatus.Pending or UpgradeDeviceStatus.InProgress);
        var allUndispatched = devices.All(d => d.Status == UpgradeDeviceStatus.Pending);
        // ───────────────────────────────────────────────────────────────────────────

        var next = !hasActiveDevice
            ? UpgradeTaskStatus.Completed           // every device reached a terminal state
            : allUndispatched
                ? UpgradeTaskStatus.NotStarted      // nothing dispatched yet
                : UpgradeTaskStatus.InProgress;     // at least one device in flight

        ApplyStatus(next);
    }

    private void ApplyStatus(UpgradeTaskStatus next)
    {
        if (Status == next) return;
        Status = next;
        if (next == UpgradeTaskStatus.InProgress) StartedAt ??= DateTimeOffset.UtcNow;
        else if (next == UpgradeTaskStatus.Completed) CompletedAt ??= DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status is UpgradeTaskStatus.Completed or UpgradeTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel a task that is already {Status}.");
        Status = UpgradeTaskStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
