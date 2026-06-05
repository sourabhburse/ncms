//namespace NCMS.Backend.Core.Domain
//{
//    public abstract record DomainEvent(
//        Guid EventId,
//        DateTimeOffset OccurredOnUtc,
//        string? CorrelationId = null,
//        string? TenantId = null
//    ): IDomainEvent
//    {
//        public static T Create<T>(Func<Guid, DateTimeOffset,T> factory)
//            where T : DomainEvent
//        {
//            ArgumentNullException.ThrowIfNull(factory);
//            return factory(Guid.NewGuid(), DateTimeOffset.UtcNow);
//        }
       
//    }
//}