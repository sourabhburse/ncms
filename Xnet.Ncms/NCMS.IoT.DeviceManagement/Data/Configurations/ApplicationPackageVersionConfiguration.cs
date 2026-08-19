using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ApplicationPackageVersionConfiguration : IEntityTypeConfiguration<ApplicationPackageVersion>
{
    public void Configure(EntityTypeBuilder<ApplicationPackageVersion> builder)
    {
        builder.ToTable("application_package_versions", "application");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Version).HasMaxLength(50).IsRequired();
        builder.Property(v => v.PackageFormat).HasMaxLength(50).IsRequired();
        builder.Property(v => v.FileName).HasMaxLength(500);
        builder.Property(v => v.StoragePath).HasMaxLength(500);
        builder.Property(v => v.Sha256Checksum).HasMaxLength(64);
        builder.Property(v => v.Metadata).HasColumnType("jsonb");
        builder.Property(v => v.ReleaseNotes).HasMaxLength(2000);
        builder.Property(v => v.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(v => v.UploadedAt).IsRequired();
        builder.Property(v => v.UploadedBy).HasMaxLength(256);

        builder.HasOne(v => v.ApplicationPackage)
            .WithMany(p => p.Versions)
            .HasForeignKey(v => v.ApplicationPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.ApplicationPackageId, v.Version, v.PackageFormat })
            .IsUnique()
            .HasDatabaseName("ix_application_package_versions_package_version_format");

        // Common filter: catalog/task selectors list enabled versions only.
        builder.HasIndex(v => new { v.ApplicationPackageId, v.IsEnabled })
            .HasDatabaseName("ix_application_package_versions_package_enabled");
    }
}
