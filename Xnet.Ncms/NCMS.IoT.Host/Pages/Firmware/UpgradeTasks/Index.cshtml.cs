using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Features.V1.Firmware.Packages;
using NCMS.IoT.DeviceManagement.Features.V1.Firmware.UpgradeTasks;
using NCMS.IoT.Host.Application;
using NCMS.IoT.Host.Models;
using NCMS.Persistence.Pagination;

namespace NCMS.IoT.Host.Pages.Firmware.UpgradeTasks;

[Authorize(Policy = DeviceManagementPermissions.UpgradeTasks.List)]
public class IndexModel : AppPageModel
{
    private readonly ISender _sender;
    public IndexModel(ISender sender) => _sender = sender;

    [BindProperty(SupportsGet = true, Name = "pageNumber")] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 25;
    [BindProperty(SupportsGet = true)] public Guid? View { get; set; }

    // Filters
    [BindProperty(SupportsGet = true)] public string? Name { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? StartDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? EndDate { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }

    public int[] PageSizeOptions { get; } = [25, 50, 100];

    [BindProperty] public Guid PackageId { get; set; }
    [BindProperty] public string TaskName { get; set; } = string.Empty;
    [BindProperty] public decimal Duration { get; set; } = 1.0m;
    [BindProperty] public List<Guid> DeviceIds { get; set; } = [];

    public PagedResponse<UpgradeTaskListItemDto> Result { get; private set; } = new();
    public PagerInfo Pager { get; private set; } = default!;

    /// <summary>Product model names for the device-filter dropdown in the create dialog.</summary>
    public List<string> ProductOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        Result = await _sender.Send(new ListUpgradeTasks.Query(
            Name, StartDate, EndDate, Sort, PageNumber, PageSize), ct);
        Pager = PagerInfo.From(Result, "/Firmware/UpgradeTasks/Index", FilterRouteValues(), "center", PageSizeOptions);

        var products = await _sender.Send(new ListUpgradeableProducts.Query(), ct);
        ProductOptions = products.Select(p => p.Name).Distinct().OrderBy(n => n).ToList();

        return PageOrPartial("_Results");
    }

    /// <summary>Active filters (plus page size and sort) preserved across sort/pagination links.</summary>
    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Name)) values["name"] = Name;
        if (StartDate is { } s) values["startDate"] = s.ToString("yyyy-MM-dd");
        if (EndDate is { } e) values["endDate"] = e.ToString("yyyy-MM-dd");
        if (!string.IsNullOrWhiteSpace(Sort)) values["sort"] = Sort;
        values["pageSize"] = PageSize.ToString();
        return values;
    }

    // ── JSON read endpoints consumed by the modals ────────────────────────────

    public async Task<IActionResult> OnGetPackagesAsync(CancellationToken ct)
    {
        var packages = await _sender.Send(new ListPackages.Query(null, OnlyEnabled: true), ct);
        return new JsonResult(packages.Select(p => new { id = p.Id, name = p.Name, version = p.Version }));
    }

    // The in-dialog device picker uses the same page-size choices as the app-wide pager.
    private static readonly int[] DevicePageSizes = [25, 50, 100];
    private const int DefaultDevicePageSize = 25;

    public async Task<IActionResult> OnGetDevicesAsync(
        Guid? packageId, string? code, string? model, string? status, [FromQuery] int page, int pageSize, CancellationToken ct)
    {
        if (!DevicePageSizes.Contains(pageSize)) pageSize = DefaultDevicePageSize;
        var result = await _sender.Send(
            new SearchDevices.Query(packageId, model, null, code, status, page < 1 ? 1 : page, pageSize), ct);
        return new JsonResult(new { items = result.Items, total = result.Total, page = page < 1 ? 1 : page, pageSize });
    }

    public async Task<IActionResult> OnGetDetailAsync(Guid id, [FromQuery] int page, int pageSize, CancellationToken ct)
    {
        try
        {
            if (!DevicePageSizes.Contains(pageSize)) pageSize = DefaultDevicePageSize;
            var task = await _sender.Send(new GetUpgradeTaskDetail.Query(id, page < 1 ? 1 : page, pageSize), ct);
            if (task is null)
                return new JsonResult(new { error = "Upgrade task not found." }) { StatusCode = StatusCodes.Status404NotFound };

            var devices = task.Devices.Select(d => new
            {
                d.Index,
                d.DeviceId,
                d.DeviceCode,
                d.ProductModel,
                d.OriginalVersion,
                d.CurrentVersion,
                d.TargetVersion,
                escalationState = d.EscalationState.ToString(),
                d.AttemptCount,
                d.DispatchedAt,
                d.CompletedAt,
                d.ErrorCode,
                d.ErrorMessage
            });

            return new JsonResult(new
            {
                id            = task.Id,
                taskName      = task.TaskName,
                firmwareName  = task.FirmwareName,
                firmwareVersion = task.FirmwareVersion,
                deviceTotal   = task.DeviceTotal,
                upgradeDurationHours = task.UpgradeDurationHours,
                taskStatus    = task.TaskStatus.ToString(),
                createdBy     = task.CreatedBy,
                createdAt     = task.CreatedAt,
                startedAt     = task.StartedAt,
                completedAt   = task.CompletedAt,
                progress = new
                {
                    total      = task.Progress.Total,
                    queued     = task.Progress.Pending,
                    inProgress = task.Progress.InProgress,
                    succeeded  = task.Progress.Succeeded,
                    failed     = task.Progress.Failed,
                    timedOut   = task.Progress.TimedOut,
                    skipped    = task.Progress.Skipped
                },
                devices
            });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return new JsonResult(new { error = ex.Message });
        }
    }

    // ── Create (POST) ─────────────────────────────────────────────────────────

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.UpgradeTasks.Add) is { } forbidden) return forbidden;
        try
        {
            var result = await _sender.Send(
                new CreateUpgradeTask.Command(PackageId, DeviceIds, TaskName, Duration), ct);
            ToastSuccess($"started successfully.");
        }
        catch (ValidationException ex)
        {
            ToastError(string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException)
        {
            ToastError(ex.Message);
        }
        return RedirectToPage(new { });
    }

    // ── Per-device abort (AJAX, consumed by the detail modal) ──────────────────
    public async Task<IActionResult> OnPostAbortDeviceAsync(Guid id, Guid deviceId, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.UpgradeTasks.Abort) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new AbortUpgradeTaskJob.Command(id, deviceId), ct);
            return new JsonResult(new { ok = true });
        }
        catch (KeyNotFoundException ex)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return new JsonResult(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return new JsonResult(new { error = ex.Message });
        }
    }

}
