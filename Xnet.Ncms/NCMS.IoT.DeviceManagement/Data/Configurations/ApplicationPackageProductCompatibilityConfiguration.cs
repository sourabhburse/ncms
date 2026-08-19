using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ApplicationPackageProductCompatibilityConfiguration : IEntityTypeConfiguration<ApplicationPackageProductCompatibility>
{
    public void Configure(EntityTypeBuilder<ApplicationPackageProductCompatibility> builder)
    {
        builder.ToTable("application_package_product", "application");

        // Composite primary key enforces the uniqueness invariant, mirrors FirmwareProduct.
        builder.HasKey(c => new { c.ApplicationPackageVersionId, c.ProductId });

        builder.HasOne(c => c.ApplicationPackageVersion)
            .WithMany(v => v.SupportedProducts)
            .HasForeignKey(c => c.ApplicationPackageVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Product)
            .WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.ProductId)
            .HasDatabaseName("ix_application_package_product_product_id");
    }
}
