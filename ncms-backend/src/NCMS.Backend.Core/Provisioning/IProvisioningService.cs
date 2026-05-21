namespace NCMS.Backend.Core.Provisioning;

public interface IProvisioningService
{
    Task<ProvisioningResult> RegisterAsync(RegisterDeviceRequest request, CancellationToken cancellationToken);
}
