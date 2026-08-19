using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Contracts.Services;
using NCMS.IoT.MqttClient.Handlers;

namespace NCMS.IoT.DeviceManagement.Mqtt;

/// <summary>
/// Processes device heartbeat messages on d/{deviceId}/heartbeat. Each heartbeat refreshes the
/// device's presence: IsOnline is taken from the payload's "status" field and LastSeenAt is set
/// to now. Absence of heartbeats is handled separately by a timeout sweep (see DevicePresence
/// reaper) — this handler only ever processes messages that DID arrive.
/// </summary>
internal sealed class DeviceStatusMessageHandler : IMqttMessageHandler
{
    public string TopicSuffix => "heartbeat";

    private readonly IDevicePresenceService _presenceService;
    private readonly ILogger<DeviceStatusMessageHandler> _logger;

    public DeviceStatusMessageHandler(
        IDevicePresenceService presenceService,
        ILogger<DeviceStatusMessageHandler> logger)
    {
        _presenceService = presenceService;
        _logger = logger;
    }

    public async Task HandleAsync(Guid deviceId, string topic, string payloadJson, byte qosLevel, CancellationToken ct)
    {
        // A heartbeat that arrived means the device is reachable, so default to online; the
        // payload's explicit "status" overrides (e.g. an LWT/graceful-shutdown "offline").
        bool isOnline = true;

        try
        {
            var hb = JsonSerializer.Deserialize<HeartbeatPayload>(payloadJson);
            if (hb is not null && !string.IsNullOrWhiteSpace(hb.Status))
                isOnline = hb.Status.Equals("online", StringComparison.OrdinalIgnoreCase);

            // The topic-derived deviceId is authoritative (it is ACL-bound to the device's cert);
            // the payload device_id is informational only — log if they disagree.
            if (hb?.DeviceId is not null
                && Guid.TryParse(hb.DeviceId, out var claimed) && claimed != deviceId)
            {
                _logger.LogWarning(
                    "Heartbeat payload device_id={Claimed} differs from topic DeviceId={DeviceId}; using topic id.",
                    claimed, deviceId);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Could not parse heartbeat payload from DeviceId={DeviceId}; treating as online.",
                deviceId);
        }

        var affected = await _presenceService.UpdatePresenceAsync(deviceId, isOnline, ct);

        if (affected == 0)
            _logger.LogWarning("Heartbeat received for unknown DeviceId={DeviceId}.", deviceId);
        else
            _logger.LogDebug(
                "Device presence updated. DeviceId={DeviceId}, IsOnline={IsOnline}",
                deviceId, isOnline);
    }

    /// <summary>Heartbeat payload published by devices on d/{deviceId}/heartbeat.</summary>
    private sealed class HeartbeatPayload
    {
        [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
        [JsonPropertyName("device_id")] public string? DeviceId { get; set; }
        [JsonPropertyName("status")]    public string? Status { get; set; }
        [JsonPropertyName("uptime")]    public long Uptime { get; set; }
    }
}
