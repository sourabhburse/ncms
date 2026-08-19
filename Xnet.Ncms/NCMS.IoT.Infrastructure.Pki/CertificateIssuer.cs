using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCMS.IoT.Core.Configuration;
using NCMS.IoT.Core.Domain.Exceptions;
using NCMS.IoT.Core.Domain.Interfaces;
using NCMS.IoT.Core.Pki.Models;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace NCMS.IoT.Infrastructure.Pki;

/// <summary>
/// Production PKI engine backed by BouncyCastle.Cryptography.
/// Thread-safe: the CA cert and key are loaded once at construction time.
/// </summary>
public sealed class CertificateIssuer : IDeviceCertificateIssuer
{
    private readonly X509Certificate _caCert;
    private readonly AsymmetricKeyParameter _caPrivateKey;
    private readonly string _rootCaPem;
    private readonly string _issuerOrganization;
    private readonly ILogger<CertificateIssuer> _logger;

    public CertificateIssuer(
        IOptions<RootCaOptions> options,
        ILogger<CertificateIssuer> logger)
    {
        _logger = logger;
        var opts = options.Value;

        try
        {
            _rootCaPem = opts.ResolveCertificatePem();
            _caCert = LoadCertificate(_rootCaPem);
            _caPrivateKey = LoadPrivateKey(opts.ResolvePrivateKeyPem());
            _issuerOrganization = opts.IssuerOrganization;
        }
        catch (Exception ex) when (ex is not PkiException)
        {
            throw new PkiException("Failed to initialize Root CA from configuration.", ex);
        }

        _logger.LogInformation(
            "PKI engine initialized. Root CA subject: {Subject}, valid until: {Expiry}",
            _caCert.SubjectDN,
            _caCert.NotAfter.ToString("O"));
    }

