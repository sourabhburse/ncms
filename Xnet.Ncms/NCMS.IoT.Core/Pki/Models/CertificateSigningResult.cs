namespace NCMS.IoT.Core.Pki.Models;

/// <summary>
/// Immutable result returned by the PKI engine after signing a CSR.
/// </summary>
public sealed record CertificateSigningResult
{
    /// <summary>PEM-encoded signed client certificate.</summary>
    public required string CertificatePem { get; init; }

    /// <summary>PEM-encoded Root CA certificate for client trust anchoring.</summary>
    public required string RootCaPem { get; init; }

    /// <summary>SHA-1 thumbprint (hex, uppercase, no colons) of the issued certificate.</summary>
    public required string Thumbprint { get; init; }

    /// <summary>Subject Distinguished Name as a string.</summary>
    public required string SubjectName { get; init; }

    /// <summary>UTC timestamp when the certificate becomes valid.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>UTC timestamp when the certificate expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
