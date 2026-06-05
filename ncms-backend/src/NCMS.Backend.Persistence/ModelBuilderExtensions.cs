using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace NCMS.Backend.Persistence
{
    internal static class ModelBuilderExtensions
    {
        public static ModelBuilder AppendGlobalQueryFilter<TInterface>(
            this ModelBuilder modelBuilder,
            string filterName,
            Expression<Func<TInterface, bool>> filter)
        {
          var entities = modelBuilder.Model.GetEntityTypes()
                .Where(e => e.BaseType is null && e.ClrType.GetInterface(typeof(TInterface ).Name) is not null)
                .Select(e => e.ClrType);

            foreach (var entity in entities)
            {
                var parameterType = Expression.Parameter(modelBuilder.Entity(entity).Metadata.ClrType);
                var filterBody = ReplacingExpressionVisitor.Replace(filter.Parameters.Single(), parameterType, filter.Body);
                modelBuilder.Entity(entity).HasQueryFilter(filterName,Expression.Lambda(filterBody, parameterType));
            }

            return modelBuilder;
        }
    }
}