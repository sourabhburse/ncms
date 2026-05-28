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
[Route("api/v1/vendors")]
public sealed class VendorsController : ControllerBase
{
    private readonly NcmsDbContext _dbContext;

    public VendorsController(NcmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var vendors = await _dbContext.Vendors
            .Select(v => new VendorResponse(v.Id, v.Name, v.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(vendors);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var vendor = await _dbContext.Vendors.FindAsync(new object[] { id }, cancellationToken);
        if (vendor is null) return NotFound();

        return Ok(new VendorResponse(vendor.Id, vendor.Name, vendor.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVendorRequest request, CancellationToken cancellationToken)
    {
        var vendor = new Vendor
        {
            Id = Guid.NewGuid(),
            Name = request.Name
        };

        _dbContext.Vendors.Add(vendor);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new VendorResponse(vendor.Id, vendor.Name, vendor.CreatedAt);
        return CreatedAtAction(nameof(GetById), new { id = vendor.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVendorRequest request, CancellationToken cancellationToken)
    {
        var vendor = await _dbContext.Vendors.FindAsync(new object[] { id }, cancellationToken);
        if (vendor is null) return NotFound();

        vendor.Name = request.Name;

        _dbContext.Vendors.Update(vendor);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var vendor = await _dbContext.Vendors.FindAsync(new object[] { id }, cancellationToken);
        if (vendor is null) return NotFound();

        _dbContext.Vendors.Remove(vendor);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
