using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NCMS.IoT.Identity.Configuration;
using NCMS.IoT.Identity.Entities;

namespace NCMS.IoT.Identity.Services;

public sealed class TokenService : ITokenService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUserClaimsBuilder _claimsBuilder;
    private readonly JwtOptions _options;

    public TokenService(UserManager<AppUser> userManager, IUserClaimsBuilder claimsBuilder, IOptions<JwtOptions> options)
    {
        _userManager = userManager;
        _claimsBuilder = claimsBuilder;
        _options = options.Value;
    }

    public async Task<(string Token, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt)> GenerateTokensAsync(
        AppUser user, CancellationToken ct = default)
    {
        var claims = await _claimsBuilder.BuildAsync(user, ct);
        claims.Add(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
        claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();
        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenExpiryDays);

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = refreshTokenExpiresAt;
        await _userManager.UpdateAsync(user);

        return (accessToken, refreshToken, refreshTokenExpiresAt);
    }

    private static string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
