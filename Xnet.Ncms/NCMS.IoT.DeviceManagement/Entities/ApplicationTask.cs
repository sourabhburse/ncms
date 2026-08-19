using NCMS.Shared.Domain;
using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// An orchestration record representing "install/upgrade/downgrade/remove this package on
/// these N devices". Third instance of the UpgradeTask/ConfigureTask pattern — mirrors
/// UpgradeTask field-for-field. <see cref="Action"/> distinguishes intent (firmware and
/// config tasks never needed this since their action never varies).
///
/// Creation invariants (enforced at the application layer):
///   - Target version must be Published or Deprecated (not Draft/Disabled)
///   - All targeted devices' Product.SupportsSoftwarePackages = true
///   - All targeted devices have an ApplicationPackageProductCompatibility record for the target
///   - No device already has an active ApplicationTaskDevice, UpgradeTaskDevice, or
///     ConfigureTaskDevice (cross-domain "device busy" guard)
///   - At least one device required; no duplicate devices
///
/// State machine:  NotStarted → InProgress → Completed
///                 NotStarted → Cancelled
///                 InProgress → Cancelled (partial; in-flight devices reach timeout)
/// </summary>
public sealed class ApplicationTask : BaseEntity<Guid>
{
    private ApplicationTask() { }

    public string Name { get; private set; } = default!;
    public ApplicationTaskAction Action { get; private set; }
    public Guid TargetApplicationPackageVersionId { get; private set; }
    public string CreatedBy { get; private set; } = "system";
    public ApplicationTaskStatus Status { get; private set; } = ApplicationTaskStatus.NotStarted;

    /// <summary>
    /// Maximum time allowed for an individual device's install before its
    /// ApplicationTaskDevice record is marked TimedOut.
    /// </summary>
    public TimeSpan? Timeout { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    // Navigation
    public ApplicationPackageVersion TargetApplicationPackageVersion { get; private set; } = default!;
    public ICollection<ApplicationTaskDevice> Devices { get; } = new List<ApplicationTaskDevice>();

    public static ApplicationTask Create(
        string name,
        ApplicationTaskAction action,
        Guid targetApplicationPackageVersionId,
        string? createdBy = null,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 256) throw new ArgumentException("Name must be 256 characters or fewer.", nameof(name));
        if (targetApplicationPackageVersionId == Guid.Empty)
            throw new ArgumentException("A target application package version must be specified.", nameof(targetApplicationPackageVersionId));

        return new ApplicationTask
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Action = action,
            TargetApplicationPackageVersionId = targetApplicationPackageVersionId,
            CreatedBy = createdBy?.Trim() ?? "system",
            Timeout = timeout,
            Status = ApplicationTaskStatus.NotStarted,
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
    /// Mirrors <see cref="UpgradeTask.Reconcile"/>. Must be called within the same
    /// transaction as any child <see cref="ApplicationTaskDevice"/> status transition.
    ///
    /// <para><see cref="ApplicationTaskStatus.Cancelled"/> is a sticky operator-intent terminal
    /// state and is never overwritten here.</para>
    /// </summary>
    public void Reconcile(IReadOnlyCollection<ApplicationTaskDevice> devices)
    {
        if (Status == ApplicationTaskStatus.Cancelled) return;
        if (devices.Count == 0) return;

        var hasActiveDevice = devices.Any(d =>
            d.Status is ApplicationTaskDeviceStatus.Pending or ApplicationTaskDeviceStatus.InProgress);
        var allUndispatched = devices.All(d => d.Status == ApplicationTaskDeviceStatus.Pending);

        var next = !hasActiveDevice
            ? ApplicationTaskStatus.Completed
            : allUndispatched
                ? ApplicationTaskStatus.NotStarted
                : ApplicationTaskStatus.InProgress;

        ApplyStatus(next);
    }

    private void ApplyStatus(ApplicationTaskStatus next)
    {
        if (Status == next) return;
        Status = next;
        if (next == ApplicationTaskStatus.InProgress) StartedAt ??= DateTimeOffset.UtcNow;
        else if (next == ApplicationTaskStatus.Completed) CompletedAt ??= DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status is ApplicationTaskStatus.Completed or ApplicationTaskStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel a task that is already {Status}.");
        Status = ApplicationTaskStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
