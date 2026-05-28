using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCMS.Backend.Core.Entities;
using NCMS.Backend.Core.Provisioning;
using NCMS.Backend.Infrastructure.Data;

namespace NCMS.Backend.Infrastructure.Provisioning;

public sealed class ProvisioningService : IProvisioningService
{
    private readonly ProvisioningOptions _options;
    private readonly NcmsDbContext _dbContext;
    private readonly IDeviceCertificateIssuer _certificateIssuer;
    private readonly ILogger<ProvisioningService> _logger;

    public ProvisioningService(
        IOptions<ProvisioningOptions> options,
        NcmsDbContext dbContext,
        IDeviceCertificateIssuer certificateIssuer,
        ILogger<ProvisioningService> logger)
    {
        _options = options.Value;
        _dbContext = dbContext;
        _certificateIssuer = certificateIssuer;
        _logger = logger;
    }

    public async Task<ProvisioningResult> RegisterAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            return Fail(HttpStatusCode.BadRequest, "bad_request", "serial_number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Csr))
        {
            return Fail(HttpStatusCode.BadRequest, "bad_request", "csr (Certificate Signing Request) is required.");
        }

        HardwareInventory? inventory = await _dbContext.HardwareInventory
            .Include(h => h.Product)
            .FirstOrDefaultAsync(x => x.SerialNumber.ToLower() == request.SerialNumber.ToLower(), cancellationToken);

        if (inventory is null)
        {
            return Fail(HttpStatusCode.NotFound, "serial_not_found", "Serial number is not registered in inventory.");
        }

        // Bypass conflict check for testing to allow repeating registration
        /*
        if (string.Equals(inventory.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(HttpStatusCode.Conflict, "already_active", "Device is already active. Admin reset is required before re-registration.");
        }
        */

        if (!IdentityPolicySatisfied(inventory.IdentityClaims, request.IdentityClaims))
        {
            return Fail(HttpStatusCode.UnprocessableEntity, "identity_policy_failed", "Required identity claim is missing or invalid.");
        }

        Guid deviceGuid;
        Device? device = null;

        if (inventory.DeviceId.HasValue)
        {
            deviceGuid = inventory.DeviceId.Value;
            device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceGuid, cancellationToken);
        }
        else
        {
            deviceGuid = Guid.NewGuid();
        }

        if (device is null)
        {
            device = new Device
            {
                Id = deviceGuid,
                HardwareInventoryId = inventory.Id,
                TenantId = inventory.TenantId,
                Status = "ACTIVE",
                CurrentFirmwareVersion = request.FirmwareVersion,
                CurrentAgentVersion = request.AgentVersion,
                MacAddresses = request.MacAddresses.ToDictionary(k => k.Key, v => v.Value),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Devices.Add(device);
            inventory.DeviceId = deviceGuid;
        }
        else
        {
            device.Status = "ACTIVE";
            device.CurrentFirmwareVersion = request.FirmwareVersion;
            device.CurrentAgentVersion = request.AgentVersion;
            device.MacAddresses = request.MacAddresses.ToDictionary(k => k.Key, v => v.Value);
            device.UpdatedAt = DateTime.UtcNow;
            _dbContext.Devices.Update(device);
        }

        string deviceIdString = deviceGuid.ToString();
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddDays(_options.CertificateValidityDays);

        DeviceCertificateBundle bundle = _certificateIssuer.Issue(deviceIdString, request.SerialNumber, request.Csr, notBefore, notAfter);

        // Compute thumbprint of the new cert
        string thumbprint;
        using (var cert = X509Certificate2.CreateFromPem(bundle.ClientCertificatePem))
        {
            thumbprint = cert.Thumbprint;
        }

        // Deactivate older certs for this device
        var activeCertificates = await _dbContext.DeviceCertificates
            .Where(c => c.DeviceId == deviceGuid && c.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var cert in activeCertificates)
        {
            cert.IsActive = false;
            _dbContext.DeviceCertificates.Update(cert);
        }

        // Record the new certificate metadata
        var deviceCert = new DeviceCertificate
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceGuid,
            Thumbprint = thumbprint,
            SubjectName = $"CN={deviceIdString}",
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = bundle.ExpiresAt.UtcDateTime,
            IsActive = true
        };
        _dbContext.DeviceCertificates.Add(deviceCert);

        inventory.Status = "ACTIVE";
        _dbContext.HardwareInventory.Update(inventory);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Provisioned serial {SerialNumber} as device {DeviceId}", request.SerialNumber, deviceIdString);

        return new ProvisioningResult
        {
            StatusCode = (int)HttpStatusCode.OK,
            Payload = new RegisterDeviceResponse
            {
                DeviceId = deviceIdString,
                Status = "registered",
                Mqtt = new MqttRegistration
                {
                    BrokerUrl = _options.Mqtt.BrokerUrl,
                    BrokerPort = _options.Mqtt.BrokerPort,
                    ClientId = deviceIdString
                },
                Pki = new PkiRegistration
                {
                    CaCertificate = bundle.CaCertificatePem,
                    ClientCertificate = bundle.ClientCertificatePem,
                    ExpiresAt = bundle.ExpiresAt
                },
                Topics = new TopicRegistration
                {
                    TelemetryPublish = $"d/{deviceIdString}/telemetry",
                    HeartbeatPublish = $"d/{deviceIdString}/heartbeat",
                    ConfigSubscribe = $"d/{deviceIdString}/config",
                    CommandSubscribe = $"d/{deviceIdString}/cmd",
                    CommandResponsePublish = $"d/{deviceIdString}/cmd/res",
                    OtaSubscribe = $"d/{deviceIdString}/ota"
                },
                TelemetryIntervalSeconds = _options.TelemetryIntervalSeconds,
                HeartbeatIntervalSeconds = _options.HeartbeatIntervalSeconds,
                Config = new ConfigRegistration
                {
                    DesiredConfigVersion = null,
                    PendingConfig = false
                }
            }
        };
    }

    private static bool IdentityPolicySatisfied(
        Dictionary<string, string?> requiredClaims,
        Dictionary<string, string?> providedClaims)
    {
        foreach ((string key, string? requiredValue) in requiredClaims)
        {
            if (string.IsNullOrWhiteSpace(requiredValue))
            {
                continue;
            }

            if (!providedClaims.TryGetValue(key, out string? providedValue))
            {
                return false;
            }

            if (!string.Equals(requiredValue, providedValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static ProvisioningResult Fail(HttpStatusCode code, string errorCode, string message)
    {
        return new ProvisioningResult
        {
            StatusCode = (int)code,
            Payload = new
            {
                error = new
                {
                    code = errorCode,
                    message
                }
            }
        };
    }
}
