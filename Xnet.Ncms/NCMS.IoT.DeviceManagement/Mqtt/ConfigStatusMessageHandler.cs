using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.MqttClient.Handlers;

namespace NCMS.IoT.DeviceManagement.Mqtt;

/// <summary>
/// Handles device configuration status reports on d/{deviceId}/config/res.
/// Mirrors <c>FirmwareStatusMessageHandler</c>.
/// </summary>
public sealed class ConfigStatusMessageHandler : IMqttMessageHandler
{
    public string TopicSuffix => "config/res";

    private readonly DeviceManagementDbContext _db;
    private readonly ILogger<ConfigStatusMessageHandler> _logger;

    public ConfigStatusMessageHandler(DeviceManagementDbContext db, ILogger<ConfigStatusMessageHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task HandleAsync(Guid deviceId, string topic, string payloadJson, byte qosLevel, CancellationToken ct)
    {
        DeviceConfigStatusPayload? status;
        try { status = JsonSerializer.Deserialize<DeviceConfigStatusPayload>(payloadJson); }
        catch
        {
            _logger.LogWarning("Could not deserialize config status payload from device {DeviceId}: {Payload}", deviceId, payloadJson);
            return;
        }

        if (status is null) return;

        var taskDevice = await _db.ConfigureTaskDevices
            .Where(d => d.DeviceId == deviceId && d.Status == DeviceConfigStatus.ConfigInProgress)
            .OrderByDescending(d => d.DispatchedAt)
            .FirstOrDefaultAsync(ct);

        if (taskDevice is null)
        {
            _logger.LogWarning("Config status '{Status}' for device {DeviceId}: no in-progress task device entry found", status.Status, deviceId);
            return;
        }

        _logger.LogInformation("Device {DeviceId} config-device {Id}: received config status '{Status}'",
            deviceId, taskDevice.Id, status.Status);

        switch (status.Status.ToLowerInvariant())
        {
            case "acknowledged":
                taskDevice.MarkAcknowledged();
                break;

            case "applying":
            case "rebooting":
                // still in progress — heartbeat only, no state transition
                break;

            case "success":
            case "complete":
                taskDevice.MarkComplete();
                break;

            case "failed":
                taskDevice.MarkFailed(status.ErrorMessage ?? "Device reported config failure.", status.ErrorCode);
                break;

            default:
                _logger.LogWarning("Unknown config status '{Status}' from device {DeviceId}", status.Status, deviceId);
                return;
        }

        // A device transition may have completed the task — keep task status in sync.
        await TaskReconciliation.ReconcileConfigureTaskAsync(_db, taskDevice.ConfigureTaskId, ct);

        await _db.SaveChangesAsync(ct);
    }

    private sealed class DeviceConfigStatusPayload
    {
        [JsonPropertyName("status")]        public string  Status       { get; set; } = string.Empty;
        [JsonPropertyName("error_code")]    public string? ErrorCode    { get; set; }
        [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    }
}
