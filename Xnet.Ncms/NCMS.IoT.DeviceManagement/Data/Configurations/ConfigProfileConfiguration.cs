using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data.Configurations;

internal sealed class ConfigProfileConfiguration : IEntityTypeConfiguration<ConfigProfile>
{
    public void Configure(EntityTypeBuilder<ConfigProfile> builder)
    {
        builder.ToTable("config_profiles", "config");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProfileName).HasMaxLength(256).IsRequired();
        builder.Property(p => p.OriginalFilename).HasMaxLength(500);
        builder.Property(p => p.StoragePath).HasMaxLength(500);
        builder.Property(p => p.Md5).HasMaxLength(64);
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.Remark).HasMaxLength(2000);
        builder.Property(p => p.CreatedBy).HasMaxLength(256);

        builder.HasOne(p => p.Product)
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.ProductId)
            .HasDatabaseName("ix_config_profiles_product_id");
    }
}
