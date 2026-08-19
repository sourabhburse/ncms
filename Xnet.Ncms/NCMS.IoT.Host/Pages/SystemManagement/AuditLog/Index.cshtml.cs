using System.ComponentModel.DataAnnotations;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NCMS.IoT.Identity.Contracts.Dtos;
using NCMS.IoT.Identity.Contracts.Enums;
using NCMS.IoT.Identity.Contracts.Permissions;
using NCMS.IoT.Identity.Features.V1.Audit;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Pages.SystemManagement.AuditLog;

[Authorize(Policy = IdentityPermissions.AuditLogs.List)]
public class IndexModel : PageModel
{
    private readonly ISender _sender;
    public IndexModel(ISender sender) => _sender = sender;

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public AuditEventType? EventType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    public int[] PageSizeOptions { get; } = [25, 50, 100];

    public PagedResponse<AuditLogDto> Result { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        var fromOffset = From is { } f ? new DateTimeOffset(f) : (DateTimeOffset?)null;
        var toOffset = To is { } t ? new DateTimeOffset(t.AddDays(1)) : (DateTimeOffset?)null;

        Result = await _sender.Send(
            new ListAuditLogs.Query(
                EventType is { } e ? [e] : null, Search, fromOffset, toOffset, PageNumber, PageSize), ct);
    }

    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (EventType is { } e) values["eventType"] = e.ToString();
        if (!string.IsNullOrWhiteSpace(Search)) values["search"] = Search;
        if (From is { } f) values["from"] = f.ToString("yyyy-MM-dd");
        if (To is { } t) values["to"] = t.ToString("yyyy-MM-dd");
        values["pageSize"] = PageSize.ToString();
        return values;
    }
}
