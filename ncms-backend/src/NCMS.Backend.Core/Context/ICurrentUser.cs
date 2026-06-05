using System.Security.Claims;

namespace NCMS.Backend.Core.Context
{
    public interface ICurrentUser
    {
        string? Name {get;}
        
        Guid GetUserId();

        string? GetUserEmail();

        String? GetTenant();

        bool IsAuthenticated();

        bool IsInRole(string role);

        IEnumerable<Claim> GetClaims();
    }
}