using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Entities;
using NCMS.IoT.Identity.Services;

namespace NCMS.IoT.Host.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IUserClaimsBuilder _claimsBuilder;
    private readonly IAuditLogService _auditLog;

    public LoginModel(UserManager<AppUser> userManager, IUserClaimsBuilder claimsBuilder, IAuditLogService auditLog)
    {
        _userManager = userManager;
        _claimsBuilder = claimsBuilder;
        _auditLog = auditLog;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user is null || !user.IsActive || !await _userManager.CheckPasswordAsync(user, Input.Password))
        {
            await _auditLog.RecordAsync(
                AuditEventType.LoginFailed,
                $"Failed login attempt for '{Input.Email}'.",
                subjectUserId: user?.Id,
                subjectDisplay: Input.Email);
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        var claims = await _claimsBuilder.BuildAsync(user);
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = Input.RememberMe,
                AllowRefresh = true
            });

        await _auditLog.RecordAsync(
            AuditEventType.LoginSucceeded,
            $"User '{user.Email}' logged in.",
            subjectUserId: user.Id,
            subjectDisplay: user.Email,
            actorUserId: user.Id,
            actorDisplay: user.Email);

        if (Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToPage("/Index");
    }
}
