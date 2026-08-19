using Mediator;
using Microsoft.AspNetCore.Mvc;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Features.V1.Dashboard;
using NCMS.IoT.Host.Application;

namespace NCMS.IoT.Host.Pages;

public class IndexModel : AppPageModel
{
    private readonly ISender _sender;
    public IndexModel(ISender sender) => _sender = sender;

    public DashboardSummaryDto Summary { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Summary = await _sender.Send(new GetDashboardSummary.Query(), ct);
    }

    public async Task<IActionResult> OnGetTrendsAsync(int days, CancellationToken ct)
    {
        var points = await _sender.Send(new GetDashboardTrends.Query(days), ct);
        return new JsonResult(points.Select(p => new
        {
            date = p.Date.ToString("MMM d"),
            newDevices = p.NewDevices,
            onlineRatePercent = p.OnlineRatePercent
        }));
    }
}
