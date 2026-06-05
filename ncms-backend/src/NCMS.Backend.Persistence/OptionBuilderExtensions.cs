
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NCMS.Backend.Shared.Persistence;

namespace NCMS.Backend.Persistence
{
    public static class OptionBuilderExtensions
    {
        public static DbContextOptionsBuilder ConfigureNcmsDatabase(this DbContextOptionsBuilder builder,
            string dbProvider, 
            string connectionString, 
            string migrationsAssembly, 
            bool isDevelopment)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(dbProvider);
            builder.ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning));

            switch (dbProvider.ToUpperInvariant())
            {
                case DbProviders.PostgresSQL:
                    builder.UseNpgsql(connectionString, options =>
                    {
                        options.EnableRetryOnFailure();
                        options.MigrationsAssembly(migrationsAssembly);
                    });
                    break;
                case DbProviders.MSSQL:
                    // builder.UseSqlServer(connectionString, options =>
                    // {
                    //     options.EnableRetryOnFailure();
                    //     options.MigrationsAssembly(migrationsAssembly);
                    // });
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Database  Provider {dbProvider} is not supported. Please check your configuration."
                    );
            }
            if(isDevelopment)
            {
                builder.EnableSensitiveDataLogging();
                builder.EnableDetailedErrors();
            }
            return builder;
        }
    }
}