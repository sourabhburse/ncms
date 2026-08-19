using System.Text.Json;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;
using NCMS.IoT.Host.Application;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Pages.AppPackages.Packages;

/// <summary>
/// Merged Application Packages page — a single flat list of every package version across all
/// packages, mirroring the Firmware Packages page. Replaces the former two-page split
/// (packages list + per-package versions list). Each row is one version; "Add Package" creates a
/// version (uploading its artifact in the same submit) and, when the name is new, its owning
/// package too. Create and Edit share one unified single-Save modal.
/// </summary>
[Authorize(Policy = DeviceManagementPermissions.ApplicationPackages.List)]
public class IndexModel : AppPageModel
{
    // Client-side JS sends camelCase JSON; System.Text.Json defaults to case-sensitive matching
    // against our PascalCase row classes, so this is required or every field silently
    // deserializes to its default value (Guid.Empty, etc).
    private static readonly JsonSerializerOptions ItemsJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ISender _sender;
    public IndexModel(ISender sender) => _sender = sender;

    // ── Filters / paging (GET) ──────────────────────────────────────────────────
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Version { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? Enabled { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Tag { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    public int[] PageSizeOptions { get; } = [25, 50, 100];

    public PagedResponse<ApplicationPackageVersionListItemDto> Result { get; private set; } = new();
    public IReadOnlyCollection<ApplicationPackageVersionListItemDto> Versions => Result.Items;
    public List<SelectListItem> ProductOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        var products = await _sender.Send(new ListApplicationCapableProducts.Query(), ct);
        ProductOptions = products
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToList();

        Result = await _sender.Send(new ListApplicationPackageVersionsPaged.Query(
            Name, Version, Enabled, ProductId, Tag, Sort, PageNumber, PageSize), ct);
        return PageOrPartial("_Results");
    }

    /// <summary>Active filters (plus page size, sort) preserved across sort/pagination links.</summary>
    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Name)) values["name"] = Name;
        if (!string.IsNullOrWhiteSpace(Version)) values["version"] = Version;
        if (Enabled is { } e) values["enabled"] = e ? "true" : "false";
        if (ProductId is { } p) values["productId"] = p.ToString();
        if (!string.IsNullOrWhiteSpace(Tag)) values["tag"] = Tag;
        if (!string.IsNullOrWhiteSpace(Sort)) values["sort"] = Sort;
        values["pageSize"] = PageSize.ToString();
        return values;
    }

    // ── JSON read endpoints consumed by the modals ────────────────────────────

    public async Task<IActionResult> OnGetDetailAsync(Guid id, CancellationToken ct)
    {
        var v = await _sender.Send(new GetApplicationPackageVersionDetail.Query(id), ct);
        return v is null ? NotFound() : new JsonResult(v);
    }

    public async Task<IActionResult> OnGetOtherPackagesAsync(CancellationToken ct)
    {
        var packages = await _sender.Send(new ListApplicationPackages.Query(null), ct);
        return new JsonResult(packages.Select(p => new { id = p.Id, name = p.Name }));
    }

    // ── Mutations ──────────────────────────────────────────────────────────────

    /// <summary>
    /// "Add Package": create a version and (when the name is new) its owning package, then
    /// upload the artifact — all from one form submit. Re-using an existing name appends a new
    /// version to that package (the Firmware model). The version is created Disabled; enabling
    /// stays a deliberate follow-up step (its prerequisites — a binary and a compatible product
    /// — are already satisfied here).
    /// </summary>
    public async Task<IActionResult> OnPostCreatePackageAsync(
        [FromForm] string name, [FromForm] List<string>? tags,
        [FromForm] string version, [FromForm] string packageFormat,
        [FromForm] string? releaseNotes,
        [FromForm] List<Guid>? productIds, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.ApplicationPackages.Add) is { } forbidden) return forbidden;
        if (file is not { Length: > 0 })
        {
            ToastError("Choose a package file to upload.");
            return RedirectToPage();
        }

        try
        {
            var result = await _sender.Send(new CreateApplicationPackageWithVersion.Command(
                name, tags ?? [], version, packageFormat, releaseNotes, productIds ?? []), ct);
            await _sender.Send(new UploadApplicationPackageVersionBinary.Command(result.Id, file), ct);
            ToastSuccess($"saved (Disabled) successfully.");
        }
        catch (ValidationException ex) { ToastError(string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    /// <summary>
    /// Unified single-Save for the Edit modal — mirrors the Create form's one-submit UX.
    /// Persists everything editable in one action: package name/tags, release notes, an
    /// optional artifact replacement, and dependencies (all editable regardless of enabled
    /// state), plus compatibility — which is only applied while the version is Disabled (the
    /// Product Model field is locked in the UI when Enabled, and this guard keeps a save of an
    /// enabled version from tripping the compatibility "disable first" rule).
    /// </summary>
    public async Task<IActionResult> OnPostSaveVersionAsync(
        [FromForm] Guid packageId, [FromForm] Guid id,
        [FromForm] string name, [FromForm] List<string>? tags,
        [FromForm] string? releaseNotes, [FromForm] List<Guid>? productIds,
        [FromForm] string? dependenciesJson, [FromForm] IFormFile? file, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.ApplicationPackages.Edit) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new UpdateApplicationPackage.Command(packageId, name, tags ?? []), ct);
            await _sender.Send(new UpdateApplicationPackageVersionDetails.Command(id, releaseNotes), ct);

            if (file is { Length: > 0 })
                await _sender.Send(new UploadApplicationPackageVersionBinary.Command(id, file), ct);

            var raw = string.IsNullOrWhiteSpace(dependenciesJson)
                ? []
                : JsonSerializer.Deserialize<List<DependencyRow>>(dependenciesJson, ItemsJsonOptions) ?? [];
            var deps = raw
                .Where(d => d.DependsOnApplicationPackageId != Guid.Empty)
                .Select(d => (d.DependsOnApplicationPackageId, d.VersionConstraint))
                .ToList();
            await _sender.Send(new SetApplicationPackageVersionDependencies.Command(id, deps), ct);

            // Compatibility is locked while enabled — only apply it when the version is disabled.
            var current = await _sender.Send(new GetApplicationPackageVersionDetail.Query(id), ct);
            if (current is { IsEnabled: false })
                await _sender.Send(new SetApplicationPackageVersionCompatibility.Command(id, productIds ?? []), ct);

            ToastSuccess("saved successfully.");
        }
        catch (ValidationException ex) { ToastError(string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync([FromForm] Guid id, [FromForm] bool enable, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.ApplicationPackages.EnableDisable) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new SetApplicationPackageVersionEnabled.Command(id, enable), ct);
            ToastSuccess(enable ? "enabled successfully." : "disabled successfully.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync([FromForm] Guid id, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.ApplicationPackages.Delete) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new DeleteApplicationPackageVersion.Command(id), ct);
            ToastSuccess("deleted successfully.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    private sealed class DependencyRow
    {
        public Guid DependsOnApplicationPackageId { get; set; }
        public string? VersionConstraint { get; set; }
    }
}
