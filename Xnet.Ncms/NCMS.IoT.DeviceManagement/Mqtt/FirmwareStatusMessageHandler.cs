using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.MqttClient.Handlers;

namespace NCMS.IoT.DeviceManagement.Mqtt;

public sealed class FirmwareStatusMessageHandler : IMqttMessageHandler
{
    // Devices report firmware-upgrade status on d/{deviceId}/ota/res.
    public string TopicSuffix => "ota/res";

    private readonly DeviceManagementDbContext _db;
    private readonly ILogger<FirmwareStatusMessageHandler> _logger;

    public FirmwareStatusMessageHandler(DeviceManagementDbContext db, ILogger<FirmwareStatusMessageHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task HandleAsync(Guid deviceId, string topic, string payloadJson, byte qosLevel, CancellationToken ct)
    {
        DeviceOtaStatusPayload? status;
        try { status = JsonSerializer.Deserialize<DeviceOtaStatusPayload>(payloadJson); }
        catch
        {
            _logger.LogWarning("Could not deserialize OTA status payload from device {DeviceId}: {Payload}", deviceId, payloadJson);
            return;
        }

        if (status is null) return;

        var taskDevice = await _db.UpgradeTaskDevices
            .Include(d => d.UpgradeTask)
            .Where(d => d.DeviceId == deviceId
                && d.Status == UpgradeDeviceStatus.InProgress)
            .OrderByDescending(d => d.DispatchedAt)
            .FirstOrDefaultAsync(ct);

        if (taskDevice is null)
        {
            _logger.LogWarning("OTA status '{Status}' for device {DeviceId}: no in-progress task device entry found", status.Status, deviceId);
            return;
        }

        _logger.LogInformation("Device {DeviceId} task-device {Id}: received OTA status '{Status}'",
            deviceId, taskDevice.Id, status.Status);

        switch (status.Status.ToLowerInvariant())
        {
            case "acknowledged":
                taskDevice.MarkAcknowledged();
                break;

            case "downloading":
            case "applying":
            case "rebooting":
                // still in progress — heartbeat only, no state transition
                break;

            case "success":
                taskDevice.MarkSucceeded();
                break;

            case "failed":
                taskDevice.MarkFailed(
                    status.ErrorMessage ?? "Device reported failure.",
                    status.ErrorCode);
                break;

            default:
                _logger.LogWarning("Unknown OTA status '{Status}' from device {DeviceId}", status.Status, deviceId);
                return;
        }

        // A device transition may have completed the task — keep task status in sync.
        await TaskReconciliation.ReconcileUpgradeTaskAsync(_db, taskDevice.UpgradeTaskId, ct);

        await _db.SaveChangesAsync(ct);
    }

    private sealed class DeviceOtaStatusPayload
    {
        [JsonPropertyName("status")]        public string  Status       { get; set; } = string.Empty;
        [JsonPropertyName("error_code")]    public string? ErrorCode    { get; set; }
        [JsonPropertyName("error_message")] public string? ErrorMessage { get; set; }
    }
}
