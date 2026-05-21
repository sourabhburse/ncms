using System.Text.Json.Serialization;

namespace NCMS.Backend.Core.Provisioning;

public sealed class RegisterDeviceResponse
{
    [JsonPropertyName("device_id")]
    public required string DeviceId { get; init; }
    [JsonPropertyName("status")]
    public required string Status { get; init; }
    [JsonPropertyName("mqtt")]
    public required MqttRegistration Mqtt { get; init; }
    [JsonPropertyName("pki")]
    public required PkiRegistration Pki { get; init; }
    [JsonPropertyName("topics")]
    public required TopicRegistration Topics { get; init; }
    [JsonPropertyName("telemetry_interval_seconds")]
    public int TelemetryIntervalSeconds { get; init; }
    [JsonPropertyName("heartbeat_interval_seconds")]
    public int HeartbeatIntervalSeconds { get; init; }
    [JsonPropertyName("config")]
    public required ConfigRegistration Config { get; init; }
}

public sealed class MqttRegistration
{
    [JsonPropertyName("broker_url")]
    public required string BrokerUrl { get; init; }
    [JsonPropertyName("broker_port")]
    public int BrokerPort { get; init; }
    [JsonPropertyName("client_id")]
    public required string ClientId { get; init; }
}

public sealed class PkiRegistration
{
    [JsonPropertyName("ca_certificate")]
    public required string CaCertificate { get; init; }
    [JsonPropertyName("client_certificate")]
    public required string ClientCertificate { get; init; }
    [JsonPropertyName("expires_at")]
    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed class TopicRegistration
{
    [JsonPropertyName("telemetry_publish")]
    public required string TelemetryPublish { get; init; }
    [JsonPropertyName("heartbeat_publish")]
    public required string HeartbeatPublish { get; init; }
    [JsonPropertyName("config_subscribe")]
    public required string ConfigSubscribe { get; init; }
    [JsonPropertyName("command_subscribe")]
    public required string CommandSubscribe { get; init; }
    [JsonPropertyName("command_response_publish")]
    public required string CommandResponsePublish { get; init; }
    [JsonPropertyName("ota_subscribe")]
    public required string OtaSubscribe { get; init; }
}

public sealed class ConfigRegistration
{
    [JsonPropertyName("desired_config_version")]
    public int? DesiredConfigVersion { get; init; }
    [JsonPropertyName("pending_config")]
    public bool PendingConfig { get; init; }
}
