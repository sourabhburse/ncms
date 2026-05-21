using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCMS.Backend.Core.Provisioning;
using NCMS.Backend.Infrastructure.Provisioning;

namespace NCMS.Backend.Infrastructure.Pki;

public sealed class PersistentDeviceCertificateIssuer : IDeviceCertificateIssuer
{
    private const string CaCertificateFileName = "ca.crt";
    private const string CaPrivateKeyFileName = "ca.key";

    private readonly X509Certificate2 _caCertificate;

    public PersistentDeviceCertificateIssuer(IOptions<ProvisioningOptions> options, ILogger<PersistentDeviceCertificateIssuer> logger)
    {
        string directory = ResolveDirectory(options.Value.CertificateAuthorityDirectory);
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create configured CA directory '{Directory}'. Falling back to local 'certs' folder.", directory);
            directory = Path.Combine(AppContext.BaseDirectory, "certs");
            Directory.CreateDirectory(directory);
        }

        string caCertificatePath = Path.Combine(directory, CaCertificateFileName);
        string caPrivateKeyPath = Path.Combine(directory, CaPrivateKeyFileName);

        _caCertificate = LoadOrCreateCaCertificate(caCertificatePath, caPrivateKeyPath, logger);
    }

    public DeviceCertificateBundle Issue(string deviceId, string serialNumber, string csrPem, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        CertificateRequest clientRequest = CertificateRequest.LoadSigningRequestPem(
            csrPem,
            HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.Default);

        string escapedSerial = EscapeX500(serialNumber);
        var subject = new X500DistinguishedName($"CN={deviceId}, SERIALNUMBER={escapedSerial}");
        var request = new CertificateRequest(subject, clientRequest.PublicKey, HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddUri(new Uri($"urn:ncms:serial:{Uri.EscapeDataString(serialNumber)}"));
        request.CertificateExtensions.Add(sanBuilder.Build());

        byte[] certificateSerial = RandomNumberGenerator.GetBytes(16);
        using X509Certificate2 issuedPublicCert = request.Create(_caCertificate, notBefore, notAfter, certificateSerial);

        return new DeviceCertificateBundle
        {
            CaCertificatePem = _caCertificate.ExportCertificatePem(),
            ClientCertificatePem = issuedPublicCert.ExportCertificatePem(),
            ExpiresAt = notAfter
        };
    }

    private static X509Certificate2 LoadOrCreateCaCertificate(string certificatePath, string privateKeyPath, ILogger logger)
    {
        if (File.Exists(certificatePath) && File.Exists(privateKeyPath))
        {
            logger.LogInformation("Loading persistent CA from {CertificatePath}", certificatePath);
            return X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);
        }

        logger.LogInformation("Creating persistent CA at {CertificatePath}", certificatePath);

        using ECDsa caKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(
            "CN=NCMS Device CA",
            caKey,
            HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        using X509Certificate2 caCertificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));

        File.WriteAllText(certificatePath, caCertificate.ExportCertificatePem());
        File.WriteAllText(privateKeyPath, caKey.ExportPkcs8PrivateKeyPem());

        return X509Certificate2.CreateFromPemFile(certificatePath, privateKeyPath);
    }

    private static string ResolveDirectory(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath, AppContext.BaseDirectory);
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "ncms-infra",
            "mosquitto",
            "certs"));
    }

    private static string EscapeX500(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("+", "\\+", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Trim();
    }
}
