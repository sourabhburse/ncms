using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class FirmwareProductConfiguration : IEntityTypeConfiguration<FirmwareProduct>
{
    public void Configure(EntityTypeBuilder<FirmwareProduct> builder)
    {
        builder.ToTable("firmware_products", "firmware");

        // Composite primary key enforces the uniqueness invariant:
        // a firmware binary can only be listed as compatible with a product once.
        builder.HasKey(fp => new { fp.FirmwareId, fp.ProductId });

        builder.HasOne(fp => fp.Firmware)
            .WithMany(f => f.SupportedProducts)
            .HasForeignKey(fp => fp.FirmwareId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(fp => fp.Product)
            .WithMany(p => p.FirmwareProducts)
            .HasForeignKey(fp => fp.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Support reverse lookup: "which firmware packages are compatible with this product?"
        builder.HasIndex(fp => fp.ProductId)
            .HasDatabaseName("ix_firmware_products_product_id");
    }
}
