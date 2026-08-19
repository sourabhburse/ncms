using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Services;

namespace NCMS.IoT.Host.Pages.Account;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly IAuditLogService _auditLog;
    public LogoutModel(IAuditLogService auditLog) => _auditLog = auditLog;

    public async Task<IActionResult> OnPostAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.FindFirstValue(ClaimTypes.Name);

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            await _auditLog.RecordAsync(
                AuditEventType.Logout,
                $"User '{userName}' logged out.",
                subjectUserId: userId,
                subjectDisplay: userName,
                actorUserId: userId,
                actorDisplay: userName);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
}
