using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.IoT.DeviceManagement.Contracts.Services;
using NCMS.IoT.MqttClient.Handlers;

namespace NCMS.IoT.DeviceManagement.Mqtt;

internal sealed class TelemetryMessageHandler : IMqttMessageHandler
{
    public string TopicSuffix => "telemetry";

    private readonly DeviceManagementDbContext _db;
    private readonly IDevicePresenceService _presenceService;
    private readonly ILogger<TelemetryMessageHandler> _logger;

    public TelemetryMessageHandler(
        DeviceManagementDbContext db,
        IDevicePresenceService presenceService,
        ILogger<TelemetryMessageHandler> logger)
    {
        _db = db;
        _presenceService = presenceService;
        _logger = logger;
    }

    public async Task HandleAsync(Guid deviceId, string topic, string payloadJson, byte qosLevel, CancellationToken ct)
    {
        // Refresh presence first; the affected-row count doubles as a device-existence check.
        // telemetry_records FKs to devices, so inserting for an unprovisioned/unknown device would
        // throw a 23503 FK violation (and stall the worker) — drop it with a warning instead.
        var seen = await _presenceService.UpdateLastSeenAsync(deviceId, ct);
        if (seen == 0)
        {
            _logger.LogWarning(
                "Telemetry from unknown DeviceId={DeviceId}; dropping record. " +
                "The device is not provisioned in this database (stale credentials or DB out of sync).",
                deviceId);
            return;
        }

        _db.TelemetryRecords.Add(new TelemetryRecord
        {
            DeviceId = deviceId,
            Timestamp = DateTimeOffset.UtcNow,
            PayloadJson = payloadJson,
            Topic = topic,
            QosLevel = qosLevel
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Telemetry persisted. DeviceId={DeviceId}, PayloadLength={Len}",
            deviceId, payloadJson.Length);
    }
}
