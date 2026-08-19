using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.Persistence;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ConfigureTaskDeviceConfiguration : IEntityTypeConfiguration<ConfigureTaskDevice>
{
    public void Configure(EntityTypeBuilder<ConfigureTaskDevice> builder)
    {
        builder.ToTable("configure_task_devices", "config");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Status).HasConversion<int>().IsRequired();
        builder.Property(d => d.FailureReason).HasMaxLength(2000);
        builder.Property(d => d.AttemptCount).HasDefaultValue(0);
        builder.Property(d => d.MaxAttempts).HasDefaultValue(3);
        builder.Property(d => d.ErrorCode).HasMaxLength(100);
        builder.Property(d => d.ErrorMessage).HasMaxLength(2000);

        builder.UseXminConcurrencyToken();

        builder.HasOne(d => d.ConfigureTask)
            .WithMany(t => t.Devices)
            .HasForeignKey(d => d.ConfigureTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Device)
            .WithMany()
            .HasForeignKey(d => d.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.ConfigureTaskId, d.DeviceId })
            .IsUnique()
            .HasDatabaseName("ix_configure_task_devices_task_device");

        builder.HasIndex(d => new { d.DeviceId, d.Status })
            .HasDatabaseName("ix_configure_task_devices_device_status");

        builder.HasIndex(d => new { d.ConfigureTaskId, d.Status })
            .HasFilter($"\"Status\" IN ({(int)DeviceConfigStatus.NotStarted}, {(int)DeviceConfigStatus.ConfigInProgress})")
            .HasDatabaseName("ix_configure_task_devices_task_active");

        // Integrity backstop: a device may belong to at most ONE *active* config task at a time.
        // Partial UNIQUE index on DeviceId filtered to the non-terminal device states
        // (NotStarted, ConfigInProgress). Mirrors ux_upgrade_task_devices_active_device.
        builder.HasIndex(d => d.DeviceId)
            .IsUnique()
            .HasFilter($"\"Status\" IN ({(int)DeviceConfigStatus.NotStarted}, {(int)DeviceConfigStatus.ConfigInProgress})")
            .HasDatabaseName("ux_configure_task_devices_active_device");
    }
}
