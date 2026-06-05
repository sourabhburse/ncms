using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using NCMS.Backend.Core.Domain;

namespace NCMS.Backend.Persistence;

public static class TenantIsolationExtensions
{
    private const string FinbuckleMultitenantAnnotation = "Finbuckle.MultiTenant";
    
    public static void ApplyTenantIsolationByDefault(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if(entityType.IsOwned()) continue;
            if(entityType.ClrType is null) continue;
            if(entityType.FindPrimaryKey() is null) continue;
            if(typeof(IGlobalEntity).IsAssignableFrom(entityType.ClrType)) continue;
            
            if(entityType.FindAnnotation(FinbuckleMultitenantAnnotation) is not null) continue;
            modelBuilder.Entity(entityType.ClrType).IsMultiTenant().AdjustUniqueIndexes();
      
        }
    }
}