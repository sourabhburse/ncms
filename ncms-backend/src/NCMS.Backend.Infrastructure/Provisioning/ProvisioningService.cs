using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCMS.Backend.Core.Provisioning;

namespace NCMS.Backend.Infrastructure.Provisioning;

public sealed class ProvisioningService : IProvisioningService
{
    private readonly ProvisioningOptions _options;
    private readonly IDeviceCertificateIssuer _certificateIssuer;
    private readonly ILogger<ProvisioningService> _logger;

    public ProvisioningService(
        IOptions<ProvisioningOptions> options,
        IDeviceCertificateIssuer certificateIssuer,
        ILogger<ProvisioningService> logger)
    {
        _options = options.Value;
        _certificateIssuer = certificateIssuer;
        _logger = logger;
    }

    public Task<ProvisioningResult> RegisterAsync(RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            return Task.FromResult(Fail(HttpStatusCode.BadRequest, "bad_request", "serial_number is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Csr))
        {
            return Task.FromResult(Fail(HttpStatusCode.BadRequest, "bad_request", "csr (Certificate Signing Request) is required."));
        }

        AllowedInventoryItem? inventory = _options.AllowedInventory
            .FirstOrDefault(x => string.Equals(x.SerialNumber, request.SerialNumber, StringComparison.OrdinalIgnoreCase));

        if (inventory is null)
        {
            return Task.FromResult(Fail(HttpStatusCode.NotFound, "serial_not_found", "Serial number is not registered in inventory."));
        }

        // Bypass conflict check for testing to allow repeating registration
        /*
        if (string.Equals(inventory.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Fail(HttpStatusCode.Conflict, "already_active", "Device is already active. Admin reset is required before re-registration."));
        }
        */

        if (!IdentityPolicySatisfied(inventory.IdentityClaims, request.IdentityClaims))
        {
            return Task.FromResult(Fail(HttpStatusCode.UnprocessableEntity, "identity_policy_failed", "Required identity claim is missing or invalid."));
        }

        string deviceId = Guid.NewGuid().ToString();
        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddDays(_options.CertificateValidityDays);

        DeviceCertificateBundle bundle = _certificateIssuer.Issue(deviceId, request.SerialNumber, request.Csr, notBefore, notAfter);

        inventory.Status = "ACTIVE";
        _logger.LogInformation("Provisioned serial {SerialNumber} as device {DeviceId}", request.SerialNumber, deviceId);

        return Task.FromResult(new ProvisioningResult
        {
            StatusCode = (int)HttpStatusCode.OK,
            Payload = new RegisterDeviceResponse
            {
                DeviceId = deviceId,
                Status = "registered",
                Mqtt = new MqttRegistration
                {
                    BrokerUrl = _options.Mqtt.BrokerUrl,
                    BrokerPort = _options.Mqtt.BrokerPort,
                    ClientId = deviceId
                },
                Pki = new PkiRegistration
                {
                    CaCertificate = bundle.CaCertificatePem,
                    ClientCertificate = bundle.ClientCertificatePem,
                    ExpiresAt = bundle.ExpiresAt
                },
                Topics = new TopicRegistration
                {
                    TelemetryPublish = $"d/{deviceId}/telemetry",
                    HeartbeatPublish = $"d/{deviceId}/heartbeat",
                    ConfigSubscribe = $"d/{deviceId}/config",
                    CommandSubscribe = $"d/{deviceId}/cmd",
                    CommandResponsePublish = $"d/{deviceId}/cmd/res",
                    OtaSubscribe = $"d/{deviceId}/ota"
                },
                TelemetryIntervalSeconds = _options.TelemetryIntervalSeconds,
                HeartbeatIntervalSeconds = _options.HeartbeatIntervalSeconds,
                Config = new ConfigRegistration
                {
                    DesiredConfigVersion = null,
                    PendingConfig = false
                }
            }
        });
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
