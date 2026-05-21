namespace NCMS.Backend.Infrastructure.Provisioning;

public sealed class ProvisioningOptions
{
    public List<AllowedInventoryItem> AllowedInventory { get; init; } = [];
    public MqttOptions Mqtt { get; init; } = new();
    public string CertificateAuthorityDirectory { get; init; } = string.Empty;
    public int TelemetryIntervalSeconds { get; init; } = 60;
    public int HeartbeatIntervalSeconds { get; init; } = 30;
    public int CertificateValidityDays { get; init; } = 365;
}

public sealed class AllowedInventoryItem
{
    public string SerialNumber { get; init; } = string.Empty;
    public string Status { get; set; } = "PENDING_ACTIVATION";
    public Dictionary<string, string?> IdentityClaims { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MqttOptions
{
    public string BrokerUrl { get; init; } = "mqtt.xnet-iot.com";
    public int BrokerPort { get; init; } = 8883;
}
