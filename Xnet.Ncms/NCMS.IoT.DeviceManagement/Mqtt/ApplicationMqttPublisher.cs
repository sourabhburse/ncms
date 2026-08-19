using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCMS.IoT.DeviceManagement.Contracts.Mqtt;
using NCMS.IoT.MqttClient;

namespace NCMS.IoT.DeviceManagement.Mqtt;

public sealed class ApplicationMqttPublisher : IApplicationMqttPublisher
{
    private readonly IMqttPublisher _publisher;
    private readonly ILogger<ApplicationMqttPublisher> _logger;

    public ApplicationMqttPublisher(IMqttPublisher publisher, ILogger<ApplicationMqttPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishApplicationCommandAsync(Guid deviceId, IApplicationCommand command, CancellationToken ct = default)
    {
        var topic = $"d/{deviceId}/application";
        // Serialize by the command's runtime type (ApplicationInstallCommand vs
        // ApplicationRemoveCommand) — serializing through the IApplicationCommand interface
        // type would otherwise emit `{}` since neither record's members are visible on it.
        var payload = JsonSerializer.SerializeToUtf8Bytes(command, command.GetType());

        await _publisher.PublishAsync(topic, payload, ct);
        _logger.LogInformation("Published application command for device {DeviceId} on topic {Topic}", deviceId, topic);
    }
}
