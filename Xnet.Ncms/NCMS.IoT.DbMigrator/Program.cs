using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.Identity.Configuration;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.Shared;
using NCMS.Shared.Modules;

// One-shot process: apply every module's pending EF Core migrations and run its seed data,
// then exit. Deliberately does not start MQTT, does not bootstrap PKI, does not serve HTTP —
// its only job is the "Step 2" InitializeModulesAsync() call that NCMS.IoT.Api and
// NCMS.IoT.Host already perform at their own startup. Both keep doing that themselves for
// local-dev convenience (a solo `dotnet run` still just works, no extra step) — this project
// is an additional, explicit way to apply migrations out-of-band (CI, an init container,
// or any environment where migrating from inside the web process isn't wanted), not a
// replacement for it.

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var deviceManagementPermissions = PermissionDiscovery.GetAllPermissions(typeof(DeviceManagementPermissions));
IModule[] modules = [new IdentityModule(deviceManagementPermissions), new DeviceManagementModule()];
builder.Services.AddModules(builder.Configuration, modules);

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Applying migrations for {Count} module(s)...", modules.Length);
    await modules.InitializeModulesAsync(app.Services);
    logger.LogInformation("Migrations applied successfully.");
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Migration failed.");
    return 1;
}
