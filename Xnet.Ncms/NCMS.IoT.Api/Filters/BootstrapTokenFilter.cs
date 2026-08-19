// This file is intentionally minimal.
// Bootstrap token validation was moved into ProvisioningService where it belongs:
// the token is part of the request body (RegisterDeviceRequest.BootstrapToken),
// validated by ProvisioningService.ProvisionAsync using constant-time PBKDF2.
// Model binding's [Required] attribute on BootstrapToken ensures the field is present
// before the controller action body executes.
//
// The RequireBootstrapTokenAttribute class is kept as a no-op marker for
// documentation and future rate-limiting hooks at the action level.

using Microsoft.AspNetCore.Mvc.Filters;

namespace NCMS.IoT.Api.Filters;

/// <summary>
/// Marker attribute indicating that this action requires a factory bootstrap token
/// in the request body. Actual verification is performed in <see cref="NCMS.IoT.ZTP.Services.ProvisioningService"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireBootstrapTokenAttribute : Attribute, IFilterMetadata { }
