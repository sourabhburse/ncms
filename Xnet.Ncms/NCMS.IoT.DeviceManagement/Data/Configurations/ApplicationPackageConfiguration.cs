using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ApplicationPackageConfiguration : IEntityTypeConfiguration<ApplicationPackage>
{
    public void Configure(EntityTypeBuilder<ApplicationPackage> builder)
    {
        builder.ToTable("application_packages", "application");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Tags).HasColumnType("text[]").IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasIndex(p => p.Name)
            .IsUnique()
            .HasDatabaseName("ix_application_packages_name");
    }
}
