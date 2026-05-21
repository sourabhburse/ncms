namespace NCMS.Backend.Core.Provisioning;

public sealed class ProvisioningResult
{
    public required int StatusCode { get; init; }
    public required object Payload { get; init; }
}
