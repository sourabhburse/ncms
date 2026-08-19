namespace NCMS.IoT.MqttClient;

/// <summary>
/// Publishes a raw byte payload to an MQTT topic via the shared backend client.
/// Backed by the same <see cref="MQTTnet.Extensions.ManagedClient.IManagedMqttClient"/>
/// that the <see cref="Workers.MqttIngestionWorker"/> uses for subscriptions.
/// </summary>
public interface IMqttPublisher
{
    Task PublishAsync(string topic, byte[] payload, CancellationToken ct = default);
}
