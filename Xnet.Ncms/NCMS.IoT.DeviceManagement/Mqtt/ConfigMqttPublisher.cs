using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Contracts.Mqtt;
using NCMS.IoT.MqttClient;

namespace NCMS.IoT.DeviceManagement.Mqtt;

public sealed class ConfigMqttPublisher : IConfigMqttPublisher
{
    private readonly IMqttPublisher _publisher;
    private readonly ILogger<ConfigMqttPublisher> _logger;

    public ConfigMqttPublisher(IMqttPublisher publisher, ILogger<ConfigMqttPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishConfigCommandAsync(Guid deviceId, ConfigCommandMessage command, CancellationToken ct = default)
    {
        var topic = $"d/{deviceId}/config";

        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            config_url   = command.ConfigUrl,
            size         = command.Size,
            md5          = command.Md5,
            commandId    = command.CommandId,
            taskDeviceId = command.TaskDeviceId,
            taskId       = command.TaskId,
            profileName  = command.ProfileName,
            expiresAt    = command.ExpiresAt
        });

        await _publisher.PublishAsync(topic, payload, ct);
        _logger.LogInformation("Published config command for device {DeviceId}, task {TaskId}", deviceId, command.TaskId);
    }
}
