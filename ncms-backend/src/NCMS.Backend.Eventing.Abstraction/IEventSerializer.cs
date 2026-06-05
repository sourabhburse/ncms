namespace NCMS.Backend.Eventing.Abstraction
{
    public interface IEventSerializer
    {
        string Serialize(IIntegrationEvent @event);
        IIntegrationEvent? Deserialize(string payload, string eventTypeName);
    }
}