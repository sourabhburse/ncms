using System.Security.Claims;

namespace NCMS.Backend.Core.Context
{
    public interface ICurrentUserInitializer
    {
        void SetCurrentUser(ClaimsPrincipal user);
        void SetCurrentUserId(Guid userId);    
    }
}