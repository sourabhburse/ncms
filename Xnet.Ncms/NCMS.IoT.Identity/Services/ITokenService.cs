using NCMS.IoT.Identity.Entities;

namespace NCMS.IoT.Identity.Services;

public interface ITokenService
{
    Task<(string Token, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)> GenerateTokensAsync(
        AppUser user, CancellationToken ct = default);
}