    /// <inheritdoc />
    public Task<CertificateSigningResult> IssueAsync(
        string base64Csr,
        string commonName,
        int validityDays = 730)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64Csr);
        ArgumentException.ThrowIfNullOrWhiteSpace(commonName);

        // Offload to a thread-pool thread so the caller's thread is freed while CPU-bound
        // RSA signing runs. Each call gets its own SecureRandom — the shared instance is
        // not thread-safe under concurrent mutation of its internal DigestRandomGenerator state.
        return Task.Run(() => IssueCore(base64Csr, commonName, validityDays));
    }

    private CertificateSigningResult IssueCore(string base64Csr, string commonName, int validityDays)
    {
        // Call-local SecureRandom: each concurrent request gets independent entropy state.
        var secureRandom = new SecureRandom();

        try
        {
            // 1. Normalise the CSR: strip PEM headers if present, leaving raw Base64 DER.
            var normalizedBase64 = StripPemHeaders(base64Csr);
            var csrBytes = Convert.FromBase64String(normalizedBase64);
            var csr = new Pkcs10CertificationRequest(csrBytes);

            if (!csr.Verify())
                throw new PkiException("CSR signature verification failed — the CSR has been tampered with.");

            var csrInfo = csr.GetCertificationRequestInfo();
            var subjectPublicKey = PublicKeyFactory.CreateKey(csrInfo.SubjectPublicKeyInfo);

            // 2. Build the subject DN, forcefully overriding CN to our authoritative value.
            var subjectDn = new X509Name($"CN={commonName}, O={_issuerOrganization}");

            // 3. Generate a cryptographically unique serial number.
            var serial = BigInteger.ProbablePrime(128, secureRandom);

            var notBefore = DateTime.UtcNow.AddMinutes(-5); // clock-skew tolerance
            var notAfter = DateTime.UtcNow.AddDays(validityDays);

            // 4. Assemble the X.509 v3 certificate.
            var certGen = new X509V3CertificateGenerator();
            certGen.SetSerialNumber(serial);
            certGen.SetIssuerDN(_caCert.SubjectDN);
            certGen.SetSubjectDN(subjectDn);
            certGen.SetNotBefore(notBefore);
            certGen.SetNotAfter(notAfter);
            certGen.SetPublicKey(subjectPublicKey);

            // 5. Add standard IoT X.509 v3 extensions.
            //    BasicConstraints(false) = this is NOT a CA certificate.
            certGen.AddExtension(
                X509Extensions.BasicConstraints,
                critical: true,
                extensionValue: new BasicConstraints(cA: false));

            //    KeyUsage: DigitalSignature | KeyEncipherment (TLS client auth)
            certGen.AddExtension(
                X509Extensions.KeyUsage,
                critical: true,
                extensionValue: new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));

            //    ExtendedKeyUsage: id-kp-clientAuth (OID 1.3.6.1.5.5.7.3.2)
            certGen.AddExtension(
                X509Extensions.ExtendedKeyUsage,
                critical: false,
                extensionValue: new ExtendedKeyUsage(KeyPurposeID.id_kp_clientAuth));

            //    SubjectKeyIdentifier — derived from the public key.
            var subjectKeyId = new SubjectKeyIdentifierStructure(subjectPublicKey);
            certGen.AddExtension(
                X509Extensions.SubjectKeyIdentifier,
                critical: false,
                extensionValue: subjectKeyId);

            //    AuthorityKeyIdentifier — links back to the CA.
            var authorityKeyId = new AuthorityKeyIdentifierStructure(_caCert);
            certGen.AddExtension(
                X509Extensions.AuthorityKeyIdentifier,
                critical: false,
                extensionValue: authorityKeyId);

            // 6. Sign using the Root CA's private key with SHA-256.
            var signatureFactory = new Asn1SignatureFactory(
                algorithm: "SHA256WITHRSA",
                privateKey: _caPrivateKey,
                random: secureRandom);

            var issuedCert = certGen.Generate(signatureFactory);

            // 7. Encode to PEM.
            var certPem = ToPem(issuedCert);

            // 8. Compute the SHA-1 thumbprint (standard X.509 fingerprint convention).
            var thumbprint = ComputeThumbprint(issuedCert.GetEncoded());

            _logger.LogInformation(
                "Issued certificate: CN={CommonName}, Thumbprint={Thumbprint}, Expires={Expiry}",
                commonName, thumbprint, notAfter.ToString("O"));

            return new CertificateSigningResult
            {
                CertificatePem = certPem,
                RootCaPem = _rootCaPem,
                Thumbprint = thumbprint,
                SubjectName = subjectDn.ToString(),
                IssuedAt = new DateTimeOffset(notBefore, TimeSpan.Zero),
                ExpiresAt = new DateTimeOffset(notAfter, TimeSpan.Zero)
            };
        }
        catch (PkiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new PkiException($"Failed to issue certificate for CN={commonName}.", ex);
        }
    }

    /// <inheritdoc />
    public string GetRootCaPem() => _rootCaPem;

    /// <inheritdoc />
    public bool PublicKeysMatch(string csrPem, string certificatePem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(csrPem);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePem);

        try
        {
            // Parse the CSR and extract its SubjectPublicKeyInfo.
            var normalizedBase64 = StripPemHeaders(csrPem);
            var csrBytes = Convert.FromBase64String(normalizedBase64);
            var csr = new Pkcs10CertificationRequest(csrBytes);
            var csrSpki = csr.GetCertificationRequestInfo().SubjectPublicKeyInfo;

            // Parse the certificate and extract its SubjectPublicKeyInfo.
            using var certReader = new StringReader(certificatePem);
            var cert = (X509Certificate)new PemReader(certReader).ReadObject();
            var certSpki = cert.CertificateStructure.SubjectPublicKeyInfo;

            // DER-encode both and compare bytes. This covers the algorithm OID and
            // key material together, making it correct across RSA, EC, and Ed25519.
            return csrSpki.GetDerEncoded().SequenceEqual(certSpki.GetDerEncoded());
        }
        catch (Exception ex) when (ex is not PkiException)
        {
            throw new PkiException("Failed to compare public keys from CSR and certificate.", ex);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static X509Certificate LoadCertificate(string pem)
    {
        using var reader = new StringReader(pem);
        var pemReader = new PemReader(reader);
        return pemReader.ReadObject() as X509Certificate
               ?? throw new PkiException("The provided CA certificate PEM is not a valid X.509 certificate.");
    }

    private static AsymmetricKeyParameter LoadPrivateKey(string pem)
    {
        using var reader = new StringReader(pem);
        var pemReader = new PemReader(reader);
        var obj = pemReader.ReadObject()
                  ?? throw new PkiException("The provided CA private key PEM is empty or unreadable.");

        return obj switch
        {
            AsymmetricCipherKeyPair kp => kp.Private,
            AsymmetricKeyParameter ak => ak,
            _ => throw new PkiException($"Unexpected PEM object type: {obj.GetType().Name}. Expected an RSA private key.")
        };
    }

    private static string ToPem(X509Certificate cert)
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        var pemWriter = new PemWriter(sw);
        pemWriter.WriteObject(cert);
        sw.Flush();
        return sb.ToString();
    }

    private static string ComputeThumbprint(byte[] derEncoded)
    {
        var hash = SHA1.HashData(derEncoded);
        return Convert.ToHexString(hash).ToUpperInvariant();
    }

    /// <summary>
    /// Accepts either a full PEM string (-----BEGIN CERTIFICATE REQUEST-----...)
    /// or a raw Base64 DER string and always returns raw Base64.
    /// </summary>
    private static string StripPemHeaders(string csrInput)
    {
        if (!csrInput.Contains("-----BEGIN", StringComparison.Ordinal))
            return csrInput.Trim();

        var sb = new System.Text.StringBuilder();
        foreach (var line in csrInput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("-----", StringComparison.Ordinal))
                sb.Append(trimmed);
        }

        return sb.ToString();
    }
}
