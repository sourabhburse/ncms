using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class FirmwareConfiguration : IEntityTypeConfiguration<Firmware>
{
    public void Configure(EntityTypeBuilder<Firmware> builder)
    {
        builder.ToTable("FirmwarePackages", "firmware");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Version).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Type).HasConversion<int>().IsRequired();
        builder.Property(p => p.FileName).HasMaxLength(500);
        builder.Property(p => p.StoragePath).HasMaxLength(500);
        builder.Property(p => p.Md5).HasColumnName("Md5Checksum").HasMaxLength(64);
        builder.Property(p => p.Size).HasColumnName("SizeBytes");
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.ReleaseNotes);
        builder.Property(p => p.DeviceTypeCode).HasMaxLength(100);
        builder.Property(p => p.BinaryChecksum).HasMaxLength(200);
        builder.Property(p => p.DigitalSignature).HasMaxLength(1000);
        builder.Property(p => p.MinRequiredFirmwareVersion).HasMaxLength(50);
        builder.Property(p => p.CreatedBy).HasMaxLength(256);
        builder.Property(p => p.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(p => new { p.Name, p.Version, p.Type })
            .IsUnique()
            .HasDatabaseName("ix_firmware_packages_name_version_type");

        builder.HasIndex(p => new { p.DeviceTypeCode, p.Version })
            .HasDatabaseName("ix_firmware_packages_devicetypecode_version");
    }
}
