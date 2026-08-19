using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Contracts.Services;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;
using NCMS.IoT.MqttClient.Handlers;

namespace NCMS.IoT.DeviceManagement.Mqtt;

internal sealed class DeviceEventMessageHandler : IMqttMessageHandler
{
    public string TopicSuffix => "events";

    private readonly DeviceManagementDbContext _db;
    private readonly IDevicePresenceService _presenceService;
    private readonly ILogger<DeviceEventMessageHandler> _logger;

    public DeviceEventMessageHandler(
        DeviceManagementDbContext db,
        IDevicePresenceService presenceService,
        ILogger<DeviceEventMessageHandler> logger)
    {
        _db = db;
        _presenceService = presenceService;
        _logger = logger;
    }

    public async Task HandleAsync(Guid deviceId, string topic, string payloadJson, byte qosLevel, CancellationToken ct)
    {
        string eventType = "UNKNOWN";
        string? severity = null;

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);

            if (doc.RootElement.TryGetProperty("event_type", out var et))
                eventType = et.GetString() ?? eventType;

            if (doc.RootElement.TryGetProperty("severity", out var sev))
                severity = sev.GetString();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Could not parse event payload from DeviceId={DeviceId}; storing with UNKNOWN type.",
                deviceId);
        }

        // Presence refresh doubles as a device-existence check; device_events FKs to devices, so
        // inserting for an unknown device would throw a 23503 FK violation — drop it with a warning.
        var seen = await _presenceService.UpdateLastSeenAsync(deviceId, ct);
        if (seen == 0)
        {
            _logger.LogWarning(
                "Event from unknown DeviceId={DeviceId}; dropping record. " +
                "The device is not provisioned in this database (stale credentials or DB out of sync).",
                deviceId);
            return;
        }

        _db.DeviceEvents.Add(new DeviceEvent
        {
            DeviceId = deviceId,
            Timestamp = DateTimeOffset.UtcNow,
            EventType = eventType,
            Severity = severity,
            PayloadJson = payloadJson
        });
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Device event persisted. DeviceId={DeviceId}, EventType={EventType}, Severity={Severity}",
            deviceId, eventType, severity);
    }
}
