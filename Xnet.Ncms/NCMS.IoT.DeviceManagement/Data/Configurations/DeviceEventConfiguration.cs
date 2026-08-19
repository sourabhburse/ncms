using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class DeviceEventConfiguration : IEntityTypeConfiguration<DeviceEvent>
{
    public void Configure(EntityTypeBuilder<DeviceEvent> builder)
    {
        builder.ToTable("device_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityByDefaultColumn();
        builder.Property(e => e.DeviceId).IsRequired();
        builder.Property(e => e.Timestamp).IsRequired();
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Severity).HasMaxLength(16);
        builder.Property(e => e.PayloadJson).IsRequired().HasColumnType("jsonb");

        builder.HasIndex(e => new { e.DeviceId, e.Timestamp })
            .HasDatabaseName("ix_device_events_device_id_timestamp");
        builder.HasIndex(e => new { e.DeviceId, e.EventType })
            .HasDatabaseName("ix_device_events_device_id_event_type");
    }
}
