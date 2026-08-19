using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NCMS.Persistence.Interceptors;

namespace NCMS.Persistence;

/// <summary>
/// Single entry point for registering a module's EF Core DbContext against this platform's
/// database provider (PostgreSQL). Centralizes the Npgsql/EF wiring so every module's own
/// entities, configurations, and migrations stay in the module itself.
/// </summary>
public static class PersistenceExtensions
{
    public static IServiceCollection AddNcmsDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName,
        string migrationsHistorySchema,
        Action<DbContextOptionsBuilder>? configureOptions = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringName);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationsHistorySchema);

        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' not found.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.TryAddScoped<ISaveChangesInterceptor, AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<TContext>((sp, options) =>
        {
            options.UseNpgsql(dataSource, npgsql =>
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", migrationsHistorySchema))
                .AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            configureOptions?.Invoke(options);
        });

        return services;
    }
}
