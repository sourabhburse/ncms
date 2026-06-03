using Microsoft.EntityFrameworkCore;
using NCMS.Backend.Core.Entities;
using System.Collections.Generic;

namespace NCMS.Backend.Infrastructure.Data;

public class NcmsDbContext : DbContext
{
    public NcmsDbContext(DbContextOptions<NcmsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<HardwareInventory> HardwareInventory => Set<HardwareInventory>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceCertificate> DeviceCertificates => Set<DeviceCertificate>();
    public DbSet<DeviceTelemetry> DeviceTelemetries => Set<DeviceTelemetry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Tenants
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => t.Slug).IsUnique();
            entity.Property(t => t.Name).IsRequired();
            entity.Property(t => t.Slug).IsRequired();
        });

        // Vendors
        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.ToTable("Vendors");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Name).IsRequired();
        });

        // Products
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.ModelName).IsRequired();
            entity.Property(p => p.Architecture).IsRequired();
            entity.Property(p => p.ConfigFormat).IsRequired();
            entity.Property(p => p.ConfigSchemaVersion).HasDefaultValue("1.0");

            entity.HasOne(p => p.Vendor)
                .WithMany()
                .HasForeignKey(p => p.VendorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // HardwareInventory
        modelBuilder.Entity<HardwareInventory>(entity =>
        {
            entity.ToTable("HardwareInventory");
            entity.HasKey(h => h.Id);
            entity.HasIndex(h => h.SerialNumber).IsUnique();
            entity.Property(h => h.SerialNumber).HasMaxLength(64).IsRequired();
            entity.Property(h => h.IdentityPolicy).HasDefaultValue("serial_only");
            
            // Map JSONB dictionary columns
            entity.Property(h => h.IdentityClaims)
                .HasColumnType("jsonb");

            entity.HasOne(h => h.Tenant)
                .WithMany()
                .HasForeignKey(h => h.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Product)
                .WithMany()
                .HasForeignKey(h => h.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Devices
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("Devices");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Status).HasDefaultValue("PROVISIONING");
            
            // Map JSONB dictionary columns
            entity.Property(d => d.MacAddresses)
                .HasColumnType("jsonb");

            // 1:1 relationship with HardwareInventory (where Device holds the foreign key)
            entity.HasOne(d => d.HardwareInventory)
                .WithOne(h => h.Device)
                .HasForeignKey<Device>(d => d.HardwareInventoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Tenant)
                .WithMany()
                .HasForeignKey(d => d.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // DeviceCertificates
        modelBuilder.Entity<DeviceCertificate>(entity =>
        {
            entity.ToTable("DeviceCertificates");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Thumbprint).IsRequired();
            entity.Property(c => c.SubjectName).IsRequired();
            entity.Property(c => c.IsActive).HasDefaultValue(true);

            entity.HasOne(c => c.Device)
                .WithMany(d => d.Certificates)
                .HasForeignKey(c => c.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DeviceTelemetries
        modelBuilder.Entity<DeviceTelemetry>(entity =>
        {
            entity.ToTable("DeviceTelemetries");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.CpuUsagePercent).IsRequired();
            entity.Property(t => t.RamUsageMb).IsRequired();
            entity.Property(t => t.RamTotalMb).IsRequired();
            entity.Property(t => t.StorageUsedMb).IsRequired();
            entity.Property(t => t.StorageTotalMb).IsRequired();
            entity.Property(t => t.UptimeSeconds).IsRequired();

            // Indexing for time-series queries
            entity.HasIndex(t => new { t.DeviceId, t.Timestamp });

            entity.HasOne(t => t.Device)
                .WithMany(d => d.Telemetries)
                .HasForeignKey(t => t.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
