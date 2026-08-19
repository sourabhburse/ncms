using NCMS.IoT.Core.Pki.Models;

namespace NCMS.IoT.Core.Domain.Interfaces;

/// <summary>
/// Signs a PKCS#10 CSR against the internal Root CA and returns the issued X.509 certificate.
/// The Common Name is always forcefully overridden to <paramref name="commonName"/>.
/// </summary>
public interface IDeviceCertificateIssuer
{
    /// <summary>
    /// Issues a signed X.509 v3 client certificate.
    /// </summary>
    /// <param name="base64Csr">PKCS#10 CSR as either a full PEM string or raw Base64 DER. PEM headers are stripped automatically.</param>
    /// <param name="commonName">CN to embed in the issued certificate. For devices, this is the Device GUID string.</param>
    /// <param name="validityDays">Certificate lifetime in days. Defaults to 730 (2 years).</param>
    /// <returns>A <see cref="CertificateSigningResult"/> containing PEM-encoded cert and the CA cert.</returns>
    Task<CertificateSigningResult> IssueAsync(
        string base64Csr,
        string commonName,
        int validityDays = 730);

    /// <summary>
    /// Returns the PEM-encoded Root CA certificate so clients can pin it.
    /// </summary>
    string GetRootCaPem();

    /// <summary>
    /// Returns <c>true</c> if the public key embedded in <paramref name="csrPem"/> matches
    /// the public key embedded in <paramref name="certificatePem"/>.
    /// Comparison is over the DER-encoded SubjectPublicKeyInfo structure, which covers both
    /// the algorithm identifier and the key material — safe across key types and sizes.
    /// </summary>
    /// <param name="csrPem">PKCS#10 CSR as PEM or raw Base64 DER.</param>
    /// <param name="certificatePem">PEM-encoded X.509 certificate.</param>
    bool PublicKeysMatch(string csrPem, string certificatePem);
}
