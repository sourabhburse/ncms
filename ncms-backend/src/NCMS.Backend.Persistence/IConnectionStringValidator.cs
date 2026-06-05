namespace NCMS.Backend.Persistence
{
    public interface IConnectionStringValidator
    {
        bool TryValidate(string connectionString, string? dbProvider = null);
    }
}