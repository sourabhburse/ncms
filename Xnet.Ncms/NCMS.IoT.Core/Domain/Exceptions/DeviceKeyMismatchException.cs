namespace NCMS.IoT.Core.Domain.Exceptions;

/// <summary>
/// Thrown when a re-provisioning request is received for an already-provisioned device
/// but the incoming CSR carries a different public key than the stored certificate.
/// The device must use the certificate renewal endpoint rather than the provisioning endpoint.
/// </summary>
public sealed class DeviceKeyMismatchException : Exception
{
    public string SerialNumber { get; }

    public DeviceKeyMismatchException(string serialNumber)
        : base($"Device '{serialNumber}' is already provisioned with a different key pair. " +
               "Submit a certificate renewal request instead of re-provisioning.")
    {
        SerialNumber = serialNumber;
    }
}
