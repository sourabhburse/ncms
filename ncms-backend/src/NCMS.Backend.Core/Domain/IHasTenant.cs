namespace NCMS.Backend.Core.Domain
{
    public interface IHasTenant
    {
        string TenantId { get; }
    }
}