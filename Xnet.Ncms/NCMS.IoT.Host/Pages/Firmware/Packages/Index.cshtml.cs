using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;
using NCMS.IoT.Host.Application;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Pages.Firmware.Packages;

[Authorize(Policy = DeviceManagementPermissions.Packages.List)]
public class IndexModel : AppPageModel
{
    private readonly ISender _sender;
    private readonly FileStorageOptions _firmwareOptions;

    public IndexModel(ISender sender, IOptions<FileStorageOptions> firmwareOptions)
    {
        _sender = sender;
        _firmwareOptions = firmwareOptions.Value;
    }

    // ── Filters / paging (GET) ──────────────────────────────────────────────────
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? Enabled { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    public int[] PageSizeOptions { get; } = [25, 50, 100];

    // ── Page data ─────────────────────────────────────────────────────────────
    public PagedResponse<FirmwarePackageListItemDto> Result { get; private set; } = new();
    public IReadOnlyCollection<FirmwarePackageListItemDto> Packages => Result.Items;
    public List<SelectListItem> ProductOptions { get; private set; } = [];

    // ── Input model ───────────────────────────────────────────────────────────
    public sealed class SaveInput
    {
        public Guid Id            { get; set; }   // empty → create, otherwise → update
        public string Name        { get; set; } = string.Empty;
        public string Version     { get; set; } = string.Empty;
        public Guid ProductId     { get; set; }
        public string? Remark     { get; set; }
        public IFormFile? File    { get; set; }
    }

    // ── GET ───────────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        Result = await _sender.Send(new ListFirmwarePackagesPaged.Query(
            Name, Enabled, ProductId, Sort, PageNumber, PageSize), ct);
        await LoadProductsAsync(ct);

        return PageOrPartial("_Results");
    }

    /// <summary>Active filters (plus page size and sort) preserved across sort/pagination links.</summary>
    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Name)) values["name"] = Name;
        if (Enabled is { } e) values["enabled"] = e ? "true" : "false";
        if (ProductId is { } p) values["productId"] = p.ToString();
        if (!string.IsNullOrWhiteSpace(Sort)) values["sort"] = Sort;
        values["pageSize"] = PageSize.ToString();
        return values;
    }

    // ── JSON read endpoint consumed by the View modal ─────────────────────────
    public async Task<IActionResult> OnGetDetailAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        try
        {
            var p = await _sender.Send(new GetPackageDetail.Query(id), ct);
            if (p is null)
                return new JsonResult(new { error = "Firmware package not found." }) { StatusCode = StatusCodes.Status404NotFound };

            return new JsonResult(new
            {
                id            = p.Id,
                name          = p.Name,
                fileName      = DeriveFileName(p),
                size          = p.Size,
                version       = p.Version,
                type          = p.Type.ToString(),
                createdBy     = p.CreatedBy,
                createdAt     = p.CreatedAt,
                remark        = p.Remark,
                isEnabled     = p.IsEnabled,
                storagePath   = p.StoragePath,
                productModels = p.ProductModels,
                productId     = p.ProductIds.FirstOrDefault()
            });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return new JsonResult(new { error = ex.Message });
        }
    }

    public async Task<IActionResult> OnGetDownloadAsync(Guid id, CancellationToken ct)
    {
        var p = await _sender.Send(new GetPackageDetail.Query(id), ct);
        if (p is null)
        {
            ToastError("Firmware package not found.");
            return RedirectToPage();
        }

        var fileName = DeriveFileName(p);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            ToastError("This package has no uploaded file to download.");
            return RedirectToPage();
        }

        var safeFileName = Path.GetFileName(fileName);
        var storageRoot = Path.GetFullPath(_firmwareOptions.StoragePath);
        var filePath = Path.GetFullPath(Path.Combine(storageRoot, $"{p.Id}_{safeFileName}"));

        if (!filePath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(filePath))
        {
            ToastError("The package file could not be found on the server.");
            return RedirectToPage();
        }

        return PhysicalFile(filePath, "application/octet-stream", safeFileName);
    }

    // ── POST: Save (create when Id is empty, otherwise update) ────────────────
    public async Task<IActionResult> OnPostSaveAsync([FromForm] SaveInput input, CancellationToken ct)
    {
        var requiredPermission = input.Id == Guid.Empty
            ? DeviceManagementPermissions.Packages.Add
            : DeviceManagementPermissions.Packages.Edit;
        if (EnsurePermission(requiredPermission) is { } forbidden) return forbidden;
        if (input.ProductId == Guid.Empty)
        {
            ToastError("Select a product model that supports upgrade.");
            return RedirectToPage();
        }

        var hasFile = input.File is { Length: > 0 };

        try
        {
            if (input.Id == Guid.Empty)
            {
                // Create — the binary is required.
                if (!hasFile)
                {
                    ToastError("Choose an upgrade file to upload.");
                    return RedirectToPage();
                }

                var result = await _sender.Send(new CreatePackage.Command(
                    input.Version,
                    input.Name,
                    input.Remark,
                    string.Empty,            // ReleaseNotes — not collected
                    null,                    // DeviceTypeCode — eligibility is driven by product models
                    null,                    // MinRequiredFirmwareVersion
                    [input.ProductId]), ct);

                await _sender.Send(new UploadPackageBinary.Command(result.Id, input.File!), ct);
                ToastSuccess($"saved successfully.");
            }
            else
            {
                // Update — the binary is optional (only replaced when a new file is chosen).
                var result = await _sender.Send(new UpdatePackage.Command(
                    input.Id, input.Name, input.Version, input.Remark, input.ProductId), ct);

                if (hasFile)
                    await _sender.Send(new UploadPackageBinary.Command(result.Id, input.File!), ct);

                ToastSuccess($"updated successfully.");
            }
        }
        catch (ValidationException ex) { ToastError(string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (KeyNotFoundException ex) { ToastError(ex.Message); }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    // ── POST: Delete ──────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostDeleteAsync([FromForm] Guid id, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.Packages.Delete) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new DeletePackage.Command(id), ct);
            ToastSuccess("deleted successfully.");
        }
        catch (KeyNotFoundException ex) { ToastError(ex.Message); }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    // ── POST: Enable / Disable ────────────────────────────────────────────────
    public async Task<IActionResult> OnPostToggleAsync([FromForm] Guid id, [FromForm] bool enable, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.Packages.EnableDisable) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new SetPackageEnabled.Command(id, enable), ct);
            ToastSuccess(enable ? "enabled successfully." : "disabled successfully.");
        }
        catch (KeyNotFoundException ex) { ToastError(ex.Message); }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task LoadProductsAsync(CancellationToken ct)
    {
        var products = await _sender.Send(new ListUpgradeableProducts.Query(), ct);
        ProductOptions = products
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToList();
    }

    /// <summary>The original upload file name, derived from the stored path.</summary>
    private static string DeriveFileName(FirmwarePackageDetailDto p)
    {
        if (!string.IsNullOrEmpty(p.FileName)) return p.FileName;
        if (string.IsNullOrEmpty(p.StoragePath)) return string.Empty;

        var segment = p.StoragePath[(p.StoragePath.LastIndexOf('/') + 1)..];
        var prefix = $"{p.Id}_";
        return segment.StartsWith(prefix, StringComparison.Ordinal)
            ? segment[prefix.Length..]
            : segment;
    }
}
