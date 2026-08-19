namespace NCMS.IoT.MqttClient.Handlers;

/// <summary>
/// Processes a single decoded MQTT message for one topic suffix.
/// Implementations are resolved from DI as scoped services per message.
/// </summary>
public interface IMqttMessageHandler
{
    /// <summary>
    /// The topic suffix this handler owns (e.g., "telemetry", "status", "events").
    /// The worker matches the last segment of the incoming topic against this value.
    /// </summary>
    string TopicSuffix { get; }

    /// <summary>
    /// Processes the message. Any exception is caught by the worker — never propagate
    /// a transient error to the MQTT layer or it will stop message delivery.
    /// </summary>
    Task HandleAsync(
        Guid deviceId,
        string topic,
        string payloadJson,
        byte qosLevel,
        CancellationToken ct);
}
