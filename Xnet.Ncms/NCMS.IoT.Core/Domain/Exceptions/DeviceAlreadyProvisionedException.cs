namespace NCMS.IoT.Core.Domain.Exceptions;

public sealed class DeviceAlreadyProvisionedException : Exception
{
    public string SerialNumber { get; }

    public DeviceAlreadyProvisionedException(string serialNumber)
        : base($"Hardware '{serialNumber}' is already provisioned and cannot be re-registered.")
    {
        SerialNumber = serialNumber;
    }
}
