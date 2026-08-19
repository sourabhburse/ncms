using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ApplicationPackageDependencyConfiguration : IEntityTypeConfiguration<ApplicationPackageDependency>
{
    public void Configure(EntityTypeBuilder<ApplicationPackageDependency> builder)
    {
        builder.ToTable("application_package_dependencies", "application");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.VersionConstraint).HasMaxLength(100);

        builder.HasOne(d => d.ApplicationPackageVersion)
            .WithMany(v => v.Dependencies)
            .HasForeignKey(d => d.ApplicationPackageVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.DependsOnApplicationPackage)
            .WithMany()
            .HasForeignKey(d => d.DependsOnApplicationPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.ApplicationPackageVersionId, d.DependsOnApplicationPackageId })
            .IsUnique()
            .HasDatabaseName("ix_application_package_dependencies_version_target");
    }
}
