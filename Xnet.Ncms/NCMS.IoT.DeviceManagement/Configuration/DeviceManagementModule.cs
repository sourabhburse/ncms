using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCMS.Persistence;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Data.Seeding;
using NCMS.IoT.DeviceManagement.Features.V1.Configuration.ConfigureTasks;
using NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;
using NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;
using NCMS.IoT.DeviceManagement.Features.V1.Firmware.UpgradeTasks;
using NCMS.IoT.DeviceManagement.Contracts.Services;
using NCMS.IoT.DeviceManagement.Features.V1.Dashboard;
using NCMS.IoT.DeviceManagement.Features.V1.Devices;
using NCMS.IoT.DeviceManagement.Features.V1.Inventory;
using NCMS.IoT.DeviceManagement.Features.V1.Registration;
using NCMS.IoT.DeviceManagement.Features.V1.Telemetry;
using NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;
using NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Tasks;
using NCMS.IoT.DeviceManagement.Services;
using NCMS.Shared;

namespace NCMS.IoT.DeviceManagement.Configuration;

public sealed class DeviceManagementModule : IModule
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNcmsDbContext<DeviceManagementDbContext>(
            configuration, "IoTDatabase", "device_management",
            configureOptions: options => options.ConfigureWarnings(w =>
                // ConfigProfile/FirmwareProduct/HardwareInventory/ApplicationPackageProductCompatibility
                // all have a genuinely required (non-nullable) FK to Product, which itself carries a
                // soft-delete query filter (!IsDeleted). EF flags this because Include(...).Product
                // would silently exclude rows once their Product is soft-deleted. That's a deliberate,
                // reviewed trade-off here: Product.SoftDelete() has no caller yet (no delete-product
                // feature is wired up), and when one is added it must not make existing hardware
                // inventory, config profiles, or compatibility records vanish from queries just
                // because their product model was discontinued from the catalog.
                w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)
                 // Every FirstOrDefaultAsync/FirstAsync in this codebase filters by a unique key
                 // (primary key lookups in TaskReconciliation, a unique DeviceId+PackageId pair in
                 // ApplicationInventorySync, etc.) — genuinely at-most-one-row queries that don't
                 // need an OrderBy to be deterministic. Reviewed and confirmed safe; this warning
                 // was firing on every 30s dispatcher tick per in-progress task and drowning out
                 // logs that actually matter.
                 .Ignore(CoreEventId.FirstWithoutOrderByAndFilterWarning)));

        services.Configure<ZtpOptions>(configuration.GetSection(ZtpOptions.SectionName));
        services.Configure<FileStorageOptions>(
            configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<DevicePresenceOptions>(
            configuration.GetSection(DevicePresenceOptions.SectionName));

        services.AddValidatorsFromAssemblyContaining<DeviceManagementModule>();

        services.AddScoped<IDevicePresenceService, DevicePresenceService>();

        // NOTE: The MQTT adapter (publishers, inbound handlers, dispatch/timeout workers)
        // lives in this assembly (Mqtt/ + BackgroundServices/) but is registered separately
        // via AddDeviceManagementMqtt(). Only the process that owns the broker client (the
        // Host) calls it; the HTTP API uses this module without ever starting MQTT.
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        RegisterDevice.Map(v1);

        var dashboard = v1.MapGroup("/dashboard");
        GetDashboardSummary.Map(dashboard);

        var devices = v1.MapGroup("/devices");
        ListDevices.Map(devices);
        GetDeviceDetail.Map(devices);

        var inventory = v1.MapGroup("/inventory");
        ListHardwareInventory.Map(inventory);

        var telemetry = v1.MapGroup("/telemetry");
        ListTelemetry.Map(telemetry);

        var packages = v1.MapGroup("/firmware/packages");
        ListPackages.Map(packages);
        GetPackage.Map(packages);
        CreatePackage.Map(packages);
        UpdatePackage.Map(packages);
        DeletePackage.Map(packages);
        UploadPackageBinary.Map(packages);
        SetPackageEnabled.Map(packages);

        var tasks = v1.MapGroup("/firmware/upgrade-tasks");
        ListUpgradeTasks.Map(tasks);
        GetUpgradeTaskDetail.Map(tasks);
        CreateUpgradeTask.Map(tasks);
        AbortUpgradeTaskJob.Map(tasks);
        SearchDevices.Map(tasks);

        var profiles = v1.MapGroup("/config/profiles");
        ListProfiles.Map(profiles);
        CreateProfile.Map(profiles);
        UpdateProfile.Map(profiles);
        DeleteProfile.Map(profiles);
        UploadProfileFile.Map(profiles);
        SetProfileEnabled.Map(profiles);
        CopyProfile.Map(profiles);

        var configTasks = v1.MapGroup("/config/tasks");
        ListConfigureTasks.Map(configTasks);
        GetConfigureTaskDetail.Map(configTasks);
        CreateConfigureTask.Map(configTasks);
        AbortConfigureTaskJob.Map(configTasks);
        AbortConfigureTask.Map(configTasks);
        SearchConfigDevices.Map(configTasks);

        var applicationTasks = v1.MapGroup("/application/tasks");
        ListApplicationTasks.Map(applicationTasks);
        GetApplicationTaskDetail.Map(applicationTasks);
        CreateApplicationTask.Map(applicationTasks);
        AbortApplicationTaskJob.Map(applicationTasks);
        SearchApplicationDevices.Map(applicationTasks);

        var applicationPackages = v1.MapGroup("/application/packages");
        ListApplicationPackages.Map(applicationPackages);
        CreateApplicationPackage.Map(applicationPackages);
        CreateApplicationPackageWithVersion.Map(applicationPackages);
        UpdateApplicationPackage.Map(applicationPackages);
        ListApplicationPackageVersions.Map(applicationPackages);
        GetApplicationPackageVersionDetail.Map(applicationPackages);
        CreateApplicationPackageVersion.Map(applicationPackages);
        UploadApplicationPackageVersionBinary.Map(applicationPackages);
        SetApplicationPackageVersionCompatibility.Map(applicationPackages);
        SetApplicationPackageVersionDependencies.Map(applicationPackages);
        UpdateApplicationPackageVersionDetails.Map(applicationPackages);
        SetApplicationPackageVersionEnabled.Map(applicationPackages);
        DeleteApplicationPackageVersion.Map(applicationPackages);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        await db.Database.MigrateAsync(ct);
        await ProductCatalogSeeder.SeedAsync(db, ct);
    }
}
