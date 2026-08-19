using System.ComponentModel.DataAnnotations;

namespace NCMS.IoT.DeviceManagement.Configuration;

public sealed class ZtpOptions
{
    public const string SectionName = "Ztp";

    [Required]
    public string CertRenewalUrl { get; set; } = string.Empty;

    public int TelemetryIntervalSeconds { get; set; } = 60;
    public int StatusIntervalSeconds { get; set; } = 30;
    public int CertRenewalThresholdDays { get; set; } = 90;

    public ZtpBrokerOptions Broker { get; set; } = new();
}

public sealed class ZtpBrokerOptions
{
    [Required]
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 8884;
    public int KeepAliveSeconds { get; set; } = 60;
    public int SessionExpirySeconds { get; set; } = 300;
}
