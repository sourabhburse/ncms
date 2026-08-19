using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Data;

public sealed class DeviceManagementDbContext : DbContext
{
    public DeviceManagementDbContext(DbContextOptions<DeviceManagementDbContext> options) : base(options) { }

    // ── Product catalog (schema: products) ───────────────────────────────────
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductType> ProductTypes => Set<ProductType>();
    public DbSet<Product> Products => Set<Product>();

    // ── Hardware & Device (schema: public) ────────────────────────────────────
    public DbSet<HardwareInventory> HardwareInventory => Set<HardwareInventory>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceCertificate> DeviceCertificates => Set<DeviceCertificate>();
    public DbSet<TelemetryRecord> TelemetryRecords => Set<TelemetryRecord>();
    public DbSet<DeviceTelemetry> DeviceTelemetries => Set<DeviceTelemetry>();
    public DbSet<DeviceEvent> DeviceEvents => Set<DeviceEvent>();

    // ── Firmware (schema: firmware) ───────────────────────────────────────────
    public DbSet<Firmware> Firmwares => Set<Firmware>();
    public DbSet<FirmwareProduct> FirmwareProducts => Set<FirmwareProduct>();

    // ── Upgrade tasks (schema: firmware) ─────────────────────────────────────
    public DbSet<UpgradeTask> UpgradeTasks => Set<UpgradeTask>();
    public DbSet<UpgradeTaskDevice> UpgradeTaskDevices => Set<UpgradeTaskDevice>();

    // ── Configuration (schema: config) ───────────────────────────────────────
    public DbSet<ConfigProfile> ConfigProfiles => Set<ConfigProfile>();
    public DbSet<ConfigureTask> ConfigureTasks => Set<ConfigureTask>();
    public DbSet<ConfigureTaskDevice> ConfigureTaskDevices => Set<ConfigureTaskDevice>();

    // ── Application packages (schema: application) ───────────────────────────
    public DbSet<ApplicationPackage> ApplicationPackages => Set<ApplicationPackage>();
    public DbSet<ApplicationPackageVersion> ApplicationPackageVersions => Set<ApplicationPackageVersion>();
    public DbSet<ApplicationPackageDependency> ApplicationPackageDependencies => Set<ApplicationPackageDependency>();
    public DbSet<ApplicationPackageProductCompatibility> ApplicationPackageProductCompat => Set<ApplicationPackageProductCompatibility>();

    // ── Application tasks (schema: application) ──────────────────────────────
    public DbSet<ApplicationTask> ApplicationTasks => Set<ApplicationTask>();
    public DbSet<ApplicationTaskDevice> ApplicationTaskDevices => Set<ApplicationTaskDevice>();

    // ── Device application state (schema: public) ────────────────────────────
    public DbSet<DeviceApplicationInventory> DeviceApplicationInventory => Set<DeviceApplicationInventory>();
    public DbSet<ApplicationInstallationHistory> ApplicationInstallationHistory => Set<ApplicationInstallationHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DeviceManagementDbContext).Assembly);
    }
}
