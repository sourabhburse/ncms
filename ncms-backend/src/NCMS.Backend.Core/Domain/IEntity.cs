namespace NCMS.Backend.Core.Domain
{
    public interface IEntity<out TId>
    {
        TId Id { get; }
    }
   
}