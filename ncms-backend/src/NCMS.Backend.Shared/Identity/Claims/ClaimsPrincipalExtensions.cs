using System.Security.Claims;
using NCMS.Backend.Shared.Constants;

namespace NCMS.Backend.Shared.Identity.Claims
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetEmail(this ClaimsPrincipal principal) =>
                principal?.FindFirstValue(ClaimTypes.Email);

        public static string? GetTenant(this ClaimsPrincipal principal) =>
                principal?.FindFirstValue(ClaimConstants.Tenant);
        
        public static string? GetFullName(this ClaimsPrincipal principal) =>
                principal?.FindFirstValue(ClaimConstants.FullName);

        public static string? GetFirstName(this ClaimsPrincipal principal) =>
                principal?.FindFirstValue(ClaimTypes.Name);

        public static string? GetSurname(this ClaimsPrincipal principal) =>
                principal?.FindFirstValue(ClaimTypes.Surname);

        public static string? GetPhoneNumber(this ClaimsPrincipal principal) =>
                principal?.FindFirstValue(ClaimTypes.MobilePhone);

        public static string? GetUserId(this ClaimsPrincipal principal) =>
                principal?.FindFirstValue(ClaimTypes.NameIdentifier);   
        
        public static Uri? GetImageUrl(this ClaimsPrincipal principal)
        {
            var imageUrl = principal?.FindFirstValue(ClaimConstants.ImageUrl);
            return Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) ? uri : null;
        }

        public static DateTimeOffset GetExpiration(this ClaimsPrincipal principal)
        {
            var expiration = principal?.FindFirstValue(ClaimConstants.Expiration);
            return expiration != null
                ? DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(expiration))
                : throw new InvalidOperationException("Expiration claim not found.");
        }

        private static string? FindFirstValue(this ClaimsPrincipal principal, string claimType) =>
            principal?.FindFirst(claimType)?.Value;

    }
}