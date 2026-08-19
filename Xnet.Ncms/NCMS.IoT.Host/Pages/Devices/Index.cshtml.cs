using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Features.V1.Devices;
using NCMS.IoT.DeviceManagement.Features.V1.Inventory;
using NCMS.IoT.Host.Application;
using NCMS.IoT.Host.Helpers;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Pages.Devices;

[Authorize(Policy = DeviceManagementPermissions.Devices.List)]
public class IndexModel : AppPageModel
{
    private readonly ISender _sender;
    public IndexModel(ISender sender) => _sender = sender;

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

    public PagedResponse<DeviceListItemDto> Result { get; private set; } = new();

    public ProductFilterOptionsDto FilterOptions { get; private set; } = new([], [], []);

    public string? LoadError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        try
        {
            FilterOptions = await _sender.Send(new ListProductFilterOptions.Query(), ct);
            Result = await _sender.Send(new ListDevices.Query(
                Search, CategoryId, TypeId, ProductId, Sort, PageNumber, PageSize), ct);
        }
        catch (Exception)
        {
            LoadError = "Unable to load devices right now. Please try again.";
        }

        return PageOrPartial("_Results");
    }

    /// <summary>
    /// Excel export of the device list. Exports every device matching the current filters —
    /// not just the page on screen — capped at <see cref="ExcelExport.MaxRows"/>.
    /// </summary>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        // Shared pagination clamps PageSize to 100, so gather the full set batch by batch.
        const int batchSize = 100;
        var devices = new List<DeviceListItemDto>();

        for (var page = 1; ; page++)
        {
            var result = await _sender.Send(new ListDevices.Query(
                Search, CategoryId, TypeId, ProductId, Sort, page, batchSize), ct);

            devices.AddRange(result.Items);

            if (result.Items.Count < batchSize || devices.Count >= ExcelExport.MaxRows) break;
        }

        if (devices.Count > ExcelExport.MaxRows)
            devices.RemoveRange(ExcelExport.MaxRows, devices.Count - ExcelExport.MaxRows);

        string[] headers =
        [
            "#", "Serial Number", "Status", "Firmware", "Agent",
            "WAN IP", "MAC Addresses", "Last Seen", "Activated"
        ];

        var rows = devices.Select((d, i) => (IReadOnlyList<object?>)new object?[]
        {
            i + 1,
            d.SerialNumber,
            // Presence (Online/Offline) mirrors the grid: the live heartbeat-derived IsOnline
            // flag, not the d.Status lifecycle string.
            d.IsOnline ? "Online" : "Offline",
            d.FirmwareVersion,
            d.AgentVersion,
            d.WanIpAddress,
            d.MacAddresses.Count == 0
                ? null
                : string.Join(", ", d.MacAddresses.Select(m => $"{m.Key}: {m.Value}")),
            d.LastSeenAt,
            d.ActivatedAt
        });

        var bytes = ExcelExport.Build("Devices", headers, rows);
        return File(bytes, ExcelExport.ContentType, ExcelExport.FileName("devices"));
    }

    /// <summary>
    /// Active filters (plus page size and current sort) preserved across the sort-header and
    /// pagination links. Consumers overwrite the key they own (sort headers overwrite "sort",
    /// the pager overwrites "page").
    /// </summary>
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
}
