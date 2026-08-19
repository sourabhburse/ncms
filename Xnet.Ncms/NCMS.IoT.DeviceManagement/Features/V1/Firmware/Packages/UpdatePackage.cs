using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;

public static class UpdatePackage
{
    public sealed record Command(
        Guid Id,
        string Name,
        string Version,
        string? Remark,
        Guid ProductId
    ) : IRequest<FirmwareDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Version).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ProductId).NotEmpty().WithMessage("A product model is required.");
        }
    }

    public sealed class Handler : IRequestHandler<Command, FirmwareDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<FirmwareDto> Handle(Command cmd, CancellationToken ct)
        {
            var pkg = await _db.Firmwares
                .Include(f => f.SupportedProducts)
                .FirstOrDefaultAsync(f => f.Id == cmd.Id, ct)
                ?? throw new KeyNotFoundException($"Firmware package {cmd.Id} not found.");

            if (pkg.IsEnabled)
                throw new InvalidOperationException("Disable the package before editing it.");

            var productEligible = await _db.Products
                .AnyAsync(p => p.Id == cmd.ProductId && p.SupportsFirmwareUpgrade && !p.IsDeleted, ct);
            if (!productEligible)
                throw new InvalidOperationException(
                    "The selected product model does not exist or does not support firmware upgrade.");

            pkg.Name = cmd.Name;
            pkg.Version = cmd.Version;
            pkg.Description = cmd.Remark;

            // Single product per package — replace the existing compatibility link.
            if (!(pkg.SupportedProducts.Count == 1 && pkg.SupportedProducts.First().ProductId == cmd.ProductId))
            {
                pkg.SupportedProducts.Clear();
                pkg.SupportedProducts.Add(new FirmwareProduct { FirmwareId = pkg.Id, ProductId = cmd.ProductId });
            }

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
        group.MapPut("/{id:guid}", async (Guid id, UpdatePackageRequest req, ISender sender, CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(new Command(id, req.Name, req.Version, req.Remark, req.ProductId), ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        })
        .RequireAuthorization(DeviceManagementPermissions.Packages.Edit)
        .WithSummary("Update a firmware package");
}

public sealed record UpdatePackageRequest(string Name, string Version, string? Remark, Guid ProductId);
