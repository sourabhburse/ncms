using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class DeviceApplicationInventoryConfiguration : IEntityTypeConfiguration<DeviceApplicationInventory>
{
    public void Configure(EntityTypeBuilder<DeviceApplicationInventory> builder)
    {
        builder.ToTable("device_application_inventory", "public");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Status).HasConversion<int>().IsRequired();
        builder.Property(i => i.InstalledAt).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();

        builder.HasOne(i => i.Device)
            .WithMany()
            .HasForeignKey(i => i.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.ApplicationPackage)
            .WithMany()
            .HasForeignKey(i => i.ApplicationPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.InstalledVersion)
            .WithMany()
            .HasForeignKey(i => i.InstalledVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Exactly one "current" row per device per logical package.
        builder.HasIndex(i => new { i.DeviceId, i.ApplicationPackageId })
            .IsUnique()
            .HasDatabaseName("ux_device_application_inventory_device_package");
    }
}
