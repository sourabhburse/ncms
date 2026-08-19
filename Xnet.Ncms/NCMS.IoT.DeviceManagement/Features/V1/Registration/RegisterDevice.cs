using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NCMS.IoT.Core.Domain.Exceptions;
using NCMS.IoT.Core.Domain.Interfaces;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.Features.V1.Registration;

public static class RegisterDevice
{
    // ── Command ───────────────────────────────────────────────────────────────

    public sealed record Command(
        string SerialNumber,
        IdentityClaimsDto IdentityClaims,
        string Csr,
        string? FirmwareVersion,
        string? AgentVersion,
        string? HardwareModel
    ) : IRequest<RegisterDeviceResponse>;

    // ── Validator ─────────────────────────────────────────────────────────────

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.SerialNumber).NotEmpty().MaximumLength(128);
            RuleFor(x => x.Csr).NotEmpty();
            RuleFor(x => x.IdentityClaims).NotNull();
            RuleFor(x => x.IdentityClaims.BaseMac)
                .Matches(@"^([0-9A-Fa-f]{2}[:\-]?){5}[0-9A-Fa-f]{2}$")
                .WithMessage("Enter a valid MAC address (e.g. AA:BB:CC:DD:EE:FF)")
                .When(x => !string.IsNullOrWhiteSpace(x.IdentityClaims.BaseMac));
        }
    }

    // ── Handler ───────────────────────────────────────────────────────────────

    public sealed class Handler : IRequestHandler<Command, RegisterDeviceResponse>
    {
        private readonly DeviceManagementDbContext _db;
        private readonly IDeviceCertificateIssuer _certIssuer;
        private readonly ZtpOptions _opts;
        private readonly ILogger<Handler> _logger;

        public Handler(
            DeviceManagementDbContext db,
            IDeviceCertificateIssuer certIssuer,
            IOptions<ZtpOptions> opts,
            ILogger<Handler> logger)
        {
            _db = db;
            _certIssuer = certIssuer;
            _opts = opts.Value;
            _logger = logger;
        }

        public async ValueTask<RegisterDeviceResponse> Handle(Command req, CancellationToken ct)
        {
            var hardware = await _db.HardwareInventory
                .FirstOrDefaultAsync(h => h.SerialNumber == req.SerialNumber, ct);

            if (hardware is null)
            {
                _logger.LogWarning(
                    "ZTP: Serial number not found in inventory. SerialNumber={SerialNumber}",
                    req.SerialNumber);
                throw new InvalidBootstrapTokenException("Hardware identity claims could not be verified.");
            }

            ValidateIdentityClaims(req, hardware);

            _logger.LogInformation(
                "ZTP: Identity claims verified. SerialNumber={SerialNumber}, IsProvisioned={IsProvisioned}",
                req.SerialNumber, hardware.IsProvisioned);

            if (hardware.IsProvisioned)
            {
                var existing = await _db.Devices
                    .Where(d => d.HardwareInventoryId == hardware.Id)
                    .Select(d => new
                    {
                        Device = d,
                        Certificate = d.Certificates
                            .Where(c => c.IsActive)
                            .OrderByDescending(c => c.IssuedAt)
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync(ct);

                if (existing is not null && existing.Certificate is not null)
                {
                    if (!_certIssuer.PublicKeysMatch(req.Csr, existing.Certificate.CertificatePem))
                    {
                        _logger.LogWarning(
                            "ZTP: Key mismatch on idempotent re-provision. DeviceId={DeviceId}, SerialNumber={SerialNumber}",
                            existing.Device.Id, req.SerialNumber);
                        throw new DeviceKeyMismatchException(req.SerialNumber);
                    }

                    _logger.LogInformation(
                        "ZTP: Idempotent re-provisioning. DeviceId={DeviceId}, SerialNumber={SerialNumber}",
                        existing.Device.Id, req.SerialNumber);
                    return BuildResponseFromExisting(existing.Device, existing.Certificate);
                }

                throw new DeviceAlreadyProvisionedException(req.SerialNumber);
            }

            var deviceId = Guid.NewGuid();

            _logger.LogInformation(
                "ZTP: Assigning DeviceId={DeviceId} to SerialNumber={SerialNumber}",
                deviceId, req.SerialNumber);

            var certResult = await _certIssuer.IssueAsync(base64Csr: req.Csr, commonName: deviceId.ToString());

            var provisionedAt = DateTimeOffset.UtcNow;
            var strategy = _db.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async (cancellationToken) =>
                {
                    await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        _db.Devices.Add(new Device
                        {
                            Id = deviceId,
                            HardwareInventoryId = hardware.Id,
                            ActivatedAt = provisionedAt,
                            FirmwareVersion = req.FirmwareVersion,
                            AgentVersion = req.AgentVersion,
                            HardwareModel = req.HardwareModel,
                            Status = "ONLINE",
                            IsOnline = true,
                            // Baseline for the presence reaper: the grace window starts at
                            // provisioning so a just-registered device isn't swept offline before
                            // its first heartbeat arrives.
                            LastSeenAt = provisionedAt,
                            MacAddresses = string.IsNullOrWhiteSpace(req.IdentityClaims.BaseMac)
                                ? new Dictionary<string, string>()
                                : new Dictionary<string, string>
                                {
                                    ["eth0"] = NormalizeMac(req.IdentityClaims.BaseMac)
                                },
                            CreatedAt = provisionedAt,
                            UpdatedAt = provisionedAt
                        });

                        _db.DeviceCertificates.Add(new DeviceCertificate
                        {
                            DeviceId = deviceId,
                            Thumbprint = certResult.Thumbprint,
                            CertificatePem = certResult.CertificatePem,
                            SubjectName = certResult.SubjectName,
                            IssuedAt = certResult.IssuedAt,
                            ExpiresAt = certResult.ExpiresAt,
                            IsActive = true
                        });

                        hardware.IsProvisioned = true;
                        hardware.Status = "ACTIVE";
                        hardware.UpdatedAt = provisionedAt;

                        await _db.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);
                    }
                    catch
                    {
                        await tx.RollbackAsync(cancellationToken);
                        throw;
                    }
                }, ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException is NpgsqlException { SqlState: "23505" }
                      && ex.InnerException.Message.Contains("IX_devices_HardwareInventoryId", StringComparison.Ordinal))
            {
                throw new DeviceAlreadyProvisionedException(req.SerialNumber);
            }

            return BuildResponse(deviceId, provisionedAt, certResult);
        }

        private RegisterDeviceResponse BuildResponseFromExisting(Device device, DeviceCertificate cert) =>
            BuildResponse(device.Id, _certIssuer.GetRootCaPem(), cert.CertificatePem, cert.ExpiresAt);

        private RegisterDeviceResponse BuildResponse(
            Guid deviceId,
            DateTimeOffset provisionedAt,
            Core.Pki.Models.CertificateSigningResult certResult) =>
            BuildResponse(deviceId, certResult.RootCaPem, certResult.CertificatePem, certResult.ExpiresAt);

        private RegisterDeviceResponse BuildResponse(
            Guid deviceId,
            string caCertificatePem,
            string clientCertificatePem,
            DateTimeOffset certExpiresAt)
        {
            var broker = _opts.Broker;
            var topicRoot = $"d/{deviceId}";
            return new RegisterDeviceResponse
            {
                DeviceId = deviceId,
                Mqtt = new MqttRegistration
                {
                    BrokerUrl = broker.Host,
                    BrokerPort = broker.Port,
                    ClientId = deviceId.ToString()
                },
                Pki = new PkiRegistration
                {
                    CaCertificate = caCertificatePem,
                    ClientCertificate = clientCertificatePem,
                    ExpiresAt = certExpiresAt
                },
                Topics = new DeviceTopicsRegistration
                {
                    TelemetryPublish        = $"{topicRoot}/telemetry",
                    HeartbeatPublish        = $"{topicRoot}/heartbeat",
                    ConfigSubscribe         = $"{topicRoot}/config",
                    CommandSubscribe        = $"{topicRoot}/cmd",
                    CommandResponsePublish  = $"{topicRoot}/cmd/res",
                    OtaSubscribe            = $"{topicRoot}/ota",
                    OtaPublish              = $"{topicRoot}/ota/res",
                    AppSubscribe            = $"{topicRoot}/application",
                    AppPublish              = $"{topicRoot}/application/res"
                },
                TelemetryIntervalSeconds = _opts.TelemetryIntervalSeconds,
                HeartbeatIntervalSeconds = _opts.StatusIntervalSeconds,
                Config = new DeviceConfigRegistration
                {
                    DesiredConfigVersion = null,
                    PendingConfig = false
                }
            };
        }

        private void ValidateIdentityClaims(Command req, HardwareInventory hardware)
        {
            const string genericError = "Hardware identity claims could not be verified.";

            var storedBaseMac = hardware.IdentityClaims
                .FirstOrDefault(kvp => string.Equals(kvp.Key, "base_mac", StringComparison.OrdinalIgnoreCase)).Value;

            if (string.IsNullOrWhiteSpace(storedBaseMac))
                return;

            var claimedMac = NormalizeMac(req.IdentityClaims.BaseMac);
            var expectedMac = NormalizeMac(storedBaseMac);

            if (!string.Equals(claimedMac, expectedMac, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "ZTP: MAC mismatch. SerialNumber={SerialNumber}, Claimed={Claimed}, Stored={Stored}",
                    req.SerialNumber, claimedMac, expectedMac);
                throw new InvalidBootstrapTokenException(genericError);
            }
        }

        private static string NormalizeMac(string? mac) =>
            string.IsNullOrWhiteSpace(mac)
                ? string.Empty
                : mac.Replace(":", "").Replace("-", "").ToUpperInvariant().Trim();
    }

    // ── Endpoint ──────────────────────────────────────────────────────────────

    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/provision", async (
            RegisterDeviceRequest request,
            ISender sender,
            ILogger<Handler> logger,
            CancellationToken ct) =>
        {
            logger.LogInformation(
                "Provisioning request received. SerialNumber={SerialNumber}, HardwareModel={Model}, AgentVersion={Agent}",
                request.SerialNumber, request.HardwareModel, request.AgentVersion);

            var command = new Command(
                request.SerialNumber,
                request.IdentityClaims,
                request.Csr,
                request.FirmwareVersion,
                request.AgentVersion,
                request.HardwareModel);

            var response = await sender.Send(command, ct);

            logger.LogInformation(
                "Provisioning successful. DeviceId={DeviceId}, TelemetryTopic={TelemetryTopic}, ExpiresAt={ExpiresAt}",
                response.DeviceId, response.Topics.TelemetryPublish, response.Pki.ExpiresAt);

            return Results.Created($"api/v1/provision/{response.DeviceId}", response);
        })
        .WithSummary("Register a factory device and receive mTLS certificate + MQTT credentials")
        .AllowAnonymous();
}
