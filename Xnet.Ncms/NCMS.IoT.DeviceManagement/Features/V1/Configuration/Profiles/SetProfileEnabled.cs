using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;

/// <summary>Enable or disable a config profile. Disabled profiles are hidden from the task selector.</summary>
public static class SetProfileEnabled
{
    public sealed record Command(Guid Id, bool IsEnabled) : IRequest<ConfigProfileDto>;

    public sealed class Handler : IRequestHandler<Command, ConfigProfileDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ConfigProfileDto> Handle(Command cmd, CancellationToken ct)
        {
            var profile = await _db.ConfigProfiles.FindAsync([cmd.Id], ct)
                ?? throw new KeyNotFoundException($"Config profile {cmd.Id} not found.");

            profile.Status = cmd.IsEnabled ? ProfileStatus.Enable : ProfileStatus.Disable;
            await _db.SaveChangesAsync(ct);
            return ListProfiles.Handler.ToDto(profile);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/enabled", async (Guid id, bool isEnabled, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new Command(id, isEnabled), ct)))
        .RequireAuthorization(DeviceManagementPermissions.ConfigProfiles.EnableDisable)
        .WithSummary("Enable or disable a config profile");
}
