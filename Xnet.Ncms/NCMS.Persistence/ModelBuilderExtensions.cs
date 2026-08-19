using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NCMS.Persistence;

/// <summary>
/// Reusable EF Core model-configuration conventions shared across entity configurations.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Configures optimistic concurrency via Postgres' xmin system column — no application
    /// property needed. Call from an IEntityTypeConfiguration&lt;T&gt;.Configure(builder).
    /// </summary>
    public static EntityTypeBuilder<TEntity> UseXminConcurrencyToken<TEntity>(
        this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<uint>("xmin").HasColumnType("xid").IsConcurrencyToken().ValueGeneratedOnAddOrUpdate();
        return builder;
    }
}
