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

namespace NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;

public static class UpdateProfile
{
    public sealed record Command(Guid Id, string ProfileName, Guid ProductId, string? Remark)
        : IRequest<ConfigProfileDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ProfileName).NotEmpty().MaximumLength(256);
            RuleFor(x => x.ProductId).NotEmpty().WithMessage("A product model is required.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, ConfigProfileDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<ConfigProfileDto> Handle(Command cmd, CancellationToken ct)
        {
            var profile = await _db.ConfigProfiles.FindAsync([cmd.Id], ct)
                ?? throw new KeyNotFoundException($"Config profile {cmd.Id} not found.");

            if (profile.Status == ProfileStatus.Enable)
                throw new InvalidOperationException("Disable the profile before editing it.");

            var product = await _db.Products
                .FirstOrDefaultAsync(p => p.Id == cmd.ProductId && p.SupportsConfiguration && !p.IsDeleted, ct)
                ?? throw new InvalidOperationException(
                    "The selected product model does not exist or does not support configuration.");

            profile.ProfileName = cmd.ProfileName.Trim();
            profile.ProductId = product.Id;
            profile.Product = product;
            profile.Remark = cmd.Remark;

            await _db.SaveChangesAsync(ct);
            return ListProfiles.Handler.ToDto(profile);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPut("/{id:guid}", async (Guid id, CreateConfigProfileRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(new Command(id, req.ProfileName, req.ProductId, req.Remark), ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.ConfigProfiles.Edit)
        .WithSummary("Update a config profile");
}
