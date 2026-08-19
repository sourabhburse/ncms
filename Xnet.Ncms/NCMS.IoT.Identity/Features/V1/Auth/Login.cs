using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Entities;
using NCMS.IoT.Identity.Services;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Identity.Features.V1.Auth;

public static class Login
{
    public sealed record Command(string Email, string Password) : IRequest<TokenResponse>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    public sealed class Handler : IRequestHandler<Command, TokenResponse>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ITokenService _tokenService;
        private readonly IAuditLogService _auditLog;

        public Handler(UserManager<AppUser> userManager, ITokenService tokenService, IAuditLogService auditLog)
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _auditLog = auditLog;
        }

        public async ValueTask<TokenResponse> Handle(Command req, CancellationToken ct)
        {
            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user is null || !user.IsActive || !await _userManager.CheckPasswordAsync(user, req.Password))
            {
                await _auditLog.RecordAsync(
                    AuditEventType.LoginFailed,
                    $"Failed login attempt for '{req.Email}'.",
                    subjectUserId: user?.Id,
                    subjectDisplay: req.Email,
                    ct: ct);
                throw new DomainException("Invalid email or password.");
            }

            var (token, refreshToken, refreshTokenExpiresAt) = await _tokenService.GenerateTokensAsync(user, ct);

            await _auditLog.RecordAsync(
                AuditEventType.LoginSucceeded,
                $"User '{user.Email}' logged in.",
                subjectUserId: user.Id,
                subjectDisplay: user.Email,
                actorUserId: user.Id,
                actorDisplay: user.Email,
                ct: ct);

            return new TokenResponse(token, refreshToken, refreshTokenExpiresAt);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/login", async (LoginRequest request, ISender sender, CancellationToken ct) =>
        {
            var response = await sender.Send(new Command(request.Email, request.Password), ct);
            return Results.Ok(response);
        })
        .WithSummary("Authenticate with email + password and receive an access + refresh token")
        .AllowAnonymous();
}
