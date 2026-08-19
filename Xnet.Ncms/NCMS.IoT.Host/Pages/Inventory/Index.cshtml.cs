using System.Text.Json;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Features.V1.Inventory;
using NCMS.IoT.Host.Application;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Pages.Inventory;

[Authorize(Policy = DeviceManagementPermissions.Inventory.List)]
public class IndexModel : AppPageModel
{
    // Client-side JS sends camelCase JSON; System.Text.Json defaults to case-sensitive
    // matching against our PascalCase row class, so this is required or claim entries
    // silently deserialize with empty Key/Value.
    private static readonly JsonSerializerOptions ClaimsJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ISender _sender;
    public IndexModel(ISender sender) => _sender = sender;

    // ── Filters / paging (GET) ──────────────────────────────────────────────────
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? TypeId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    public int[] PageSizeOptions { get; } = [25, 50, 100];

    // ── Page data ─────────────────────────────────────────────────────────────
    public PagedResponse<HardwareInventoryDto> Result { get; private set; } = new();
    public IReadOnlyCollection<HardwareInventoryDto> Items => Result.Items;
    public ProductFilterOptionsDto FilterOptions { get; private set; } = new([], [], []);
    public List<SelectListItem> ProductOptions { get; private set; } = [];

    // ── Input models ──────────────────────────────────────────────────────────
    public sealed class CreateInput
    {
        public Guid   ProductId      { get; set; }
        public string SerialNumber   { get; set; } = string.Empty;
        public string IdentityPolicy { get; set; } = "serial_only";
        public string? ClaimsJson    { get; set; }
    }

    public sealed class EditInput
    {
        public Guid   Id            { get; set; }
        public Guid   ProductId      { get; set; }
        public string SerialNumber   { get; set; } = string.Empty;
        public string IdentityPolicy { get; set; } = "serial_only";
        public string? ClaimsJson   { get; set; }
    }

    // ── GET ───────────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        FilterOptions = await _sender.Send(new ListProductFilterOptions.Query(), ct);
        Result = await _sender.Send(new ListHardwareInventory.Query(
            Search, CategoryId, TypeId, ProductId, Sort, PageNumber, PageSize), ct);
        await LoadProductsAsync(ct);

        return PageOrPartial("_Results");
    }

    /// <summary>Active filters (plus page size and sort) preserved across sort/pagination links.</summary>
    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Search)) values["search"] = Search;
        if (CategoryId is { } c) values["categoryId"] = c.ToString();
        if (TypeId is { } t) values["typeId"] = t.ToString();
        if (ProductId is { } p) values["productId"] = p.ToString();
        if (!string.IsNullOrWhiteSpace(Sort)) values["sort"] = Sort;
        values["pageSize"] = PageSize.ToString();
        return values;
    }

    // ── POST: Create ──────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostCreateAsync([FromForm] CreateInput input, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.Inventory.Add) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new AddHardwareInventory.Command(
                input.SerialNumber,
                input.ProductId,
                input.IdentityPolicy,
                ParseClaims(input.ClaimsJson)), ct);
            ToastSuccess("saved successfully.");
        }
        catch (ValidationException ex) { ToastError(string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    // ── POST: Edit ────────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostEditAsync([FromForm] EditInput input, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.Inventory.Edit) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new UpdateHardwareInventory.Command(
                input.Id,
                input.SerialNumber,
                input.ProductId,
                input.IdentityPolicy,
                ParseClaims(input.ClaimsJson) ?? new()), ct);
            ToastSuccess("updated successfully.");
        }
        catch (ValidationException ex) { ToastError(string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage))); }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    // ── POST: Delete ──────────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostDeleteAsync([FromForm] Guid id, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.Inventory.Delete) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new DeleteHardwareInventory.Command(id), ct);
            ToastSuccess("deleted successfully.");
        }
        catch (InvalidOperationException ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    // ── POST: BatchDelete ─────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostBatchDeleteAsync([FromForm] string? idsJson, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.Inventory.Delete) is { } forbidden) return forbidden;
        var ids = ParseGuids(idsJson);
        if (ids.Count == 0) { ToastError("No records selected."); return RedirectToPage(); }

        try
        {
            var deleted = await _sender.Send(new BatchDeleteHardwareInventory.Command(ids), ct);
            ToastSuccess($"{deleted} {(deleted == 1 ? "hardware item" : "hardware items")} deleted successfully.");
        }
        catch (Exception ex) { ToastError(ex.Message); }
        return RedirectToPage();
    }

    // ── GET: CSV template ─────────────────────────────────────────────────────
    public async Task<IActionResult> OnGetTemplateAsync(Guid productId, CancellationToken ct)
    {
        if (productId == Guid.Empty) return BadRequest();

        var products = await _sender.Send(new ListProducts.Query(), ct);
        var product = products.FirstOrDefault(p => p.Id == productId);
        if (product is null) return NotFound();

        var csv = "SerialNumber,IdentityPolicy\r\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"hardware-template-{product.Name.Replace(" ", "-")}.csv");
    }

    // ── POST: CSV import ──────────────────────────────────────────────────────
    public async Task<IActionResult> OnPostImportAsync(
        [FromForm] Guid productId,
        IFormFile? file,
        CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.Inventory.Upload) is { } forbidden) return forbidden;
        if (productId == Guid.Empty || file is null || file.Length == 0)
        {
            ToastError("Choose a product and a .csv file to import.");
            return RedirectToPage();
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var lines = (await reader.ReadToEndAsync(ct))
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1) // skip header
                .ToList();

            var count = 0;
            var errors = new List<string>();
            foreach (var line in lines)
            {
                var cols = line.Trim().Split(',');
                if (cols.Length < 1) continue;
                try
                {
                    await _sender.Send(new AddHardwareInventory.Command(
                        cols[0].Trim(),
                        productId,
                        cols.Length > 1 ? cols[1].Trim() : "serial_only"), ct);
                    count++;
                }
                catch (Exception ex) { errors.Add($"Row '{cols[0]}': {ex.Message}"); }
            }

            if (errors.Count == 0)
                ToastSuccess($"Imported {count} {(count == 1 ? "hardware item" : "hardware items")} successfully.");
            else
                ToastError($"Imported {count}, {errors.Count} failed: " + string.Join(" | ", errors.Take(3)));
        }
        catch (Exception ex) { ToastError($"Import failed: {ex.Message}"); }

        return RedirectToPage();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task LoadProductsAsync(CancellationToken ct)
    {
        var products = await _sender.Send(new ListProducts.Query(), ct);
        ProductOptions = products
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToList();
    }

    private static Dictionary<string, string?>? ParseClaims(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var entries = JsonSerializer.Deserialize<List<ClaimEntry>>(json, ClaimsJsonOptions) ?? [];
            return entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Key))
                .ToDictionary(e => e.Key.Trim(), e => (string?)e.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException) { return null; }
    }

    private static List<Guid> ParseGuids(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<Guid>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private sealed class ClaimEntry { public string Key { get; set; } = ""; public string? Value { get; set; } }
}
