using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;
using FirmwareEntity = NCMS.IoT.DeviceManagement.Entities.Firmware;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

public static class CreatePackage
{
    public sealed record Command(
        string Version,
        string Name,
        string? Description,
        string ReleaseNotes,
        string? DeviceTypeCode,
        string? MinRequiredFirmwareVersion,
        IReadOnlyList<Guid>? ProductIds = null
    ) : IRequest<FirmwareDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.DeviceTypeCode).MaximumLength(100);
        }
    }

    public sealed class Handler : IRequestHandler<Command, FirmwareDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<FirmwareDto> Handle(Command cmd, CancellationToken ct)
        {
            var pkg = new FirmwareEntity
            {
                Id = Guid.NewGuid(),
                Version = cmd.Version,
                Name = cmd.Name,
                Description = cmd.Description,
                ReleaseNotes = cmd.ReleaseNotes,
                DeviceTypeCode = cmd.DeviceTypeCode ?? string.Empty,
                MinRequiredFirmwareVersion = cmd.MinRequiredFirmwareVersion,
                CreatedBy = "system",
                UploadedAt = DateTimeOffset.UtcNow
            };

            // Resolve the selected product models. Only products that exist and are
            // flagged SupportsFirmwareUpgrade may be linked — this is the source of
            // truth for upgrade eligibility (see FirmwareProduct).
            var productIds = (cmd.ProductIds ?? []).Distinct().ToList();
            if (productIds.Count > 0)
            {
                var eligible = await _db.Products
                    .Where(p => productIds.Contains(p.Id) && p.SupportsFirmwareUpgrade && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToListAsync(ct);

                if (eligible.Count != productIds.Count)
                    throw new InvalidOperationException(
                        "One or more selected product models do not exist or do not support firmware upgrade.");

                foreach (var productId in eligible)
                    pkg.SupportedProducts.Add(new FirmwareProduct { FirmwareId = pkg.Id, ProductId = productId });
            }

            _db.Firmwares.Add(pkg);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
            {
                throw new InvalidOperationException(
                    $"A firmware package named '{cmd.Name}' with version '{cmd.Version}' already exists.");
            }
            return ListPackages.Handler.ToDto(pkg);
        }
    }

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/", async (CreateFirmwareRequest req, ISender sender, CancellationToken ct) =>
        {
            var command = new Command(req.Version, req.Name, req.Description, req.ReleaseNotes,
                req.DeviceTypeCode, req.MinRequiredFirmwareVersion);
            try
            {
                var result = await sender.Send(command, ct);
                return Results.Created($"/api/v1/firmware/packages/{result.Id}", result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthorization(DeviceManagementPermissions.Packages.Add)
        .WithSummary("Create a new firmware package");
}
