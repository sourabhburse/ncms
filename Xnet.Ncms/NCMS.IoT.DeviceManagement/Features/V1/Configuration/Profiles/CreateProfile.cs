using FluentValidation;
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

public static class CreateProfile
{
    public sealed record Command(string ProfileName, Guid ProductId, string? Remark)
        : IRequest<ConfigProfileDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ProfileName).NotEmpty().MaximumLength(256);
            RuleFor(x => x.ProductId).NotEmpty();
        }
    }

    public sealed class Handler : IRequestHandler<Command, ConfigProfileDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ConfigProfileDto> Handle(Command cmd, CancellationToken ct)
        {
            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.Id == cmd.ProductId && p.SupportsConfiguration && !p.IsDeleted, ct)
                ?? throw new InvalidOperationException(
                    "The selected product model does not exist or does not support configuration.");

            var profile = new ConfigProfile
            {
                Id = Guid.NewGuid(),
                ProfileName = cmd.ProfileName.Trim(),
                ProductId = product.Id,
                Product = product,
                Status = ProfileStatus.Enable,
                Remark = cmd.Remark,
                CreatedBy = "system",
                UploadedAt = DateTimeOffset.UtcNow
            };

            _db.ConfigProfiles.Add(profile);
            await _db.SaveChangesAsync(ct);
            return ListProfiles.Handler.ToDto(profile);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateConfigProfileRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(new Command(req.ProfileName, req.ProductId, req.Remark), ct);
                return Results.Created($"/api/v1/config/profiles/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthorization(DeviceManagementPermissions.ConfigProfiles.Add)
        .WithSummary("Create a new config profile");
}
