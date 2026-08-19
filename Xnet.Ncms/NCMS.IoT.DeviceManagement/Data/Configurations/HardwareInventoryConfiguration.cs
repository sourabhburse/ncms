using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class HardwareInventoryConfiguration : IEntityTypeConfiguration<HardwareInventory>
{
    public void Configure(EntityTypeBuilder<HardwareInventory> builder)
    {
        builder.ToTable("hardware_inventory");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.ProductId).IsRequired();
        builder.HasIndex(h => h.ProductId)
            .HasDatabaseName("ix_hardware_inventory_product_id");

        builder.Property(h => h.SerialNumber).IsRequired().HasMaxLength(128);
        builder.HasIndex(h => h.SerialNumber)
            .IsUnique()
            .HasDatabaseName("ix_hardware_inventory_serial_number");

        builder.Property(h => h.IsProvisioned).IsRequired().HasDefaultValue(false);
        builder.Property(h => h.CreatedAt).IsRequired().HasDefaultValueSql("NOW()");
        builder.Property(h => h.UpdatedAt).IsRequired().HasDefaultValueSql("NOW()");

        // Lifecycle & identity fields
        builder.Property(h => h.Status).IsRequired().HasMaxLength(50).HasDefaultValue("PENDING_ACTIVATION");
        builder.Property(h => h.IdentityPolicy).IsRequired().HasMaxLength(100).HasDefaultValue("serial_only");
        builder.Property(h => h.IdentityClaims)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.HasOne(h => h.Product)
            .WithMany(p => p.HardwareInventories)
            .HasForeignKey(h => h.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Device)
            .WithOne(d => d.HardwareInventory)
            .HasForeignKey<Device>(d => d.HardwareInventoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
