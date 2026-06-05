using System.ComponentModel.DataAnnotations;

namespace NCMS.Backend.Shared.Persistence
{
    public sealed class DatabaseOptions : IValidatableObject
    {
       public string Provider { get; set; } = DbProviders.PostgresSQL;
       public string ConnectionString { get; set; } = string.Empty;

       public string MigrationsAssembly { get; set; } = string.Empty;
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if(string.IsNullOrWhiteSpace(ConnectionString))
            {
                yield return new ValidationResult("connection string cannot be empty.", [nameof(ConnectionString)]);
            }
        }
    }
}