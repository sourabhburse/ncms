using Microsoft.AspNetCore.Mvc;
using NCMS.Backend.Core.Provisioning;

namespace NCMS.Backend.API.Controllers;

[ApiController]
[Route("api/v1/provision")]
public sealed class ProvisioningController : ControllerBase
{
    private readonly IProvisioningService _provisioningService;

    public ProvisioningController(IProvisioningService provisioningService)
    {
        _provisioningService = provisioningService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        ProvisioningResult result = await _provisioningService.RegisterAsync(request, cancellationToken);

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        return StatusCode(result.StatusCode, result.Payload);
    }
}
