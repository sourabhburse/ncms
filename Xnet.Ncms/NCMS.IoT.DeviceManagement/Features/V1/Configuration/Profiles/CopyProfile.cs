using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;

/// <summary>
/// Duplicates a config profile (same product + file content), created Disabled.
/// </summary>
public static class CopyProfile
{
    public sealed record Command(Guid Id) : IRequest<ConfigProfileDto>;

    public sealed class Handler : IRequestHandler<Command, ConfigProfileDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ConfigProfileDto> Handle(Command cmd, CancellationToken ct)
        {
            var src = await _db.ConfigProfiles
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == cmd.Id, ct)
                ?? throw new KeyNotFoundException($"Config profile {cmd.Id} not found.");

            var copy = new ConfigProfile
            {
                Id = Guid.NewGuid(),
                ProfileName = $"{src.ProfileName} (copy)",
                OriginalFilename = src.OriginalFilename,
                StoragePath = src.StoragePath,
                Size = src.Size,
                Md5 = src.Md5,
                ProductId = src.ProductId,
                Product = src.Product,
                Status = ProfileStatus.Disable,
                Remark = src.Remark,
                CreatedBy = "system",
                UploadedAt = DateTimeOffset.UtcNow
            };

            _db.ConfigProfiles.Add(copy);
            await _db.SaveChangesAsync(ct);
            return ListProfiles.Handler.ToDto(copy);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/{id:guid}/copy", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            try { return Results.Ok(await sender.Send(new Command(id), ct)); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ConfigProfiles.Copy)
        .WithSummary("Copy a config profile");
}
