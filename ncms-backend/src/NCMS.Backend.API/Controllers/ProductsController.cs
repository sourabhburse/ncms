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
[Route("api/v1/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly NcmsDbContext _dbContext;

    public ProductsController(NcmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? vendorId, CancellationToken cancellationToken)
    {
        var query = _dbContext.Products.AsQueryable();

        if (vendorId.HasValue)
        {
            query = query.Where(p => p.VendorId == vendorId.Value);
        }

        var products = await query
            .Select(p => new ProductResponse(
                p.Id, p.VendorId, p.Vendor != null ? p.Vendor.Name : null, p.ModelName,
                p.Architecture, p.ConfigFormat, p.ConfigSchemaVersion, p.SupportedIdentityPolicies, p.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .Include(p => p.Vendor)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null) return NotFound();

        return Ok(new ProductResponse(
            product.Id, product.VendorId, product.Vendor != null ? product.Vendor.Name : null, product.ModelName,
            product.Architecture, product.ConfigFormat, product.ConfigSchemaVersion, product.SupportedIdentityPolicies, product.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Vendors.AnyAsync(v => v.Id == request.VendorId, cancellationToken))
        {
            return BadRequest(new { error = "Specified vendor does not exist." });
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            VendorId = request.VendorId,
            ModelName = request.ModelName,
            Architecture = request.Architecture,
            ConfigFormat = request.ConfigFormat,
            ConfigSchemaVersion = request.ConfigSchemaVersion,
            SupportedIdentityPolicies = request.SupportedIdentityPolicies ?? new()
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var vendor = await _dbContext.Vendors.FindAsync(new object[] { product.VendorId }, cancellationToken);

        var response = new ProductResponse(
            product.Id, product.VendorId, vendor?.Name, product.ModelName,
            product.Architecture, product.ConfigFormat, product.ConfigSchemaVersion, product.SupportedIdentityPolicies, product.CreatedAt);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product is null) return NotFound();

        product.ModelName = request.ModelName;
        product.Architecture = request.Architecture;
        product.ConfigFormat = request.ConfigFormat;
        product.ConfigSchemaVersion = request.ConfigSchemaVersion;
        product.SupportedIdentityPolicies = request.SupportedIdentityPolicies ?? new();

        _dbContext.Products.Update(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product is null) return NotFound();

        _dbContext.Products.Remove(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
