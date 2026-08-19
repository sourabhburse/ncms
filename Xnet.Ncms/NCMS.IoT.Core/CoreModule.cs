using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCMS.IoT.Core.Configuration;

namespace NCMS.IoT.Core;

public static class CoreModule
{
    /// <summary>
    /// Registers Core configuration options (RootCaOptions).
    /// PKI service registrations live in NCMS.IoT.Infrastructure.Pki — call AddPkiModule() there.
    /// </summary>
    public static IServiceCollection AddCoreModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RootCaOptions>(
            configuration.GetSection(RootCaOptions.SectionName));

        return services;
    }
}
