using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NCMS.IoT.Identity.Authorization;
using NCMS.IoT.Identity.Contracts.Services;
using NCMS.IoT.Identity.Data;
using NCMS.IoT.Identity.Data.Seeding;
using NCMS.IoT.Identity.Entities;
using NCMS.IoT.Identity.Features.V1.Audit;
using NCMS.IoT.Identity.Features.V1.Auth;
using NCMS.IoT.Identity.Features.V1.Roles;
using NCMS.IoT.Identity.Features.V1.Users;
using NCMS.IoT.Identity.Services;
using NCMS.Persistence;
using NCMS.Shared;

namespace NCMS.IoT.Identity.Configuration;

/// <summary>
/// The Identity module: users, roles, permission-based authorization, and authentication.
/// <paramref name="additionalPermissions"/> lets a host register another module's permission
/// constants (e.g. DeviceManagementPermissions) for seeding/role-management purposes without
/// Identity taking a project reference on that module.
/// <paramref name="useCookieAuthentication"/> switches the default scheme from JWT bearer
/// (for the HTTP API, <c>NCMS.IoT.Api</c>) to a browser cookie (for the Razor Pages Host,
/// <c>NCMS.IoT.Host</c>) — the Host never validates externally-issued bearer tokens, so it
/// has no need for the JWT scheme at all.
/// </summary>
public sealed class IdentityModule(
    IEnumerable<string>? additionalPermissions = null,
    bool useCookieAuthentication = false) : IModule
{
    private readonly KnownPermissions _knownPermissions = new(additionalPermissions);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddNcmsDbContext<IdentityDbContext>(configuration, "IoTDatabase", "identity");

        services.AddSingleton(_knownPermissions);

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

        if (useCookieAuthentication)
        {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.LogoutPath = "/Account/Logout";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                });
        }
        else
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });
        }

        // Authenticated by default: any endpoint (API or Host) without an explicit
        // .AllowAnonymous()/[AllowAnonymous] now requires a signed-in principal, regardless
        // of which scheme authenticated them.
        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<IRolePermissionCache, RolePermissionCache>();
        services.AddScoped<IUserClaimsBuilder, UserClaimsBuilder>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddValidatorsFromAssemblyContaining<IdentityModule>();
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        var auth = v1.MapGroup("/auth");
        Login.Map(auth);
        RefreshToken.Map(auth);

        var users = v1.MapGroup("/users");
        ListUsers.Map(users);
        GetUser.Map(users);
        CreateUser.Map(users);
        UpdateUser.Map(users);
        DeleteUser.Map(users);
        ChangePassword.Map(users);
        ResetPassword.Map(users);

        var roles = v1.MapGroup("/roles");
        ListRoles.Map(roles);
        CreateRole.Map(roles);
        UpdateRole.Map(roles);
        DeleteRole.Map(roles);
        GetRolePermissions.Map(roles);
        UpdateRolePermissions.Map(roles);

        var auditLogs = v1.MapGroup("/audit-logs");
        ListAuditLogs.Map(auditLogs);
    }

    public async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync(ct);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(IdentityModule));

        await RoleAndAdminSeeder.SeedAsync(roleManager, userManager, _knownPermissions.All, logger, ct);
    }
}
