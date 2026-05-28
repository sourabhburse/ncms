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
[Route("api/v1/tenants")]
public sealed class TenantsController : ControllerBase
{
    private readonly NcmsDbContext _dbContext;

    public TenantsController(NcmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenants = await _dbContext.Tenants
            .Select(t => new TenantResponse(t.Id, t.Name, t.Slug, t.ContactEmail, t.IsActive, t.CreatedAt, t.UpdatedAt))
            .ToListAsync(cancellationToken);
        return Ok(tenants);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants.FindAsync(new object[] { id }, cancellationToken);
        if (tenant is null) return NotFound();

        return Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.ContactEmail, tenant.IsActive, tenant.CreatedAt, tenant.UpdatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (await _dbContext.Tenants.AnyAsync(t => t.Slug.ToLower() == request.Slug.ToLower(), cancellationToken))
        {
            return Conflict(new { error = "Tenant with this slug already exists." });
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug.ToLowerInvariant(),
            ContactEmail = request.ContactEmail,
            IsActive = true
        };

        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.ContactEmail, tenant.IsActive, tenant.CreatedAt, tenant.UpdatedAt);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants.FindAsync(new object[] { id }, cancellationToken);
        if (tenant is null) return NotFound();

        tenant.Name = request.Name;
        tenant.ContactEmail = request.ContactEmail;
        tenant.IsActive = request.IsActive;
        tenant.UpdatedAt = DateTime.UtcNow;

        _dbContext.Tenants.Update(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants.FindAsync(new object[] { id }, cancellationToken);
        if (tenant is null) return NotFound();

        _dbContext.Tenants.Remove(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
