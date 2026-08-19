using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ApplicationTaskDeviceConfiguration : IEntityTypeConfiguration<ApplicationTaskDevice>
{
    public void Configure(EntityTypeBuilder<ApplicationTaskDevice> builder)
    {
        builder.ToTable("application_task_devices", "application");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Status).HasConversion<int>().IsRequired();
        builder.Property(d => d.FailureReason).HasMaxLength(2000);

        // Dispatch tracking
        builder.Property(d => d.AttemptCount).HasDefaultValue(0);
        builder.Property(d => d.MaxAttempts).HasDefaultValue(3);
        builder.Property(d => d.ErrorCode).HasMaxLength(100);
        builder.Property(d => d.ErrorMessage).HasMaxLength(2000);

        builder.UseXminConcurrencyToken();

        builder.HasOne(d => d.ApplicationTask)
            .WithMany(t => t.Devices)
            .HasForeignKey(d => d.ApplicationTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Device FK: device is in the same bounded context, so we enforce the FK.
        builder.HasOne(d => d.Device)
            .WithMany()
            .HasForeignKey(d => d.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Target package FK — folded in from the old per-item table now that a task always
        // targets exactly one package (no more bundle fan-out).
        builder.HasOne(d => d.ApplicationPackageVersion)
            .WithMany()
            .HasForeignKey(d => d.ApplicationPackageVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Uniqueness: a device may appear only once per application task.
        builder.HasIndex(d => new { d.ApplicationTaskId, d.DeviceId })
            .IsUnique()
            .HasDatabaseName("ix_application_task_devices_task_device");

        // Support: find all in-progress/pending entries for a specific device.
        builder.HasIndex(d => new { d.DeviceId, d.Status })
            .HasDatabaseName("ix_application_task_devices_device_status");

        // Support: find all pending entries for a task (dispatcher query).
        builder.HasIndex(d => new { d.ApplicationTaskId, d.Status })
            .HasFilter($"\"Status\" IN ({(int)ApplicationTaskDeviceStatus.Pending}, {(int)ApplicationTaskDeviceStatus.InProgress})")
            .HasDatabaseName("ix_application_task_devices_task_active");

        // Integrity backstop: a device may belong to at most ONE *active* application task at a
        // time. Partial UNIQUE index on DeviceId filtered to the non-terminal device states
        // (Pending, InProgress). Mirrors ux_upgrade_task_devices_active_device /
        // ux_configure_task_devices_active_device. Note: this only guards within the application
        // domain — the cross-domain "device busy" guard against firmware/config tasks is a
        // dispatcher-level check (IsDeviceBusyElsewhere), not a DB constraint, since the three
        // domains use independent tables.
        builder.HasIndex(d => d.DeviceId)
            .IsUnique()
            .HasFilter($"\"Status\" IN ({(int)ApplicationTaskDeviceStatus.Pending}, {(int)ApplicationTaskDeviceStatus.InProgress})")
            .HasDatabaseName("ux_application_task_devices_active_device");
    }
}
