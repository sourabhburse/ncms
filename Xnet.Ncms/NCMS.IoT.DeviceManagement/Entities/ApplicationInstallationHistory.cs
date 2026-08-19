using NCMS.IoT.DeviceManagement.Contracts.Enums;

namespace NCMS.IoT.DeviceManagement.Entities;

/// <summary>
/// Append-only audit ledger — the single history table for every software event, whether
/// task-driven or drift-detected. Task-driven rows carry <see cref="ApplicationTaskDeviceId"/>;
/// drift rows (an agent reporting software that never went through a task — manual shell
/// access, factory preload) leave it null and set Source = DriftDetected. Never mutated.
/// </summary>
public sealed class ApplicationInstallationHistory
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid ApplicationPackageVersionId { get; set; }
    public ApplicationTaskAction Action { get; set; }
    public ApplicationInstallationResult Result { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ApplicationTaskDeviceId { get; set; }
    public ApplicationInstallationSource Source { get; set; } = ApplicationInstallationSource.Deployment;

    // Navigation
    public Device Device { get; set; } = default!;
    public ApplicationPackageVersion ApplicationPackageVersion { get; set; } = default!;
    public ApplicationTaskDevice? ApplicationTaskDevice { get; set; }
}
