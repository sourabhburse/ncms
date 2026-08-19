using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;
using NCMS.IoT.Host.Application;

namespace NCMS.IoT.Host.Pages.Configuration.Profiles;

[Authorize(Policy = DeviceManagementPermissions.ConfigProfiles.List)]
public class IndexModel : AppPageModel
{
    private readonly ISender _sender;
    private readonly FileStorageOptions _storageOptions;

    public IndexModel(ISender sender, IOptions<FileStorageOptions> storageOptions)
    {
        _sender = sender;
        _storageOptions = storageOptions.Value;
    }

    [BindProperty(SupportsGet = true, Name = "pageNumber")] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 25;
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    public int[] PageSizeOptions { get; } = [25, 50, 100];

    // Filters
    [BindProperty(SupportsGet = true)] public string? Name { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? FilterProductId { get; set; }
    [BindProperty(SupportsGet = true)] public ProfileStatus? Status { get; set; }

    public NCMS.Persistence.Pagination.PagedResponse<ConfigProfileListItemDto> Result { get; private set; } = new();
    public IReadOnlyCollection<ConfigProfileListItemDto> Profiles => Result.Items;
    public NCMS.IoT.Host.Models.PagerInfo Pager { get; private set; } = default!;
    public List<SelectListItem> ProductOptions { get; private set; } = [];

    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Name)) values["name"] = Name;
        if (FilterProductId is { } p) values["filterProductId"] = p.ToString();
        if (Status is { } s) values["status"] = s.ToString();
        if (!string.IsNullOrWhiteSpace(Sort)) values["sort"] = Sort;
        values["pageSize"] = PageSize.ToString();
        return values;
    }

    public sealed class SaveInput
    {
        public Guid Id            { get; set; }   // empty → create, otherwise → update
        public string ProfileName { get; set; } = string.Empty;
        public Guid ProductId     { get; set; }
        public string? Remark     { get; set; }
        public IFormFile? File    { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        Result = await _sender.Send(new ListConfigProfilesPaged.Query(Sort, PageNumber, PageSize, Name, FilterProductId, Status), ct);
        Pager = NCMS.IoT.Host.Models.PagerInfo.From(Result, "/Configuration/Profiles/Index", FilterRouteValues(), "center", PageSizeOptions);
        await LoadProductsAsync(ct);
        return PageOrPartial("_Results");
    }

    public async Task<IActionResult> OnGetDetailAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        try
        {
            var p = await _sender.Send(new GetProfileDetail.Query(id), ct);
            if (p is null)
                return new JsonResult(new { error = "Config profile not found." }) { StatusCode = StatusCodes.Status404NotFound };

            return new JsonResult(new
            {
                id          = p.Id,
                profileName = p.ProfileName,
                fileName    = DeriveFileName(p),
                size        = p.Size,
                productModel = p.ProductModel,
                productId   = p.ProductId,
                isEnabled   = p.Status == ProfileStatus.Enable,
                remark      = p.Remark,
                createdBy   = p.CreatedBy,
                createdAt   = p.CreatedAt,
                storagePath = p.StoragePath
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
        var p = await _sender.Send(new GetProfileDetail.Query(id), ct);
        if (p is null)
        {
            ToastError("Config profile not found.");
            return RedirectToPage();
        }

        var fileName = DeriveFileName(p);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            ToastError("This config profile has no uploaded file to download.");
            return RedirectToPage();
        }

        var safeFileName = Path.GetFileName(fileName);
        var storageRoot = Path.GetFullPath(_storageOptions.StoragePath);
        var filePath = Path.GetFullPath(Path.Combine(storageRoot, $"{p.Id}_{safeFileName}"));

        if (!filePath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(filePath))
        {
            ToastError("The config file could not be found on the server.");
            return RedirectToPage();
        }

        return PhysicalFile(filePath, "application/octet-stream", safeFileName);
    }

    public async Task<IActionResult> OnPostSaveAsync([FromForm] SaveInput input, CancellationToken ct)
    {
        var requiredPermission = input.Id == Guid.Empty
            ? DeviceManagementPermissions.ConfigProfiles.Add
            : DeviceManagementPermissions.ConfigProfiles.Edit;
        if (EnsurePermission(requiredPermission) is { } forbidden) return forbidden;
        if (input.ProductId == Guid.Empty)
        {
            ToastError("Select a product model that supports configuration.");
            return RedirectToPage();
        }

        var hasFile = input.File is { Length: > 0 };

        try
        {
            if (input.Id == Guid.Empty)
            {
                if (!hasFile)
                {
                    ToastError("Choose a config file to upload.");
                    return RedirectToPage();
                }

                var result = await _sender.Send(new CreateProfile.Command(
                    input.ProfileName, input.ProductId, input.Remark), ct);

                await _sender.Send(new UploadProfileFile.Command(result.Id, input.File!), ct);
                ToastSuccess($"saved successfully.");
            }
            else
            {
                var result = await _sender.Send(new UpdateProfile.Command(
                    input.Id, input.ProfileName, input.ProductId, input.Remark), ct);

                if (hasFile)
                    await _sender.Send(new UploadProfileFile.Command(result.Id, input.File!), ct);

                ToastSuccess($"updated successfully.");
            }
        }
        catch (ValidationException ex) { ToastError(string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (KeyNotFoundException ex) { ToastError(ex.Message); }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync([FromForm] Guid id, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.ConfigProfiles.Delete) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new DeleteProfile.Command(id), ct);
            ToastSuccess("deleted successfully.");
        }
        catch (KeyNotFoundException ex) { ToastError(ex.Message); }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync([FromForm] Guid id, [FromForm] bool enable, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.ConfigProfiles.EnableDisable) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new SetProfileEnabled.Command(id, enable), ct);
            ToastSuccess(enable ? "enabled successfully." : "disabled successfully.");
        }
        catch (KeyNotFoundException ex) { ToastError(ex.Message); }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCopyAsync([FromForm] Guid id, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.ConfigProfiles.Copy) is { } forbidden) return forbidden;
        try
        {
            var copy = await _sender.Send(new CopyProfile.Command(id), ct);
            ToastSuccess($"Copied to '{copy.ProfileName}' (disabled).");
        }
        catch (KeyNotFoundException ex) { ToastError(ex.Message); }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    private async Task LoadProductsAsync(CancellationToken ct)
    {
        var products = await _sender.Send(new ListConfigurableProducts.Query(), ct);
        ProductOptions = products
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToList();
    }

    private static string DeriveFileName(ConfigProfileDetailDto p)
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
