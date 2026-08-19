using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Contracts.Mqtt;
using NCMS.IoT.MqttClient;

namespace NCMS.IoT.DeviceManagement.Mqtt;

public sealed class FirmwareMqttPublisher : IFirmwareMqttPublisher
{
    private readonly IMqttPublisher _publisher;
    private readonly ILogger<FirmwareMqttPublisher> _logger;

    public FirmwareMqttPublisher(IMqttPublisher publisher, ILogger<FirmwareMqttPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishFirmwareCommandAsync(Guid deviceId, FirmwareCommandMessage command, CancellationToken ct = default)
    {
        var topic = $"d/{deviceId}/ota";

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            package_url       = command.PackageUrl,
            size              = command.Size,
            md5               = command.Md5,
            sha256            = command.Sha256,
            commandId         = command.CommandId,
            taskDeviceId      = command.TaskDeviceId,
            taskId            = command.TaskId,
            targetVersion     = command.TargetVersion,
            rollbackOnFailure = command.RollbackOnFailure,
            expiresAt         = command.ExpiresAt
        });

        await _publisher.PublishAsync(topic, payload, ct);
        _logger.LogInformation("Published firmware command for device {DeviceId}, task {TaskId}", deviceId, command.TaskId);
    }
}
