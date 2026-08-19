namespace NCMS.IoT.Core.Domain.Exceptions;

public sealed class HardwareNotFoundException : Exception
{
    public string SerialNumber { get; }

    public HardwareNotFoundException(string serialNumber)
        : base($"No factory-registered hardware found with serial number '{serialNumber}'.")
    {
        SerialNumber = serialNumber;
    }
}
