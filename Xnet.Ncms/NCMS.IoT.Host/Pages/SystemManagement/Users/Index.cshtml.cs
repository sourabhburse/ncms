using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NCMS.IoT.Host.Application;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Features.V1.Roles;
using NCMS.IoT.Identity.Features.V1.Users;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Pages.SystemManagement.Users;

[Authorize(Policy = IdentityPermissions.Users.List)]
public class IndexModel : AppPageModel
{
    private readonly ISender _sender;
    public IndexModel(ISender sender) => _sender = sender;

    // ── Filters / paging (GET) ──────────────────────────────────────────────
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Role { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? IsActive { get; set; }

    public int[] PageSizeOptions { get; } = [25, 50, 100];

    // ── Page data ─────────────────────────────────────────────────────────
    public PagedResponse<UserDto> Result { get; private set; } = new();
    public IReadOnlyList<RoleDto> AllRoles { get; private set; } = [];

    public sealed class CreateInput
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public List<string> Roles { get; set; } = [];
    }

    public sealed class EditInput
    {
        public Guid Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = [];
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        Result = await _sender.Send(new ListUsers.Query(Search, Role, IsActive, PageNumber, PageSize), ct);
        AllRoles = (await _sender.Send(new ListRoles.Query(null, 1, 1000), ct)).Items.ToList();
        return Page();
    }

    /// <summary>Active filters (plus page size) preserved across pagination links.</summary>
    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Search)) values["search"] = Search;
        if (!string.IsNullOrWhiteSpace(Role)) values["role"] = Role;
        if (IsActive is { } a) values["isActive"] = a.ToString();
        values["pageSize"] = PageSize.ToString();
        return values;
    }

    public async Task<IActionResult> OnPostCreateAsync([FromForm] CreateInput input, CancellationToken ct)
    {
        if (EnsurePermission(IdentityPermissions.Users.Add) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new CreateUser.Command(
                input.UserName, input.Email, input.Password, input.FirstName, input.LastName, input.Roles), ct);
            ToastSuccess("User created successfully.");
        }
        catch (ValidationException ex) { ToastError(string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (Exception ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync([FromForm] EditInput input, CancellationToken ct)
    {
        if (EnsurePermission(IdentityPermissions.Users.Edit) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new UpdateUser.Command(
                input.Id, input.FirstName, input.LastName, input.IsActive, input.Roles), ct);
            ToastSuccess("User updated successfully.");
        }
        catch (Exception ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync([FromForm] Guid id, CancellationToken ct)
    {
        if (EnsurePermission(IdentityPermissions.Users.Delete) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new DeleteUser.Command(id), ct);
            ToastSuccess("User deleted successfully.");
        }
        catch (Exception ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(
        [FromForm] Guid id, [FromForm] string newPassword, CancellationToken ct)
    {
        if (EnsurePermission(IdentityPermissions.Users.Edit) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new ResetPassword.Command(id, newPassword), ct);
            ToastSuccess("Password reset successfully.");
        }
        catch (ValidationException ex) { ToastError(string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (Exception ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }
}
