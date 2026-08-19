using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using NCMS.IoT.Core.Domain.Interfaces;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;

namespace NCMS.IoT.MqttClient.Bootstrapping;

/// <summary>
/// Generates or refreshes the backend's own mTLS client certificate using the internal PKI.
/// This runs before the MQTT connection is established, implementing the auto-bootstrapping lifecycle.
/// </summary>
public sealed class BackendCertificateBootstrapper
{
    private const string BackendCn = "ncms-client";
    private const int RsaKeySize = 2048;
    private const int CertValidityDays = 365;

    private readonly BackendCertificateStore _store;
    private readonly IDeviceCertificateIssuer _certIssuer;
    private readonly ILogger<BackendCertificateBootstrapper> _logger;

    public BackendCertificateBootstrapper(
        BackendCertificateStore store,
        IDeviceCertificateIssuer certIssuer,
        ILogger<BackendCertificateBootstrapper> logger)
    {
        _store = store;
        _certIssuer = certIssuer;
        _logger = logger;
    }

    /// <summary>
    /// Ensures a valid backend certificate is present in the store.
    /// Generates a new RSA key pair and signs a CSR if needed.
    /// </summary>
    public async Task EnsureCertificateAsync(CancellationToken ct)
    {
        if (_store.HasValidCertificate)
        {
            _logger.LogDebug("Backend mTLS certificate is present and valid; skipping bootstrapping.");
            return;
        }

        _logger.LogInformation(
            "Backend mTLS certificate missing or nearing expiry. Generating new certificate for CN={Cn}.",
            BackendCn);

        // ── Step 1: Generate RSA-2048 key pair in memory using BouncyCastle ──
        var secureRandom = new SecureRandom();
        var keyGenParams = new KeyGenerationParameters(secureRandom, RsaKeySize);
        var keyGen = new RsaKeyPairGenerator();
        keyGen.Init(keyGenParams);
        AsymmetricCipherKeyPair keyPair = keyGen.GenerateKeyPair();

        // ── Step 2: Build a PKCS#10 CSR for CN=dotnet-backend ───────────────
        var subject = new X509Name($"CN={BackendCn}");
        var csr = new Pkcs10CertificationRequest(
            signatureAlgorithm: "SHA256WITHRSA",
            subject: subject,
            publicKey: keyPair.Public,
            attributes: null,
            signingKey: keyPair.Private);

        if (!csr.Verify())
            throw new CryptographicException("Generated CSR failed self-verification.");

        var csrBase64 = Convert.ToBase64String(csr.GetEncoded());

        // ── Step 3: Sign via the internal PKI engine ─────────────────────────
        var result = await _certIssuer.IssueAsync(
            base64Csr: csrBase64,
            commonName: BackendCn,
            validityDays: CertValidityDays);

        _logger.LogInformation(
            "Backend certificate issued. Thumbprint={Thumbprint}, Expires={Expiry}",
            result.Thumbprint, result.ExpiresAt);

        // ── Step 4: Convert to X509Certificate2 with exportable private key ──
        //    .NET 8+ X509Certificate2.CreateFromPem handles the combination.
        var privateKeyPem = ExportPrivateKeyToPem(keyPair.Private);
        var combined = X509Certificate2.CreateFromPem(result.CertificatePem, privateKeyPem);

        // On Windows, we must use the PFX round-trip to make the key usable by SChannel.
        var exportable = ExportAsPfxAndReload(combined);
        combined.Dispose();

        _store.Store(exportable);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string ExportPrivateKeyToPem(AsymmetricKeyParameter privateKey)
    {
        var sb = new StringBuilder();
        using var sw = new StringWriter(sb);
        var pemWriter = new PemWriter(sw);
        pemWriter.WriteObject(privateKey);
        sw.Flush();
        return sb.ToString();
    }

    /// <summary>
    /// Exports the certificate+key as a PFX blob and re-imports with the private key
    /// marked as exportable. Required on Windows so the TLS stack can access the key.
    /// </summary>
    private static X509Certificate2 ExportAsPfxAndReload(X509Certificate2 cert)
    {
        var pfxBytes = cert.Export(X509ContentType.Pfx);
        // EphemeralKeySet bypasses the Windows CNG key store. SChannel (used by SslStream/MQTTnet)
        // is an OS-level component and cannot access keys stored outside the CNG store on Windows,
        // causing Win32Exception 0x8009030D at AcquireCredentialsHandle. On Linux/macOS there is no
        // OS key store, so EphemeralKeySet is safe and avoids leaving keys on disk.
        var flags = X509KeyStorageFlags.Exportable;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            flags |= X509KeyStorageFlags.EphemeralKeySet;

        return X509CertificateLoader.LoadPkcs12(pfxBytes, password: (string?)null, keyStorageFlags: flags);
    }
}
