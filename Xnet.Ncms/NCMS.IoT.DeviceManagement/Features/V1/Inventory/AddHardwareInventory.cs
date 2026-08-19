using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Features.V1.Inventory;

public static class AddHardwareInventory
{
    public sealed record Command(
        string SerialNumber,
        Guid ProductId,
        string IdentityPolicy = "serial_only",
        Dictionary<string, string?>? IdentityClaims = null
    ) : IRequest<HardwareInventoryDto>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SerialNumber)
                .NotEmpty().MaximumLength(128)
                .WithName("Serial number");

            RuleFor(x => x.ProductId)
                .NotEmpty()
                .WithName("Product");
        }
    }

    public sealed class Handler : IRequestHandler<Command, HardwareInventoryDto>
    {
        private readonly DeviceManagementDbContext _db;
        public Handler(DeviceManagementDbContext db) => _db = db;

        public async ValueTask<HardwareInventoryDto> Handle(Command cmd, CancellationToken ct)
        {
            var productExists = await _db.Products
                .AnyAsync(p => p.Id == cmd.ProductId, ct);
            if (!productExists)
                throw new InvalidOperationException("Selected product does not exist.");

            var entry = new HardwareInventory
            {
                Id = Guid.NewGuid(),
                ProductId = cmd.ProductId,
                SerialNumber = cmd.SerialNumber.Trim().ToUpperInvariant(),
                IsProvisioned = false,
                Status = "PENDING_ACTIVATION",
                IdentityPolicy = string.IsNullOrWhiteSpace(cmd.IdentityPolicy) ? "serial_only" : cmd.IdentityPolicy.Trim(),
                IdentityClaims = cmd.IdentityClaims ?? new(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _db.HardwareInventory.Add(entry);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
            {
                if (pg.ConstraintName?.Contains("serial") == true)
                    throw new InvalidOperationException(
                        $"Serial number '{cmd.SerialNumber}' is already in inventory.");
                throw;
            }

            var product = await _db.Products
                .AsNoTracking()
                .Where(p => p.Id == cmd.ProductId)
                .Select(p => p.Name)
                .FirstAsync(ct);

            return new HardwareInventoryDto(
                entry.Id, entry.ProductId, product,
                entry.SerialNumber,
                entry.IsProvisioned, entry.Status, entry.IdentityPolicy, entry.IdentityClaims,
                entry.CreatedAt, entry.UpdatedAt);
        }
    }
}
