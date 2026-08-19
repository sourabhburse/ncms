using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class TelemetryRecordConfiguration : IEntityTypeConfiguration<TelemetryRecord>
{
    public void Configure(EntityTypeBuilder<TelemetryRecord> builder)
    {
        builder.ToTable("telemetry_records");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).UseIdentityByDefaultColumn();
        builder.Property(t => t.DeviceId).IsRequired();
        builder.Property(t => t.Timestamp).IsRequired();
        builder.Property(t => t.PayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(t => t.Topic).IsRequired().HasMaxLength(256);
        builder.Property(t => t.QosLevel).IsRequired();

        builder.HasIndex(t => new { t.DeviceId, t.Timestamp })
            .HasDatabaseName("ix_telemetry_records_device_id_timestamp");
    }
}
