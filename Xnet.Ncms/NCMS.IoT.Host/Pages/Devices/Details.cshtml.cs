using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Features.V1.Devices;
using NCMS.IoT.DeviceManagement.Features.V1.Telemetry;
using NCMS.IoT.Host.Helpers;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Pages.Devices;

[Authorize(Policy = DeviceManagementPermissions.Devices.View)]
public class DetailsModel : PageModel
{
    private readonly ISender _sender;
    public DetailsModel(ISender sender) => _sender = sender;

    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "overview";
    [BindProperty(SupportsGet = true)] public string? Serial { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? StartDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? EndDate { get; set; }
    [BindProperty(SupportsGet = true, Name = "pageNumber")] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true, Name = "page")] public int? LegacyPageNumber { get; set; }
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 50;

    public DeviceDetailDto? Device { get; private set; }

    /// <summary>
    /// Raw telemetry records for this device (newest first). The History &amp; Analysis
    /// table parses each record's PayloadJson on the client and discovers its columns
    /// at runtime — no fixed telemetry schema.
    /// </summary>
    public IReadOnlyList<TelemetryRecordDto> Telemetry { get; private set; } = [];
    public bool HasMore { get; private set; }
    public int Total { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));
    public NCMS.IoT.Host.Models.PagerInfo Pager { get; private set; } = new()
    {
        Page = 1,
        PageSize = 50,
        Total = 0,
        PageName = "/Devices/Details",
        PageSizeOptions = PageSizeOptions,
        Framed = false
    };
    public string? LoadError { get; private set; }

    public static readonly int[] PageSizeOptions = { 20, 50, 100 };

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        PageNumber = LegacyPageNumber ?? PageNumber;
        if (PageNumber <= 0) PageNumber = 1;
        if (PageSize <= 0) PageSize = 50;

        try
        {
            var device = await _sender.Send(new GetDeviceDetail.Query(Id), ct);
            if (device is null) return NotFound();
            Device = device;

            // Load telemetry regardless of the active tab: the tab switch is client-side
            // (x-show), so the data must already be present when "History & Analysis" is shown.
            // Read the raw telemetry records (telemetry_records); the view parses them.
            // DateOnly -> DateTimeOffset with Kind=Unspecified picks up the server's local
            // offset, matching what the date inputs mean to the user. End date is inclusive
            // of the whole day, so the query uses the start of the following day as an
            // exclusive upper bound.
            DateTimeOffset? startDate = StartDate.HasValue
                ? new DateTimeOffset(StartDate.Value.ToDateTime(TimeOnly.MinValue))
                : null;
            DateTimeOffset? endDateExclusive = EndDate.HasValue
                ? new DateTimeOffset(EndDate.Value.ToDateTime(TimeOnly.MinValue).AddDays(1))
                : null;

            var result = await _sender.Send(new ListTelemetry.Query(
                Id,
                PageNumber,
                PageSize,
                string.IsNullOrWhiteSpace(Serial) ? null : Serial.Trim(),
                startDate,
                endDateExclusive), ct);
            Telemetry = result.Items;
            Total = result.Total;
            HasMore = Telemetry.Count == PageSize;
            Pager = NCMS.IoT.Host.Models.PagerInfo.From(
                new PagedResponse<TelemetryRecordDto>
                {
                    Items = result.Items,
                    PageNumber = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.Total
                },
                "/Devices/Details",
                new Dictionary<string, string?>
                {
                    ["id"] = Id.ToString(),
                    ["tab"] = "history",
                    ["serial"] = Serial,
                    ["startDate"] = StartDate?.ToString("yyyy-MM-dd"),
                    ["endDate"] = EndDate?.ToString("yyyy-MM-dd"),
                    ["pageSize"] = PageSize.ToString()
                },
                "center",
                PageSizeOptions,
                framed: false);
        }
        catch (Exception ex)
        {
            LoadError = "Unable to load device details. Please try again.";
            _ = ex;
        }

        if (Request.Headers.ContainsKey("X-Partial-Table"))
            return Partial("_HistoryResults", this);

        return Page();
    }

    /// <summary>
    /// Excel export of the History &amp; Analysis tab. Exports every telemetry record matching the
    /// current filters — not just the page on screen — capped at <see cref="ExcelExport.MaxRows"/>.
    /// </summary>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken ct)
    {
        var device = await _sender.Send(new GetDeviceDetail.Query(Id), ct);
        if (device is null) return NotFound();

        var records = await LoadAllTelemetryAsync(ct);
        var table = TelemetryTable.Flatten(records);

        var headers = new List<string> { "#", "Timestamp" };
        headers.AddRange(table.Columns.Select(TelemetryTable.Label));

        var rows = table.Rows.Select((row, i) =>
        {
            var cells = new List<object?> { i + 1, row.Timestamp };
            cells.AddRange(table.Columns.Select(c => row.Values.GetValueOrDefault(c)));
            return (IReadOnlyList<object?>)cells;
        });

        var bytes = ExcelExport.Build("Telemetry", headers, rows);
        var serial = string.IsNullOrWhiteSpace(device.SerialNumber) ? "device" : device.SerialNumber;
        return File(bytes, ExcelExport.ContentType, ExcelExport.FileName($"telemetry_{serial}"));
    }

    /// <summary>
    /// Pages through the telemetry query to gather the full filtered set: the handler clamps
    /// PageSize to 200, so a single oversized request cannot return everything.
    /// </summary>
    private async Task<List<TelemetryRecordDto>> LoadAllTelemetryAsync(CancellationToken ct)
    {
        const int batchSize = 200;

        DateTimeOffset? startDate = StartDate.HasValue
            ? new DateTimeOffset(StartDate.Value.ToDateTime(TimeOnly.MinValue))
            : null;
        DateTimeOffset? endDateExclusive = EndDate.HasValue
            ? new DateTimeOffset(EndDate.Value.ToDateTime(TimeOnly.MinValue).AddDays(1))
            : null;

        var serial = string.IsNullOrWhiteSpace(Serial) ? null : Serial.Trim();
        var all = new List<TelemetryRecordDto>();

        for (var page = 1; ; page++)
        {
            var result = await _sender.Send(
                new ListTelemetry.Query(Id, page, batchSize, serial, startDate, endDateExclusive), ct);

            all.AddRange(result.Items);

            if (result.Items.Count < batchSize || all.Count >= ExcelExport.MaxRows) break;
        }

        if (all.Count > ExcelExport.MaxRows)
            all.RemoveRange(ExcelExport.MaxRows, all.Count - ExcelExport.MaxRows);

        return all;
    }
}