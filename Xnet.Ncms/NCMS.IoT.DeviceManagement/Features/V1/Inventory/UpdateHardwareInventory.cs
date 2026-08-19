using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.Features.V1.Inventory;

public static class UpdateHardwareInventory
{
    public sealed record Command(
        Guid Id,
        string SerialNumber,
        Guid ProductId,
        string IdentityPolicy,
        Dictionary<string, string?> IdentityClaims
    ) : IRequest<HardwareInventoryDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.SerialNumber)
                .NotEmpty().MaximumLength(128)
                .WithName("Serial number");
            RuleFor(x => x.ProductId)
                .NotEmpty()
                .WithName("Product");
            RuleFor(x => x.IdentityPolicy).NotEmpty().MaximumLength(100).WithName("Identity policy");
        }
    }

    public sealed class Handler : IRequestHandler<Command, HardwareInventoryDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<HardwareInventoryDto> Handle(Command cmd, CancellationToken ct)
        {
            var entry = await _db.HardwareInventory
                .Include(h => h.Product)
                .FirstOrDefaultAsync(h => h.Id == cmd.Id, ct)
                ?? throw new InvalidOperationException("Hardware entry not found.");

            var productExists = await _db.Products
                .AnyAsync(p => p.Id == cmd.ProductId, ct);
            if (!productExists)
                throw new InvalidOperationException("Selected product does not exist.");

            entry.ProductId = cmd.ProductId;
            entry.SerialNumber = cmd.SerialNumber.Trim().ToUpperInvariant();
            entry.IdentityPolicy = cmd.IdentityPolicy.Trim();
            entry.IdentityClaims = cmd.IdentityClaims;
            entry.UpdatedAt = DateTimeOffset.UtcNow;

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is PostgresException { SqlState: "23505" } pg
                    && pg.ConstraintName?.Contains("serial") == true)
            {
                throw new InvalidOperationException(
                    $"Serial number '{cmd.SerialNumber}' is already in inventory.");
            }

            var productName = await _db.Products
                .AsNoTracking()
                .Where(p => p.Id == entry.ProductId)
                .Select(p => p.Name)
                .FirstAsync(ct);

            return new HardwareInventoryDto(
                entry.Id, entry.ProductId, productName,
                entry.SerialNumber,
                entry.IsProvisioned, entry.Status, entry.IdentityPolicy, entry.IdentityClaims,
                entry.CreatedAt, entry.UpdatedAt);
        }
    }
}
