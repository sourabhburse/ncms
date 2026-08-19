using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.IsDeleted).HasDefaultValue(false);

        // Capability flags — explicit columns, not a bitmask.
        builder.Property(p => p.SupportsFirmwareUpgrade).HasDefaultValue(false);
        builder.Property(p => p.SupportsConfiguration).HasDefaultValue(false);
        builder.Property(p => p.SupportsRemoteConfiguration).HasDefaultValue(false);
        builder.Property(p => p.SupportsCommands).HasDefaultValue(false);
        builder.Property(p => p.SupportsRealtimeLogs).HasDefaultValue(false);
        builder.Property(p => p.SupportsTelemetry).HasDefaultValue(false);
        builder.Property(p => p.SupportsSoftwarePackages).HasDefaultValue(false);

        // Name unique within the same ProductType.
        builder.HasIndex(p => new { p.ProductTypeId, p.Name })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = FALSE")
            .HasDatabaseName("ix_products_type_name");

        // Filtered index for firmware-upgradeable products (common query filter).
        builder.HasIndex(p => p.SupportsFirmwareUpgrade)
            .HasFilter("\"SupportsFirmwareUpgrade\" = TRUE")
            .HasDatabaseName("ix_products_supports_firmware_upgrade");

        // Filtered index for software-package-capable products (device selector queries).
        builder.HasIndex(p => p.SupportsSoftwarePackages)
            .HasFilter("\"SupportsSoftwarePackages\" = TRUE")
            .HasDatabaseName("ix_products_supports_software_packages");

        builder.HasOne(p => p.ProductType)
            .WithMany(t => t.Products)
            .HasForeignKey(p => p.ProductTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
