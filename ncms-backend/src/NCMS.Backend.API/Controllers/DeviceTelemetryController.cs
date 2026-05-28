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
[Route("api/v1/device-telemetry")]
public sealed class DeviceTelemetryController : ControllerBase
{
    private readonly NcmsDbContext _dbContext;

    public DeviceTelemetryController(NcmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? deviceId,
        [FromQuery] string? serialNumber,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DeviceTelemetries.AsQueryable();

        if (deviceId.HasValue)
        {
            query = query.Where(t => t.DeviceId == deviceId.Value);
        }

        if (!string.IsNullOrEmpty(serialNumber))
        {
            query = query.Where(t => t.Device != null && t.Device.HardwareInventory != null && t.Device.HardwareInventory.SerialNumber == serialNumber);
        }

        var telemetry = await query
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .Select(t => new DeviceTelemetryResponse(
                t.Id, t.DeviceId, t.Timestamp, t.CpuUsagePercent, t.RamUsageMb, t.RamTotalMb,
                t.StorageUsedMb, t.StorageTotalMb, t.UptimeSeconds, t.WanIp, t.TemperatureCelsius,
                t.SignalStrengthRssi, t.SignalQualityRsrp))
            .ToListAsync(cancellationToken);

        return Ok(telemetry);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var t = await _dbContext.DeviceTelemetries.FindAsync(new object[] { id }, cancellationToken);
        if (t is null) return NotFound();

        return Ok(new DeviceTelemetryResponse(
            t.Id, t.DeviceId, t.Timestamp, t.CpuUsagePercent, t.RamUsageMb, t.RamTotalMb,
            t.StorageUsedMb, t.StorageTotalMb, t.UptimeSeconds, t.WanIp, t.TemperatureCelsius,
            t.SignalStrengthRssi, t.SignalQualityRsrp));
    }
}
