using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Entities;
using NCMS.IoT.Identity.Services;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Identity.Features.V1.Auth;

public static class RefreshToken
{
    public sealed record Command(string RefreshToken) : IRequest<TokenResponse>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.RefreshToken).NotEmpty();
    }

    public sealed class Handler : IRequestHandler<Command, TokenResponse>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;

        public Handler(UserManager<AppUser> userManager, ITokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
        }

        public async ValueTask<TokenResponse> Handle(Command req, CancellationToken ct)
        {
            var user = await _userManager.Users
                .Where(u => u.RefreshToken == req.RefreshToken)
                .FirstOrDefaultAsync(ct);

            if (user is null || user.RefreshTokenExpiresAt is null || user.RefreshTokenExpiresAt < DateTimeOffset.UtcNow)
                throw new DomainException("Invalid or expired refresh token.");

            var (token, refreshToken, refreshTokenExpiresAt) = await _tokenService.GenerateTokensAsync(user, ct);
            return new TokenResponse(token, refreshToken, refreshTokenExpiresAt);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/refresh", async (RefreshTokenRequest request, ISender sender, CancellationToken ct) =>
        {
            var response = await sender.Send(new Command(request.RefreshToken), ct);
            return Results.Ok(response);
        })
        .WithSummary("Exchange a refresh token for a new access + refresh token pair")
        .AllowAnonymous();
}
