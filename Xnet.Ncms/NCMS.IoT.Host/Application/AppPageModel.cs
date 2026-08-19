using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using NCMS.IoT.Identity.Contracts.Services;

namespace NCMS.IoT.Host.Application;

/// <summary>
/// Base PageModel with presentation helpers shared by all pages: toast notifications
/// (rendered by <c>_Toastr</c> on the next request) and mapping of the Mediator
/// validation pipeline's <see cref="ValidationException"/> into <see cref="ModelState"/>.
/// </summary>
public abstract class AppPageModel : PageModel
{
    protected void ToastSuccess(string message) => TempData["toast.success"] = message;
    protected void ToastError(string message) => TempData["toast.error"] = message;
    protected void ToastInfo(string message) => TempData["toast.info"] = message;

    /// <summary>
    /// True when the request comes from the client-side tableController (HTML-over-the-wire),
    /// which asks for just the results partial instead of the whole page.
    /// </summary>
    protected bool IsPartialTableRequest => Request.Headers.ContainsKey("X-Partial-Table");

    /// <summary>
    /// Returns the results partial for a tableController fetch, or the full page otherwise.
    /// Lets a single OnGet handler serve both the initial full load and partial updates.
    /// </summary>
    protected IActionResult PageOrPartial(string partialName) =>
        IsPartialTableRequest ? Partial(partialName, this) : Page();

    /// <summary>
    /// Maps FluentValidation failures (thrown by the shared ValidationBehavior pipeline)
    /// onto ModelState so they render inline next to the offending fields.
    /// </summary>
    protected void AddValidationErrors(ValidationException ex)
    {
        foreach (var failure in ex.Errors)
            ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
    }

    /// <summary>
    /// True when the signed-in user's roles grant the given permission. Resolved via
    /// <see cref="ICurrentUser"/> (role-based lookup, cached) rather than a "Permission"
    /// claim — the cookie principal deliberately carries only role names (see
    /// <c>UserClaimsBuilder</c>), not the full permission set, to keep it small.
    /// </summary>
    protected bool HasPermission(string permission) =>
        HttpContext.RequestServices.GetRequiredService<ICurrentUser>().HasPermission(permission);

    /// <summary>
    /// Guard for mutating handlers (create/update/delete/upload/enable) on a page that's
    /// otherwise gated at the class level by a coarser "View" policy — returns a 403 result
    /// when the user lacks <paramref name="permission"/>, or <c>null</c> to continue.
    /// </summary>
    protected IActionResult? EnsurePermission(string permission) =>
        HasPermission(permission) ? null : Forbid();
}
