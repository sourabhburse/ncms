using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class DeviceTelemetryConfiguration : IEntityTypeConfiguration<DeviceTelemetry>
{
    public void Configure(EntityTypeBuilder<DeviceTelemetry> builder)
    {
        builder.ToTable("device_telemetry");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.DeviceId).IsRequired();
        builder.Property(t => t.Timestamp).IsRequired();
        builder.Property(t => t.CpuUsagePercent).IsRequired();
        builder.Property(t => t.RamUsageMb).IsRequired();
        builder.Property(t => t.RamTotalMb).IsRequired();
        builder.Property(t => t.StorageUsedMb).IsRequired();
        builder.Property(t => t.StorageTotalMb).IsRequired();
        builder.Property(t => t.UptimeSeconds).IsRequired();
        builder.Property(t => t.WanIp).HasMaxLength(64);

        // Composite index for efficient history queries (device + time)
        builder.HasIndex(t => new { t.DeviceId, t.Timestamp })
            .HasDatabaseName("ix_device_telemetry_device_timestamp")
            .IsDescending(false, true);
    }
}
