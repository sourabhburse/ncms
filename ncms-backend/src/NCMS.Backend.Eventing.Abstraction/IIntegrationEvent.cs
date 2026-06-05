namespace NCMS.Backend.Eventing.Abstraction
{
    public interface IIntegrationEvent
    {
        Guid Id { get; }
        DateTime OccurredOnUtc { get; }
        string? TenantId { get; }
        string CorrelationId { get; }
        string Source { get; } 
    }
}