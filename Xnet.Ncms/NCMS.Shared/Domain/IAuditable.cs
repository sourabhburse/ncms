namespace NCMS.Shared.Domain;

/// <summary>
/// Marks an entity as carrying creation/last-modification timestamps. Implementations are
/// stamped automatically by NCMS.Persistence's AuditableEntitySaveChangesInterceptor — CreatedAt
/// is set by the entity itself at construction, UpdatedAt is set by the interceptor on every
/// SaveChanges where the entity is Modified.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
