using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.MqttClient.Handlers;

namespace NCMS.IoT.DeviceManagement.Mqtt;

/// <summary>
/// Devices report deployment status on d/{deviceId}/pkg/res. Since a task always targets
/// exactly one package now (no bundle fan-out), a status message maps directly onto the
/// device's single in-progress ApplicationTaskDevice — no per-item lookup needed.
/// </summary>
public sealed class ApplicationStatusMessageHandler : IMqttMessageHandler
{
    public string TopicSuffix => "application/res";

    private readonly DeviceManagementDbContext _db;
    private readonly ILogger<ApplicationStatusMessageHandler> _logger;

    public ApplicationStatusMessageHandler(DeviceManagementDbContext db, ILogger<ApplicationStatusMessageHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task HandleAsync(Guid deviceId, string topic, string payloadJson, byte qosLevel, CancellationToken ct)
    {
        DeviceApplicationStatusPayload? status;
        try { status = JsonSerializer.Deserialize<DeviceApplicationStatusPayload>(payloadJson); }
        catch
        {
            _logger.LogWarning("Could not deserialize application status payload from device {DeviceId}: {Payload}", deviceId, payloadJson);
            return;
        }

        if (status is null) return;

        var taskDevice = await _db.ApplicationTaskDevices
            .Include(d => d.ApplicationTask)
            .Where(d => d.DeviceId == deviceId && d.Status == ApplicationTaskDeviceStatus.InProgress)
            .OrderByDescending(d => d.DispatchedAt)
            .FirstOrDefaultAsync(ct);

        if (taskDevice is null)
        {
            _logger.LogWarning("Application status '{Status}' for device {DeviceId}: no in-progress task device entry found", status.Status, deviceId);
            return;
        }

        _logger.LogInformation("Device {DeviceId} task-device {Id}: received status '{Status}'",
            deviceId, taskDevice.Id, status.Status);

        switch (status.Status.ToLowerInvariant())
        {
            case "acknowledged":
                taskDevice.MarkAcknowledged();
                break;

            case "downloading":
            case "installing":
                // Heartbeat only — already InProgress, no transition needed.
                break;

            case "success":
                taskDevice.MarkSucceeded();
                await ApplicationInventorySync.ApplyDeviceOutcomeAsync(_db, taskDevice, taskDevice.ApplicationTask.Action, ct);
                break;

            case "failed":
                taskDevice.MarkFailed(status.ErrorMessage ?? "Device reported failure.");
                await ApplicationInventorySync.ApplyDeviceOutcomeAsync(_db, taskDevice, taskDevice.ApplicationTask.Action, ct);
                break;

            default:
                _logger.LogWarning("Unknown application status '{Status}' from device {DeviceId}", status.Status, deviceId);
                return;
        }

        // A device transition may have completed the task — reconcile it in the same unit of work.
        await TaskReconciliation.ReconcileApplicationTaskAsync(_db, taskDevice.ApplicationTaskId, ct);

        await _db.SaveChangesAsync(ct);
    }

    private sealed class DeviceApplicationStatusPayload
    {
        [JsonPropertyName("status")]         public string  Status       { get; set; } = string.Empty;
        [JsonPropertyName("error_code")]     public string? ErrorCode    { get; set; }
        [JsonPropertyName("error_message")]  public string? ErrorMessage { get; set; }
    }
}
