using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCMS.Backend.Core.Domain;
using NCMS.Backend.Shared.Multitenancy;
using NCMS.Backend.Shared.Persistence;

namespace NCMS.Backend.Persistence.Context
{
    public class BaseDbContext(IMultiTenantContextAccessor<NcmsTenantInfo> multiTenantContextAccessor,
        DbContextOptions options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) 
        : MultiTenantDbContext(multiTenantContextAccessor, options)
    {
        private readonly DatabaseOptions _settings = settings.Value;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            modelBuilder.AppendGlobalQueryFilter<ISoftDeletable>(QueryFilters.SoftDelete, s=> !s.IsDeleted );
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyTenantIsolationByDefault();
            
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            ArgumentNullException.ThrowIfNull(optionsBuilder);
            if(!string.IsNullOrWhiteSpace(multiTenantContextAccessor.MultiTenantContext?.TenantInfo?.ConnectionString))
            {
               optionsBuilder.ConfigureDatabase(
                _settings.Provider,
                multiTenantContextAccessor.MultiTenantContext?.TenantInfo?.ConnectionString!,
                _settings.MigrationsAssembly,
                environment.IsDevelopment()
               );
            }
          
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            TenantNotSetMode = TenantNotSetMode.Overwrite;
            int result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
       
    }
}