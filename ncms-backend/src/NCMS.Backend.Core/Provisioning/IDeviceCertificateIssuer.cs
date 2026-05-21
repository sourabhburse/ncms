namespace NCMS.Backend.Core.Provisioning;

public interface IDeviceCertificateIssuer
{
    DeviceCertificateBundle Issue(
        string deviceId,
        string serialNumber,
        string csrPem,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter
    );
}

public sealed class DeviceCertificateBundle
{
    public required string CaCertificatePem { get; init; }
    public required string ClientCertificatePem { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}
