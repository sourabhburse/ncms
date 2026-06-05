namespace NCMS.Backend.Eventing.Abstraction
{
    public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
    {
        Task HandleAsync(TEvent @event, CancellationToken ct = default);
    }
}