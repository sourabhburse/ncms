using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCMS.IoT.Core.Configuration;
using NCMS.IoT.Core.Domain.Exceptions;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace NCMS.IoT.Infrastructure.Pki;

/// <summary>
/// Ensures a Root CA certificate and private key exist on disk before the application
/// starts accepting requests.
///
/// On first startup: generates a 4096-bit RSA self-signed Root CA and writes it to the
/// paths defined in <see cref="RootCaOptions"/>. On subsequent restarts: skips generation
/// if both files already exist and the certificate is still valid.
///
/// ⚠ Security note:
/// The Root CA private key is stored on the same filesystem as the application.
/// This is acceptable for development, single-server, or air-gapped deployments.
/// For production environments with a strong threat model, replace the generated key with
/// one issued by an offline CA or hardware security module (HSM) and set
/// <c>RootCa:CertificatePath</c> / <c>RootCa:PrivateKeyPath</c> to those external files —
/// this bootstrapper will detect they exist and skip generation entirely.
/// </summary>
public sealed class RootCaBootstrapper
{
    private const int RsaKeyBits = 4096;
    private const int ValidityYears = 10;

    private readonly RootCaOptions _options;
    private readonly ILogger<RootCaBootstrapper> _logger;

    public RootCaBootstrapper(IOptions<RootCaOptions> options, ILogger<RootCaBootstrapper> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Idempotent startup check. Generates the Root CA only when both paths are
    /// configured AND the files do not yet exist. Safe to call on every restart.
    /// </summary>
    public Task EnsureAsync(CancellationToken ct = default)
    {
        // Inline PEM mode — no files to manage, nothing to do.
        if (string.IsNullOrWhiteSpace(_options.CertificatePath) ||
            string.IsNullOrWhiteSpace(_options.PrivateKeyPath))
        {
            _logger.LogDebug(
                "RootCa is configured via inline PEM; skipping file-based CA bootstrapping.");
            return Task.CompletedTask;
        }

        var certPath = _options.CertificatePath;
        var keyPath = _options.PrivateKeyPath;

        var certExists = File.Exists(certPath);
        var keyExists = File.Exists(keyPath);

        // Both present — validate the cert is still usable.
        if (certExists && keyExists)
        {
            ValidateExistingCertificate(certPath);
            _logger.LogInformation(
                "Root CA found on disk. CertPath={CertPath}", certPath);
            return Task.CompletedTask;
        }

        // Partial state: one file present, the other missing — regenerate both to stay consistent.
        if (certExists != keyExists)
        {
            _logger.LogWarning(
                "Root CA files are in an inconsistent state " +
                "(CertExists={CertExists}, KeyExists={KeyExists}). Regenerating both.",
                certExists, keyExists);
        }
        else
        {
            _logger.LogInformation(
                "Root CA not found. Generating a new {Bits}-bit RSA Root CA. " +
                "This happens once — keep these files safe: {CertPath}, {KeyPath}",
                RsaKeyBits, certPath, keyPath);
        }

        GenerateAndSave(certPath, keyPath);
        return Task.CompletedTask;
    }

    // ── Generation ───────────────────────────────────────────────────────────

    private void GenerateAndSave(string certPath, string keyPath)
    {
        // Ensure the target directory exists.
        var directory = Path.GetDirectoryName(certPath)
            ?? throw new PkiException($"Cannot determine directory from cert path '{certPath}'.");
        Directory.CreateDirectory(directory);

        var secureRandom = new SecureRandom();

        // 1. Generate RSA-4096 key pair.
        var keyGen = new RsaKeyPairGenerator();
        keyGen.Init(new KeyGenerationParameters(secureRandom, RsaKeyBits));
        AsymmetricCipherKeyPair keyPair = keyGen.GenerateKeyPair();

        // 2. Build subject / issuer DN (self-signed: both are the same).
        var subject = new X509Name(
            $"CN=NCMS IoT Root CA, O={_options.IssuerOrganization}");

        var notBefore = DateTime.UtcNow.AddMinutes(-5);
        var notAfter = DateTime.UtcNow.AddYears(ValidityYears);

        // 3. Assemble the CA certificate.
        var certGen = new X509V3CertificateGenerator();
        certGen.SetSerialNumber(BigInteger.One);
        certGen.SetIssuerDN(subject);
        certGen.SetSubjectDN(subject);
        certGen.SetNotBefore(notBefore);
        certGen.SetNotAfter(notAfter);
        certGen.SetPublicKey(keyPair.Public);

        // BasicConstraints: cA=true, no pathLength constraint (can sign device certs)
        certGen.AddExtension(
            X509Extensions.BasicConstraints,
            critical: true,
            extensionValue: new BasicConstraints(cA: true));

        // KeyUsage: KeyCertSign (issue device certs) + CrlSign (future CRL support)
        certGen.AddExtension(
            X509Extensions.KeyUsage,
            critical: true,
            extensionValue: new KeyUsage(KeyUsage.KeyCertSign | KeyUsage.CrlSign));

        // SubjectKeyIdentifier: aids certificate chain building by clients
        certGen.AddExtension(
            X509Extensions.SubjectKeyIdentifier,
            critical: false,
            extensionValue: new SubjectKeyIdentifierStructure(keyPair.Public));

        // 4. Self-sign with the CA's own private key.
        var signatureFactory = new Asn1SignatureFactory(
            "SHA256WITHRSA", keyPair.Private, secureRandom);

        X509Certificate caCert = certGen.Generate(signatureFactory);

        // 5. Write files atomically: write to .tmp, then rename to avoid
        //    leaving a half-written file if the process is interrupted.
        WritePemAtomic(certPath, caCert);
        WritePemAtomic(keyPath, keyPair.Private);

        // 6. Restrict private key file permissions on POSIX systems.
        RestrictKeyFilePermissions(keyPath);

        _logger.LogWarning(
            "⚠ Root CA generated and saved. The private key at '{KeyPath}' is the trust anchor " +
            "for ALL device certificates. Back it up securely and restrict access. " +
            "Deleting it will invalidate all issued device certificates.",
            keyPath);

        _logger.LogInformation(
            "Root CA ready. Subject={Subject}, ValidUntil={ValidUntil}",
            subject, notAfter.ToString("O"));
    }

    // ── Validation of existing cert ──────────────────────────────────────────

    private void ValidateExistingCertificate(string certPath)
    {
        try
        {
            using var reader = new StringReader(File.ReadAllText(certPath));
            var cert = (X509Certificate) new PemReader(reader).ReadObject();

            var daysRemaining = (cert.NotAfter - DateTime.UtcNow).TotalDays;

            if (daysRemaining < 0)
                throw new PkiException(
                    $"The Root CA certificate at '{certPath}' has expired. " +
                    "Delete both CA files and restart to auto-generate a new one, " +
                    "then re-provision all devices.");

            if (daysRemaining < 180)
                _logger.LogWarning(
                    "Root CA certificate expires in {Days:F0} days ({Expiry}). " +
                    "Plan CA rotation before expiry to avoid service disruption.",
                    daysRemaining, cert.NotAfter.ToString("O"));
            else
                _logger.LogDebug(
                    "Root CA valid for {Days:F0} more days.", daysRemaining);
        }
        catch (PkiException) { throw; }
        catch (Exception ex)
        {
            throw new PkiException(
                $"Root CA certificate at '{certPath}' could not be read or parsed.", ex);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void WritePemAtomic(string targetPath, object pemObject)
    {
        var tmpPath = targetPath + ".tmp";
        try
        {
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            {
                var writer = new PemWriter(sw);
                writer.WriteObject(pemObject);
                sw.Flush();
            }

            File.WriteAllText(tmpPath, sb.ToString(), Encoding.ASCII);
            File.Move(tmpPath, targetPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tmpPath))
                File.Delete(tmpPath);
            throw;
        }
    }

    private static void RestrictKeyFilePermissions(string keyPath)
    {
        // On Linux/macOS: chmod 600 (owner read/write only)
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                File.SetUnixFileMode(keyPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                // Non-fatal: log but don't prevent startup
                Console.Error.WriteLine(
                    $"[WARN] Could not set restrictive permissions on '{keyPath}': {ex.Message}");
            }
        }
        // On Windows: NTFS ACL hardening is out of scope for this bootstrapper.
        // Consider using DPAPI or Windows Certificate Store for production key protection.
    }
}
