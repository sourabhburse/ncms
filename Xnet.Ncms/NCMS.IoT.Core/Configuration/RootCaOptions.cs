using System.ComponentModel.DataAnnotations;

namespace NCMS.IoT.Core.Configuration;

/// <summary>
/// Bound from configuration section "RootCa".
/// Supply either file paths (preferred for production) or inline PEM strings (useful for secrets managers).
/// </summary>
public sealed class RootCaOptions
{
    public const string SectionName = "RootCa";

    /// <summary>Absolute path to the Root CA PEM certificate file. Takes priority over <see cref="CertificatePem"/>.</summary>
    public string? CertificatePath { get; set; }

    /// <summary>Absolute path to the Root CA unencrypted PEM private key file. Takes priority over <see cref="PrivateKeyPem"/>.</summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>Inline PEM-encoded Root CA certificate. Used when <see cref="CertificatePath"/> is not set.</summary>
    public string? CertificatePem { get; set; }

    /// <summary>Inline PEM-encoded Root CA private key (RSA, unencrypted). Used when <see cref="PrivateKeyPath"/> is not set.</summary>
    public string? PrivateKeyPem { get; set; }

    /// <summary>Default issuer Organization name embedded in issued certificates.</summary>
    public string IssuerOrganization { get; set; } = "NCMS IoT Platform";

    /// <summary>
    /// Resolves the certificate PEM string, preferring the file path when available.
    /// </summary>
    public string ResolveCertificatePem()
    {
        if (!string.IsNullOrWhiteSpace(CertificatePath))
            return File.ReadAllText(CertificatePath);

        if (!string.IsNullOrWhiteSpace(CertificatePem))
            return CertificatePem;

        throw new InvalidOperationException(
            "RootCa configuration is missing. Set 'RootCa:CertificatePath' or 'RootCa:CertificatePem'.");
    }

    /// <summary>
    /// Resolves the private key PEM string, preferring the file path when available.
    /// </summary>
    public string ResolvePrivateKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(PrivateKeyPath))
            return File.ReadAllText(PrivateKeyPath);

        if (!string.IsNullOrWhiteSpace(PrivateKeyPem))
            return PrivateKeyPem;

        throw new InvalidOperationException(
            "RootCa configuration is missing. Set 'RootCa:PrivateKeyPath' or 'RootCa:PrivateKeyPem'.");
    }
}
