using System.Security.Cryptography.X509Certificates;

namespace NCMS.IoT.MqttClient.Bootstrapping;

/// <summary>
/// In-memory singleton store for the backend's mTLS client certificate.
/// The certificate is generated at worker startup if absent or nearing expiry.
/// </summary>
public sealed class BackendCertificateStore
{
    private volatile CertificateEntry? _entry;

    /// <summary>
    /// Returns true if a valid, non-expiring certificate is present.
    /// "Nearing expiry" is defined as less than 30 days remaining.
    /// </summary>
    public bool HasValidCertificate =>
        _entry is not null &&
        _entry.Certificate.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(30);

    /// <summary>
    /// The current backend client certificate. Throws if not yet bootstrapped.
    /// </summary>
    public X509Certificate2 Certificate =>
        _entry?.Certificate ?? throw new InvalidOperationException(
            "Backend mTLS certificate has not been bootstrapped yet.");

    /// <summary>
    /// Stores a newly issued or loaded certificate.
    /// The store takes ownership; the caller should not dispose the certificate.
    /// </summary>
    public void Store(X509Certificate2 certificate) =>
        _entry = new CertificateEntry(certificate);

    private sealed record CertificateEntry(X509Certificate2 Certificate);
}
