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
[Route("api/v1/devices")]
public sealed class DevicesController : ControllerBase
{
    private readonly NcmsDbContext _dbContext;

    public DevicesController(NcmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? tenantId, [FromQuery] bool includeTelemetry = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Devices.AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(d => d.TenantId == tenantId.Value);
        }

        if (includeTelemetry)
        {
            var devicesWithTelemetry = await query
                .Select(d => new DeviceWithLatestTelemetryResponse(
                    d.Id,
                    d.HardwareInventoryId,
                    d.HardwareInventory != null ? d.HardwareInventory.SerialNumber : null,
                    d.TenantId,
                    d.Name,
                    d.Status,
                    d.LastSeenAt,
                    d.CurrentFirmwareVersion,
                    d.CurrentAgentVersion,
                    d.WanIpAddress,
                    d.MacAddresses,
                    d.Latitude,
                    d.Longitude,
                    d.Notes,
                    d.Telemetries
                        .OrderByDescending(t => t.Timestamp)
                        .Select(t => new DeviceTelemetryResponse(
                            t.Id,
                            t.DeviceId,
                            t.Timestamp,
                            t.CpuUsagePercent,
                            t.RamUsageMb,
                            t.RamTotalMb,
                            t.StorageUsedMb,
                            t.StorageTotalMb,
                            t.UptimeSeconds,
                            t.WanIp,
                            t.TemperatureCelsius,
                            t.SignalStrengthRssi,
                            t.SignalQualityRsrp))
                        .FirstOrDefault(),
                    d.CreatedAt,
                    d.UpdatedAt))
                .ToListAsync(cancellationToken);

            return Ok(devicesWithTelemetry);
        }

        var devices = await query
            .Select(d => new DeviceResponse(
                d.Id, d.HardwareInventoryId, d.HardwareInventory != null ? d.HardwareInventory.SerialNumber : null, d.TenantId, d.Name, d.Status, d.LastSeenAt,
                d.CurrentFirmwareVersion, d.CurrentAgentVersion, d.WanIpAddress, d.MacAddresses,
                d.Latitude, d.Longitude, d.Notes, d.CreatedAt, d.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(devices);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var d = await _dbContext.Devices
            .Include(d => d.HardwareInventory)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
            
        if (d is null) return NotFound();

        return Ok(new DeviceResponse(
            d.Id, d.HardwareInventoryId, d.HardwareInventory != null ? d.HardwareInventory.SerialNumber : null, d.TenantId, d.Name, d.Status, d.LastSeenAt,
            d.CurrentFirmwareVersion, d.CurrentAgentVersion, d.WanIpAddress, d.MacAddresses,
            d.Latitude, d.Longitude, d.Notes, d.CreatedAt, d.UpdatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Tenants.AnyAsync(t => t.Id == request.TenantId, cancellationToken))
        {
            return BadRequest(new { error = "Specified tenant does not exist." });
        }

        var hw = await _dbContext.HardwareInventory.FindAsync(new object[] { request.HardwareInventoryId }, cancellationToken);
        if (hw is null)
        {
            return BadRequest(new { error = "Specified hardware inventory item does not exist." });
        }

        if (await _dbContext.Devices.AnyAsync(d => d.HardwareInventoryId == request.HardwareInventoryId, cancellationToken))
        {
            return Conflict(new { error = "An active device registration already exists for this hardware inventory item." });
        }

        var d = new Device
        {
            Id = Guid.NewGuid(),
            HardwareInventoryId = request.HardwareInventoryId,
            TenantId = request.TenantId,
            Name = request.Name,
            Status = request.Status ?? "PROVISIONING",
            Notes = request.Notes
        };

        _dbContext.Devices.Add(d);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new DeviceResponse(
            d.Id, d.HardwareInventoryId, hw.SerialNumber, d.TenantId, d.Name, d.Status, d.LastSeenAt,
            d.CurrentFirmwareVersion, d.CurrentAgentVersion, d.WanIpAddress, d.MacAddresses,
            d.Latitude, d.Longitude, d.Notes, d.CreatedAt, d.UpdatedAt);

        return CreatedAtAction(nameof(GetById), new { id = d.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDeviceRequest request, CancellationToken cancellationToken)
    {
        var d = await _dbContext.Devices.FindAsync(new object[] { id }, cancellationToken);
        if (d is null) return NotFound();

        d.Name = request.Name;
        d.Status = request.Status;
        d.Latitude = request.Latitude;
        d.Longitude = request.Longitude;
        d.Notes = request.Notes;
        d.UpdatedAt = DateTime.UtcNow;

        _dbContext.Devices.Update(d);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var d = await _dbContext.Devices.FindAsync(new object[] { id }, cancellationToken);
        if (d is null) return NotFound();

        _dbContext.Devices.Remove(d);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
