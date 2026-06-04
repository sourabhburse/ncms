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

    [HttpGet("template")]
    public async Task<IActionResult> GetTemplate([FromQuery] Guid productId, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null)
        {
            return NotFound(new { error = "Product not found." });
        }

        var distinctKeys = product.SupportedIdentityPolicies
            .SelectMany(p => p.RequiredClaimKeys)
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        var headers = new System.Collections.Generic.List<string> { "SerialNumber", "IdentityPolicy" };
        foreach (var key in distinctKeys)
        {
            headers.Add($"Claim_{key}");
        }

        var csvBuilder = new System.Text.StringBuilder();
        csvBuilder.AppendLine(string.Join(",", headers));

        // Generate sample rows for each supported policy
        foreach (var policy in product.SupportedIdentityPolicies)
        {
            var rowValues = new System.Collections.Generic.List<string> { $"SN-{product.ModelName}-001", policy.PolicyName };
            foreach (var key in distinctKeys)
            {
                if (policy.RequiredClaimKeys.Contains(key))
                {
                    rowValues.Add($"[value_for_{key}]");
                }
                else
                {
                    rowValues.Add(string.Empty);
                }
            }
            csvBuilder.AppendLine(string.Join(",", rowValues));
        }

        var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvBuilder.ToString());
        var fileName = $"hardware_inventory_template_{product.ModelName.ToLower().Replace(" ", "_")}.csv";
        return File(csvBytes, "text/csv", fileName);
    }

    [HttpPost("import-csv")]
    public async Task<IActionResult> ImportCsv(
        [FromForm] ImportHardwareInventoryCsvRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Request is required." });
        }

        var file = request.File;
        var productId = request.ProductId;
        var tenantId = request.TenantId;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "CSV file is required." });
        }

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return BadRequest(new { error = "Product not found." });
        }

        var tenantExists = await _dbContext.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!tenantExists)
        {
            return BadRequest(new { error = "Tenant not found." });
        }

        var items = new System.Collections.Generic.List<HardwareInventory>();
        var errors = new System.Collections.Generic.List<string>();

        using (var reader = new System.IO.StreamReader(file.OpenReadStream()))
        {
            var headerLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return BadRequest(new { error = "CSV file is empty or missing headers." });
            }

            var headers = headerLine.Split(',').Select(h => h.Trim()).ToList();
            var serialIndex = headers.IndexOf("SerialNumber");
            var policyIndex = headers.IndexOf("IdentityPolicy");

            if (serialIndex == -1 || policyIndex == -1)
            {
                return BadRequest(new { error = "CSV is missing required headers: SerialNumber, IdentityPolicy." });
            }

            var claimColumns = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].StartsWith("Claim_"))
                {
                    var claimKey = headers[i].Substring(6); // Remove "Claim_"
                    claimColumns[claimKey] = i;
                }
            }

            int rowNum = 1;
            var batchSerials = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                rowNum++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = line.Split(',').Select(f => f.Trim()).ToArray();
                if (fields.Length <= Math.Max(serialIndex, policyIndex))
                {
                    errors.Add($"Line {rowNum}: Insufficient columns.");
                    continue;
                }

                var serial = fields[serialIndex];
                var policyName = fields[policyIndex];

                if (string.IsNullOrWhiteSpace(serial))
                {
                    errors.Add($"Line {rowNum}: SerialNumber is missing.");
                    continue;
                }

                if (batchSerials.Contains(serial))
                {
                    errors.Add($"Line {rowNum}: Duplicate SerialNumber '{serial}' in batch.");
                    continue;
                }
                batchSerials.Add(serial);

                var matchingPolicy = product.SupportedIdentityPolicies
                    .FirstOrDefault(p => string.Equals(p.PolicyName, policyName, StringComparison.OrdinalIgnoreCase));

                if (matchingPolicy is null)
                {
                    errors.Add($"Line {rowNum}: Unsupported IdentityPolicy '{policyName}' for product '{product.ModelName}'.");
                    continue;
                }

                var claims = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                bool claimError = false;

                foreach (var requiredKey in matchingPolicy.RequiredClaimKeys)
                {
                    if (!claimColumns.TryGetValue(requiredKey, out int colIndex) || 
                        colIndex >= fields.Length || 
                        string.IsNullOrWhiteSpace(fields[colIndex]))
                    {
                        errors.Add($"Line {rowNum}: Missing required claim '{requiredKey}' for policy '{policyName}'.");
                        claimError = true;
                        break;
                    }

                    claims[requiredKey] = fields[colIndex];
                }

                if (claimError) continue;

                items.Add(new HardwareInventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = productId,
                    SerialNumber = serial,
                    IdentityPolicy = matchingPolicy.PolicyName,
                    IdentityClaims = claims,
                    Status = "PENDING_ACTIVATION",
                    ImportedAt = DateTime.UtcNow
                });
            }
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { error = "CSV validation failed.", details = errors });
        }

        if (items.Count == 0)
        {
            return BadRequest(new { error = "No valid hardware inventory items found in CSV." });
        }

        // Check database duplicates
        var serialsToCheck = items.Select(i => i.SerialNumber.ToLower()).ToList();
        var existingSerials = await _dbContext.HardwareInventory
            .Where(h => serialsToCheck.Contains(h.SerialNumber.ToLower()))
            .Select(h => h.SerialNumber)
            .ToListAsync(cancellationToken);

        if (existingSerials.Count > 0)
        {
            return Conflict(new { error = "One or more serial numbers are already registered.", serial_numbers = existingSerials });
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.HardwareInventory.AddRange(items);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return StatusCode(500, new { error = "Failed to import hardware inventory batch.", details = ex.Message });
        }

        return Ok(new { message = "Import successful", count = items.Count });
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

public class ImportHardwareInventoryCsvRequest
{
    public Microsoft.AspNetCore.Http.IFormFile File { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
}
