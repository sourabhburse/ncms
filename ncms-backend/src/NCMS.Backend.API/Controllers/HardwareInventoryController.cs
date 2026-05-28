using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NCMS.Backend.Core.Dtos;
using NCMS.Backend.Core.Entities;
using NCMS.Backend.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NCMS.Backend.API.Controllers;

[ApiController]
[Route("api/v1/hardware-inventory")]
public sealed class HardwareInventoryController : ControllerBase
{
    private readonly NcmsDbContext _dbContext;

    public HardwareInventoryController(NcmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? productId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.HardwareInventory.AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(h => h.TenantId == tenantId.Value);
        }

        if (productId.HasValue)
        {
            query = query.Where(h => h.ProductId == productId.Value);
        }

        var inventory = await query
            .Select(h => new HardwareInventoryResponse(
                h.Id, h.TenantId, h.ProductId, h.SerialNumber, h.Status,
                h.IdentityPolicy, h.IdentityClaims, h.ImportedAt))
            .ToListAsync(cancellationToken);

        return Ok(inventory);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var h = await _dbContext.HardwareInventory.FindAsync(new object[] { id }, cancellationToken);
        if (h is null) return NotFound();

        return Ok(new HardwareInventoryResponse(
            h.Id, h.TenantId, h.ProductId, h.SerialNumber, h.Status,
            h.IdentityPolicy, h.IdentityClaims, h.ImportedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHardwareInventoryRequest request, CancellationToken cancellationToken)
    {
        if (await _dbContext.HardwareInventory.AnyAsync(h => h.SerialNumber.ToLower() == request.SerialNumber.ToLower(), cancellationToken))
        {
            return Conflict(new { error = "Hardware with this serial number is already registered." });
        }

        if (!await _dbContext.Tenants.AnyAsync(t => t.Id == request.TenantId, cancellationToken))
        {
            return BadRequest(new { error = "Specified tenant does not exist." });
        }

        if (!await _dbContext.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken))
        {
            return BadRequest(new { error = "Specified product does not exist." });
        }

        var h = new HardwareInventory
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ProductId = request.ProductId,
            SerialNumber = request.SerialNumber,
            Status = "INACTIVE",
            IdentityPolicy = request.IdentityPolicy,
            IdentityClaims = request.IdentityClaims ?? new()
        };

        _dbContext.HardwareInventory.Add(h);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new HardwareInventoryResponse(
            h.Id, h.TenantId, h.ProductId, h.SerialNumber, h.Status,
            h.IdentityPolicy, h.IdentityClaims, h.ImportedAt);

        return CreatedAtAction(nameof(GetById), new { id = h.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHardwareInventoryRequest request, CancellationToken cancellationToken)
    {
        var h = await _dbContext.HardwareInventory.FindAsync(new object[] { id }, cancellationToken);
        if (h is null) return NotFound();

        h.Status = request.Status;
        h.IdentityPolicy = request.IdentityPolicy;
        h.IdentityClaims = request.IdentityClaims ?? new();

        _dbContext.HardwareInventory.Update(h);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var h = await _dbContext.HardwareInventory.FindAsync(new object[] { id }, cancellationToken);
        if (h is null) return NotFound();

        _dbContext.HardwareInventory.Remove(h);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
