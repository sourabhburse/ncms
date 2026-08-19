using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NCMS.Shared.Modules;

public static class ModuleExtensions
{
    public static IServiceCollection AddModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<IModule> modules)
    {
        foreach (var module in modules)
            module.ConfigureServices(services, configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapModuleEndpoints(
        this IEndpointRouteBuilder app,
        IEnumerable<IModule> modules)
    {
        foreach (var module in modules)
            module.MapEndpoints(app);
        return app;
    }

    public static async Task InitializeModulesAsync(
        this IEnumerable<IModule> modules,
        IServiceProvider services,
        CancellationToken ct = default)
    {
        foreach (var module in modules)
            await module.InitializeAsync(services, ct);
    }
}
