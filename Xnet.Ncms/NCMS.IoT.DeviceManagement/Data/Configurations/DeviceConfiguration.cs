using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.HardwareInventoryId).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200);
        builder.Property(d => d.ActivatedAt).IsRequired();
        builder.Property(d => d.FirmwareVersion).HasMaxLength(64);
        builder.Property(d => d.AgentVersion).HasMaxLength(64);
        builder.Property(d => d.HardwareModel).HasMaxLength(64);
        builder.Property(d => d.IsOnline).HasDefaultValue(false);

        // Reference backend fields
        builder.Property(d => d.Status).IsRequired().HasMaxLength(50).HasDefaultValue("PROVISIONING");
        builder.Property(d => d.WanIpAddress).HasMaxLength(64);
        builder.Property(d => d.MacAddresses)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();
        builder.Property(d => d.Latitude).HasPrecision(9, 6);
        builder.Property(d => d.Longitude).HasPrecision(9, 6);
        builder.Property(d => d.Notes).HasMaxLength(2000);
        builder.Property(d => d.CreatedAt).IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(d => d.UpdatedAt).IsRequired().HasDefaultValueSql("NOW()");

        builder.HasMany(d => d.Certificates)
            .WithOne(c => c.Device)
            .HasForeignKey(c => c.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.TelemetryRecords)
            .WithOne(t => t.Device)
            .HasForeignKey(t => t.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.DeviceTelemetries)
            .WithOne(t => t.Device)
            .HasForeignKey(t => t.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Events)
            .WithOne(e => e.Device)
            .HasForeignKey(e => e.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
