using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class DeviceCertificateConfiguration : IEntityTypeConfiguration<DeviceCertificate>
{
    public void Configure(EntityTypeBuilder<DeviceCertificate> builder)
    {
        builder.ToTable("device_certificates");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Thumbprint).IsRequired().HasMaxLength(40);
        builder.HasIndex(c => c.Thumbprint).IsUnique().HasDatabaseName("ix_device_certificates_thumbprint");

        builder.Property(c => c.CertificatePem).IsRequired().HasColumnType("text");
        builder.Property(c => c.SubjectName).IsRequired().HasMaxLength(512);
        builder.Property(c => c.IssuedAt).IsRequired();
        builder.Property(c => c.ExpiresAt).IsRequired();
        builder.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).IsRequired().HasDefaultValueSql("NOW()");

        builder.HasIndex(c => new { c.DeviceId, c.IsActive })
            .HasDatabaseName("ix_device_certificates_device_id_is_active");
    }
}
