using Mediator;
using Microsoft.AspNetCore.HttpOverrides;
using NCMS.IoT.Core;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Mqtt;
using NCMS.IoT.Host.Application.Abstractions;
using NCMS.IoT.Host.Application.Navigation;
using NCMS.IoT.Identity.Configuration;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Infrastructure.Pki;
using NCMS.IoT.MqttClient;
using NCMS.Shared;
using NCMS.Shared.Behaviors;
using NCMS.Shared.Modules;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

// Authenticated by default: every page under the app root requires a signed-in user unless
// its PageModel opts out with [AllowAnonymous] (Login, Logout, AccessDenied).
builder.Services.AddRazorPages(options => options.Conventions.AuthorizeFolder("/"));

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
    // Request.Host without its port and sending framework redirects (e.g. cookie auth's
    // challenge to /Account/Login) to the default port for the scheme instead of the real one.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Razor UI + the MQTT runtime (the single broker client + dispatch/timeout workers).
// The API stays HTTP-only; this Host owns MQTT, so it must always be running for
// telemetry ingestion and firmware/config dispatch.
builder.Services
    .AddCoreModule(builder.Configuration)
    .AddPkiModule(builder.Configuration)
    .AddMqttClientModule(builder.Configuration);

var deviceManagementPermissions = PermissionDiscovery.GetAllPermissions(typeof(DeviceManagementPermissions));
IModule[] modules =
[
    new IdentityModule(deviceManagementPermissions, useCookieAuthentication: true),
    new DeviceManagementModule()
];
builder.Services.AddModules(builder.Configuration, modules);

// The Device Management MQTT adapter: inbound handlers + firmware/config dispatch workers.
builder.Services.AddDeviceManagementMqtt(builder.Configuration);

builder.Services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddSingleton<INavigationMenuProvider, NavigationMenuProvider>();

var app = builder.Build();

// ── Startup sequence (order matters, mirrors NCMS.IoT.Api) ────────────────────

// Step 1: Ensure Root CA exists before IDeviceCertificateIssuer (singleton) resolves.
await app.Services.EnsurePkiAsync();

// Step 2: Run per-module initialization (migrations + seeding).
await modules.InitializeModulesAsync(app.Services);

// ── Middleware pipeline ───────────────────────────────────────────────────────

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// No UseHttpsRedirection() here — TLS termination is nginx's job when running behind a
// reverse proxy. This deployment's nginx does plain HTTP, so forcing an HTTPS redirect here
// would either fail (nothing listening on 443) or silently no-op; either way it's the wrong
// layer to make that decision.
app.UseForwardedHeaders();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();

// Expose the module minimal-API endpoints (e.g. /api/v1/provision, firmware, telemetry)
// alongside the Razor UI so the Host can run as a single self-contained process.
app.MapModuleEndpoints(modules);

// Static assets (css/js/images) must stay anonymous — they're plain files, not app pages,
// and the login page itself needs to load its own scripts/styles before a user is signed in.
// MapStaticAssets() registers real routed endpoints (unlike classic UseStaticFiles()), so
// without this the global RequireAuthenticatedUser() fallback policy 302-redirects every
// asset request to /Account/Login, which the browser then fails to parse as JS/CSS.
app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
