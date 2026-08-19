using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class UpgradeTaskDeviceConfiguration : IEntityTypeConfiguration<UpgradeTaskDevice>
{
    public void Configure(EntityTypeBuilder<UpgradeTaskDevice> builder)
    {
        builder.ToTable("upgrade_task_devices", "firmware");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Status).HasConversion<int>().IsRequired();
        builder.Property(d => d.PreviousFirmwareVersion).HasMaxLength(100);
        builder.Property(d => d.TargetFirmwareVersion).IsRequired().HasMaxLength(100);
        builder.Property(d => d.FailureReason).HasMaxLength(2000);

        // Dispatch tracking
        builder.Property(d => d.AttemptCount).HasDefaultValue(0);
        builder.Property(d => d.MaxAttempts).HasDefaultValue(3);
        builder.Property(d => d.ErrorCode).HasMaxLength(100);
        builder.Property(d => d.ErrorMessage).HasMaxLength(2000);

        builder.UseXminConcurrencyToken();

        builder.HasOne(d => d.UpgradeTask)
            .WithMany(t => t.Devices)
            .HasForeignKey(d => d.UpgradeTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Device FK: device is in the same bounded context, so we enforce the FK.
        // If Device moves to a separate service, change to no navigation + no FK constraint.
        builder.HasOne(d => d.Device)
            .WithMany()
            .HasForeignKey(d => d.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Uniqueness: a device may appear only once per upgrade task.
        builder.HasIndex(d => new { d.UpgradeTaskId, d.DeviceId })
            .IsUnique()
            .HasDatabaseName("ix_upgrade_task_devices_task_device");

        // Support: find all in-progress/pending entries for a specific device.
        builder.HasIndex(d => new { d.DeviceId, d.Status })
            .HasDatabaseName("ix_upgrade_task_devices_device_status");

        // Support: find all pending entries for a task (dispatcher query).
        builder.HasIndex(d => new { d.UpgradeTaskId, d.Status })
            .HasFilter($"\"Status\" IN ({(int)UpgradeDeviceStatus.Pending}, {(int)UpgradeDeviceStatus.InProgress})")
            .HasDatabaseName("ix_upgrade_task_devices_task_active");

        // Integrity backstop: a device may belong to at most ONE *active* upgrade task at a
        // time. Partial UNIQUE index on DeviceId filtered to the non-terminal device states
        // (Pending, InProgress). Postgres enforces this atomically, so two concurrent task
        // creations cannot both assign the same device — the second insert fails with 23505.
        // UpgradeTaskDevice.Status is the authoritative "is this device busy" signal; terminal
        // task transitions cascade to device rows (via Reconcile / abort), so a device frees its
        // slot only when its own row reaches a terminal state.
        builder.HasIndex(d => d.DeviceId)
            .IsUnique()
            .HasFilter($"\"Status\" IN ({(int)UpgradeDeviceStatus.Pending}, {(int)UpgradeDeviceStatus.InProgress})")
            .HasDatabaseName("ux_upgrade_task_devices_active_device");
    }
}
