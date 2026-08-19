using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Entities;
using NCMS.Shared.Exceptions;

namespace NCMS.IoT.Identity.Features.V1.Users;

public static class GetUser
{
    public sealed record Query(Guid Id) : IRequest<UserDto>;

    public sealed class Handler : IRequestHandler<Query, UserDto>
    {
        private readonly UserManager<AppUser> _userManager;

        public Handler(UserManager<AppUser> userManager) => _userManager = userManager;

        public async ValueTask<UserDto> Handle(Query req, CancellationToken ct)
        {
            var user = await _userManager.FindByIdAsync(req.Id.ToString())
                ?? throw NotFoundException.For<AppUser>(req.Id);

            var roles = await _userManager.GetRolesAsync(user);
            return new UserDto(
                user.Id, user.UserName ?? "", user.Email ?? "",
                user.FirstName, user.LastName, user.IsActive, roles.ToList());
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new Query(id), ct)))
            .WithSummary("Get a user by id")
            .RequireAuthorization(IdentityPermissions.Users.View);
}
