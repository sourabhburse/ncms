using System.Text.RegularExpressions;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NCMS.IoT.Host.Application;
using NCMS.IoT.Identity.Configuration;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Features.V1.Roles;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Pages.SystemManagement.Roles;

[Authorize(Policy = IdentityPermissions.Roles.List)]
public class IndexModel : AppPageModel
{
    private readonly ISender _sender;
    private readonly KnownPermissions _knownPermissions;

    public IndexModel(ISender sender, KnownPermissions knownPermissions)
    {
        _sender = sender;
        _knownPermissions = knownPermissions;
    }

    // ── Filters / paging (GET) ──────────────────────────────────────────────
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public int[] PageSizeOptions { get; } = [25, 50, 100];

    // ── Page data ─────────────────────────────────────────────────────────
    public PagedResponse<RoleDto> Result { get; private set; } = new();

    /// <summary>RoleId -> its granted/known permissions, keyed for the permission-management modal (current page only).</summary>
    public Dictionary<Guid, RolePermissionsDto> Permissions { get; } = new();

    /// <summary>
    /// Every known permission ("Permissions.&lt;Module&gt;.&lt;Area&gt;.&lt;Action&gt;"), organized as a
    /// three-level tree (Module -&gt; Area -&gt; Action) for the permission-management tree view.
    /// </summary>
    public IReadOnlyList<PermissionModuleNode> PermissionTree { get; private set; } = [];

    public sealed record PermissionActionNode(string Action, string Permission);
    public sealed record PermissionAreaNode(string Name, IReadOnlyList<PermissionActionNode> Actions);
    public sealed record PermissionModuleNode(string Name, IReadOnlyList<PermissionAreaNode> Areas);

    public sealed class CreateInput
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public sealed class EditInput
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        Result = await _sender.Send(new ListRoles.Query(Search, PageNumber, PageSize), ct);

        foreach (var role in Result.Items)
            Permissions[role.Id] = await _sender.Send(new GetRolePermissions.Query(role.Id), ct);

        PermissionTree = BuildTree(_knownPermissions.All);

        return Page();
    }

    /// <summary>Active filters (plus page size) preserved across pagination links.</summary>
    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Search)) values["search"] = Search;
        values["pageSize"] = PageSize.ToString();
        return values;
    }

    /// <summary>"Permissions.&lt;Module&gt;.&lt;Area&gt;.&lt;Action&gt;" -&gt; a Module/Area/Action tree.</summary>
    private static IReadOnlyList<PermissionModuleNode> BuildTree(IEnumerable<string> permissions) =>
        permissions
            .Select(p => p.Split('.'))
            .Where(parts => parts.Length >= 4)
            .GroupBy(parts => parts[1])
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(moduleGroup => new PermissionModuleNode(
                Humanize(moduleGroup.Key),
                moduleGroup
                    .GroupBy(parts => parts[2])
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(areaGroup => new PermissionAreaNode(
                        Humanize(areaGroup.Key),
                        areaGroup
                            .Select(parts => new PermissionActionNode(parts[3], string.Join('.', parts)))
                            .OrderBy(a => a.Action, StringComparer.Ordinal)
                            .ToList()))
                    .ToList()))
            .ToList();

    /// <summary>"DeviceManagement" -&gt; "Device Management"; "ConfigProfiles" -&gt; "Config Profiles".</summary>
    private static string Humanize(string pascalCase) =>
        Regex.Replace(pascalCase, "(?<=[a-z0-9])(?=[A-Z])", " ");

    public async Task<IActionResult> OnPostCreateAsync([FromForm] CreateInput input, CancellationToken ct)
    {
        if (EnsurePermission(IdentityPermissions.Roles.Add) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new CreateRole.Command(input.Name, input.Description), ct);
            ToastSuccess("Role created successfully.");
        }
        catch (ValidationException ex) { ToastError(string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (Exception ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync([FromForm] EditInput input, CancellationToken ct)
    {
        if (EnsurePermission(IdentityPermissions.Roles.Edit) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new UpdateRole.Command(input.Id, input.Name, input.Description), ct);
            ToastSuccess("Role updated successfully.");
        }
        catch (Exception ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync([FromForm] Guid id, CancellationToken ct)
    {
        if (EnsurePermission(IdentityPermissions.Roles.Delete) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new DeleteRole.Command(id), ct);
            ToastSuccess("Role deleted successfully.");
        }
        catch (Exception ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSavePermissionsAsync(
        [FromForm] Guid roleId, [FromForm] List<string>? permissions, CancellationToken ct)
    {
        if (EnsurePermission(IdentityPermissions.Roles.Edit) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new UpdateRolePermissions.Command(roleId, permissions ?? []), ct);
            ToastSuccess("Role permissions updated successfully.");
        }
        catch (Exception ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }
}
