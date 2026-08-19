using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCMS.IoT.Core.Domain.Interfaces;

namespace NCMS.IoT.Infrastructure.Pki;

public static class PkiModule
{
    /// <summary>
    /// Registers the BouncyCastle PKI engine and the Root CA bootstrapper.
    /// Call <see cref="EnsurePkiAsync"/> at application startup before the first request.
    /// </summary>
    public static IServiceCollection AddPkiModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // RootCaBootstrapper runs once at startup to ensure CA files exist.
        // Must be singleton so it is resolved before CertificateIssuer.
        services.AddSingleton<RootCaBootstrapper>();

        // Singleton: the CA cert and key are loaded once; re-loading per request is wasteful.
        services.AddSingleton<IDeviceCertificateIssuer, CertificateIssuer>();

        return services;
    }

    /// <summary>
    /// Ensures the Root CA certificate and private key exist on disk.
    /// Call at application startup, before the first request is served
    /// and before <see cref="IDeviceCertificateIssuer"/> is first resolved.
    /// </summary>
    public static async Task EnsurePkiAsync(
        this IServiceProvider services,
        CancellationToken ct = default)
    {
        var bootstrapper = services.GetRequiredService<RootCaBootstrapper>();
        await bootstrapper.EnsureAsync(ct);
    }
}
