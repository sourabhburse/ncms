using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NCMS.Backend.Core.Dtos;
using NCMS.Backend.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NCMS.Backend.API.Controllers;

[ApiController]
[Route("api/v1/device-certificates")]
public sealed class DeviceCertificatesController : ControllerBase
{
    private readonly NcmsDbContext _dbContext;

    public DeviceCertificatesController(NcmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? deviceId, CancellationToken cancellationToken)
    {
        var query = _dbContext.DeviceCertificates.AsQueryable();

        if (deviceId.HasValue)
        {
            query = query.Where(c => c.DeviceId == deviceId.Value);
        }

        var certs = await query
            .Select(c => new DeviceCertificateResponse(
                c.Id, c.DeviceId, c.Thumbprint, c.SubjectName, c.ExpiresAt, c.IsActive, c.IssuedAt))
            .ToListAsync(cancellationToken);

        return Ok(certs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var c = await _dbContext.DeviceCertificates.FindAsync(new object[] { id }, cancellationToken);
        if (c is null) return NotFound();

        return Ok(new DeviceCertificateResponse(
            c.Id, c.DeviceId, c.Thumbprint, c.SubjectName, c.ExpiresAt, c.IsActive, c.IssuedAt));
    }
}
