using System;
using System.Collections.Generic;

namespace NCMS.Backend.Core.Dtos;

public record DeviceResponse(
    Guid Id,
    Guid HardwareInventoryId,
    string? SerialNumber,
    Guid TenantId,
    string? Name,
    string Status,
    DateTime? LastSeenAt,
    string? CurrentFirmwareVersion,
    string? CurrentAgentVersion,
    string? WanIpAddress,
    Dictionary<string, string> MacAddresses,
    decimal? Latitude,
    decimal? Longitude,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateDeviceRequest(
    Guid HardwareInventoryId,
    Guid TenantId,
    string? Name,
    string Status,
    string? Notes
);

public record UpdateDeviceRequest(
    string? Name,
    string Status,
    decimal? Latitude,
    decimal? Longitude,
    string? Notes
);
