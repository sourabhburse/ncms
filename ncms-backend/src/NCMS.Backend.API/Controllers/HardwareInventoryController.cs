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

    [HttpPost("batch")]
    public async Task<IActionResult> CreateBatch([FromBody] CreateHardwareInventoryBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { error = "At least one hardware inventory item is required." });
        }

        var duplicateSerials = request.Items
            .GroupBy(item => item.SerialNumber.ToLower())
            .Where(group => group.Count() > 1)
            .Select(group => group.First().SerialNumber)
            .ToList();

        if (duplicateSerials.Count > 0)
        {
            return Conflict(new { error = "Batch contains duplicate serial numbers.", serial_numbers = duplicateSerials });
        }

        var serialNumbers = request.Items.Select(item => item.SerialNumber.ToLower()).ToList();
        var existingSerialNumbers = await _dbContext.HardwareInventory
            .Where(h => serialNumbers.Contains(h.SerialNumber.ToLower()))
            .Select(h => h.SerialNumber)
            .ToListAsync(cancellationToken);

        if (existingSerialNumbers.Count > 0)
        {
            return Conflict(new { error = "One or more serial numbers are already registered.", serial_numbers = existingSerialNumbers });
        }

        var tenantIds = request.Items.Select(item => item.TenantId).Distinct().ToList();
        var existingTenantIds = await _dbContext.Tenants
            .Where(tenant => tenantIds.Contains(tenant.Id))
            .Select(tenant => tenant.Id)
            .ToListAsync(cancellationToken);
        var missingTenantIds = tenantIds.Except(existingTenantIds).ToList();

        if (missingTenantIds.Count > 0)
        {
            return BadRequest(new { error = "One or more specified tenants do not exist.", tenant_ids = missingTenantIds });
        }

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var existingProductIds = await _dbContext.Products
            .Where(product => productIds.Contains(product.Id))
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);
        var missingProductIds = productIds.Except(existingProductIds).ToList();

        if (missingProductIds.Count > 0)
        {
            return BadRequest(new { error = "One or more specified products do not exist.", product_ids = missingProductIds });
        }

        var hardwareInventory = request.Items
            .Select(item => new HardwareInventory
            {
                Id = Guid.NewGuid(),
                TenantId = item.TenantId,
                ProductId = item.ProductId,
                SerialNumber = item.SerialNumber,
                Status = "INACTIVE",
                IdentityPolicy = item.IdentityPolicy,
                IdentityClaims = item.IdentityClaims ?? new()
            })
            .ToList();

        _dbContext.HardwareInventory.AddRange(hardwareInventory);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var responseItems = hardwareInventory
            .Select(h => new HardwareInventoryResponse(
                h.Id, h.TenantId, h.ProductId, h.SerialNumber, h.Status,
                h.IdentityPolicy, h.IdentityClaims, h.ImportedAt))
            .ToList();

        return CreatedAtAction(nameof(GetAll), new HardwareInventoryBatchResponse(responseItems.Count, responseItems));
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

    [HttpDelete("batch")]
    public async Task<IActionResult> DeleteBatch([FromBody] DeleteHardwareInventoryBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest(new { error = "At least one hardware inventory ID is required." });
        }

        var requestedIds = request.Ids
            .Distinct()
            .ToList();

        var hardwareInventory = await _dbContext.HardwareInventory
            .Where(h => requestedIds.Contains(h.Id))
            .ToListAsync(cancellationToken);

        var foundIds = hardwareInventory
            .Select(h => h.Id)
            .ToList();

        var missingIds = requestedIds
            .Except(foundIds)
            .ToList();

        if (missingIds.Count > 0)
        {
            return NotFound(new { error = "One or more hardware inventory IDs were not found.", ids = missingIds });
        }

        _dbContext.HardwareInventory.RemoveRange(hardwareInventory);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new HardwareInventoryDeleteBatchResponse(foundIds.Count, foundIds));
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
