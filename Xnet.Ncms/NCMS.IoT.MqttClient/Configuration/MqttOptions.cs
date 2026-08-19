using System.ComponentModel.DataAnnotations;

namespace NCMS.IoT.MqttClient.Configuration;

/// <summary>
/// Bound from configuration section "Mqtt".
/// </summary>
public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    [Required]
    public required string BrokerHost { get; set; }

    public int BrokerPort { get; set; } = 8884;

    /// <summary>Must match the Mosquitto ACL user entry "user dotnet-backend".</summary>
    public string ClientId { get; set; } = "ncms-client";
    public string Username { get; set; } = "dotnet-backend";

    public int KeepAliveSeconds { get; set; } = 60;

    /// <summary>
    /// MQTT 5 session expiry interval sent in CONNECT. 0 = no persistent session.
    /// Also embedded in the provisioning response so devices use the same value.
    /// </summary>
    public int SessionExpirySeconds { get; set; } = 300;

    public int ReconnectDelaySeconds { get; set; } = 5;
    public int MaxPendingMessages { get; set; } = 1000;

    /// <summary>
    /// When true, the MQTT client accepts any server certificate without chain validation.
    /// NEVER set this to true in production — it opens the connection to MITM attacks.
    /// Use only in development when Mosquitto has a self-signed cert not issued by the NCMS Root CA.
    /// </summary>
    public bool AllowUntrustedServerCertificate { get; set; } = false;

    /// <summary>
    /// MQTT topic subscriptions for the backend ingestion worker.
    /// Each entry must have a corresponding Mosquitto ACL "topic read" grant for the backend user:
    ///   user dotnet-backend
    ///   topic read d/+/telemetry
    ///   topic read d/+/heartbeat
    ///   topic read d/+/events
    ///   topic read d/+/ota/res
    /// </summary>
    public DeviceTopics Topics { get; set; } = new();
}

public sealed class DeviceTopics
{
    /// <summary>Sensor / measurement payloads from devices.</summary>
    public string Telemetry { get; set; } = "d/+/telemetry";

    /// <summary>
    /// Heartbeat / online-state messages from devices (d/{deviceId}/heartbeat). Must match the
    /// HeartbeatPublish topic handed to devices at provisioning and DeviceStatusMessageHandler's
    /// TopicSuffix ("heartbeat").
    /// </summary>
    public string Heartbeat { get; set; } = "d/+/heartbeat";

    /// <summary>Fault codes, alerts, and discrete state-change events.</summary>
    public string Events { get; set; } = "d/+/events";

    /// <summary>OTA firmware upgrade status reports from devices (d/{deviceId}/ota/res).</summary>
    public string OtaStatus { get; set; } = "d/+/ota/res";

    public string AppStatus { get; set; } = "d/+/application/res";

    /// <summary>All configured topic patterns as an enumerable for bulk subscription.</summary>
    public IEnumerable<string> All() { yield return Telemetry; yield return Heartbeat; yield return Events; yield return OtaStatus; yield return AppStatus; }
}
