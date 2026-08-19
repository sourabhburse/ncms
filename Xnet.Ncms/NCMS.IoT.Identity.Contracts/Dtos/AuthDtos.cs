namespace NCMS.IoT.Identity.Contracts.Dtos;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string Token, string RefreshToken);

public sealed record TokenResponse(string Token, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);
