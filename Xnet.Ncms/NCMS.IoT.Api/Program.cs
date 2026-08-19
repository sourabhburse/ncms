using Mediator;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using NCMS.IoT.Api.Middleware;
using NCMS.IoT.Core;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.Identity.Configuration;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Infrastructure.Pki;
using NCMS.Shared;
using NCMS.Shared.Behaviors;
using NCMS.Shared.Modules;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;

    // Trust the reverse proxy regardless of its peer address. Without this, the default
    // KnownProxies/KnownNetworks (loopback-only, with strict IP matching) can silently drop
    // X-Forwarded-Host/Proto if nginx's connecting address isn't recognized — leaving
    // Request.Host without its port and sending framework redirects to the default port for
    // the scheme instead of the real one.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// HTTP-only backend: device registration + DB query/command APIs. No MQTT here —
// the broker connection and dispatch workers live in NCMS.IoT.MqttWorker.
builder.Services
    .AddCoreModule(builder.Configuration)
    .AddPkiModule(builder.Configuration);

var deviceManagementPermissions = PermissionDiscovery.GetAllPermissions(typeof(DeviceManagementPermissions));
IModule[] modules = [new IdentityModule(deviceManagementPermissions), new DeviceManagementModule()];
builder.Services.AddModules(builder.Configuration, modules);

builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// ── Build application ─────────────────────────────────────────────────────────

var app = builder.Build();

// ── Startup sequence (order matters) ─────────────────────────────────────────

// Step 1: Ensure Root CA exists before IDeviceCertificateIssuer (singleton) resolves
await app.Services.EnsurePkiAsync();

// Step 2: Run per-module initialization (migrations + seeding)
await modules.InitializeModulesAsync(app.Services);

// ── Middleware pipeline ───────────────────────────────────────────────────────

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapOpenApi();
app.MapScalarApiReference();

// No UseHttpsRedirection() here — TLS termination is nginx's job when running behind a
// reverse proxy; this deployment's nginx does plain HTTP, so forcing an HTTPS redirect at
// this layer is the wrong call (see NCMS.IoT.Host/Program.cs for the full rationale).
app.UseForwardedHeaders();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();

app.MapModuleEndpoints(modules);

await app.RunAsync();
