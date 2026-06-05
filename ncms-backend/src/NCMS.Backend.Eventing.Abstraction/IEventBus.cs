namespace NCMS.Backend.Eventing.Abstraction
{
    public interface IEventBus
    {
        Task PublishAsync(IIntegrationEvent @event, CancellationToken ct = default);
        Task PublishAsync(IEnumerable<IIntegrationEvent> events, CancellationToken ct = default);
    }
}