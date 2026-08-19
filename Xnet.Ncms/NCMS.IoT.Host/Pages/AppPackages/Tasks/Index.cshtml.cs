using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Packages;
using NCMS.IoT.DeviceManagement.Features.V1.AppPackages.Tasks;
using NCMS.IoT.Host.Application;
using NCMS.IoT.Host.Models;

namespace NCMS.IoT.Host.Pages.AppPackages.Tasks;

[Authorize(Policy = DeviceManagementPermissions.ApplicationTasks.List)]
public class IndexModel : AppPageModel
{
    // The in-dialog device search uses a fixed page size (matched by the modal's client pager).
    // The in-dialog device picker uses the same page-size choices as the app-wide pager.
    private static readonly int[] DevicePageSizes = [25, 50, 100];
    private const int DefaultDevicePageSize = 25;
    private readonly ISender _sender;
    public IndexModel(ISender sender) => _sender = sender;

    [BindProperty(SupportsGet = true, Name = "pageNumber")] public int PageNumber { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int PageSize { get; set; } = 25;
    [BindProperty(SupportsGet = true)] public string? Sort { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? View { get; set; }

    // Filters
    [BindProperty(SupportsGet = true)] public string? Name { get; set; }
    [BindProperty(SupportsGet = true, Name = "action")] public ApplicationTaskAction? FilterAction { get; set; }
    [BindProperty(SupportsGet = true)] public ApplicationTaskStatus? Status { get; set; }

    public int[] PageSizeOptions { get; } = [25, 50, 100];

    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Name)) values["name"] = Name;
        if (FilterAction is { } a) values["action"] = a.ToString();
        if (Status is { } s) values["status"] = s.ToString();
        if (!string.IsNullOrWhiteSpace(Sort)) values["sort"] = Sort;
        values["pageSize"] = PageSize.ToString();
        return values;
    }

    [BindProperty] public ApplicationTaskAction Action { get; set; }
    [BindProperty] public Guid ApplicationPackageVersionId { get; set; }
    [BindProperty] public string TaskName { get; set; } = string.Empty;
    [BindProperty] public decimal TimeoutHours { get; set; } = 1.0m;
    [BindProperty] public List<Guid> DeviceIds { get; set; } = [];

    public ApplicationTaskPagedResult Result { get; private set; } = default!;
    public PagerInfo Pager { get; private set; } = default!;

    /// <summary>Product model names for the device-filter dropdown in the create dialog.</summary>
    public List<string> ProductOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        Result = await _sender.Send(new ListApplicationTasks.Query(PageNumber, PageSize, Sort, Name, FilterAction, Status), ct);
        Pager = new PagerInfo
        {
            Page = Result.Page,
            PageSize = Result.PageSize,
            Total = Result.Total,
            PageName = "/AppPackages/Tasks/Index",
            Align = "center",
            PageSizeOptions = PageSizeOptions,
            Query = FilterRouteValues()
        };

        var products = await _sender.Send(new ListApplicationCapableProducts.Query(), ct);
        ProductOptions = products.Select(p => p.Name).Distinct().OrderBy(n => n).ToList();

        return PageOrPartial("_Results");
    }

    // ── JSON read endpoints consumed by the modals ────────────────────────────

    public async Task<IActionResult> OnGetVersionsAsync(CancellationToken ct)
    {
        var versions = await _sender.Send(new ListApplicationPackageVersions.Query(null, DeployableOnly: true), ct);
        return new JsonResult(versions.Select(v => new
        {
            id = v.Id, name = $"{v.PackageName} {v.Version}", version = v.Version, format = v.PackageFormat
        }));
    }

    public async Task<IActionResult> OnGetDevicesAsync(
        Guid? versionId, string? code, string? model, string? status,
        [FromQuery] int page, int pageSize, CancellationToken ct)
    {
        if (!DevicePageSizes.Contains(pageSize)) pageSize = DefaultDevicePageSize;
        var result = await _sender.Send(
            new SearchApplicationDevices.Query(versionId, model, code, status, page < 1 ? 1 : page, pageSize), ct);
        return new JsonResult(new { items = result.Items, total = result.Total, page = page < 1 ? 1 : page, pageSize });
    }

    public async Task<IActionResult> OnGetDetailAsync(Guid id, [FromQuery] int page, int pageSize, CancellationToken ct)
    {
        try
        {
            if (!DevicePageSizes.Contains(pageSize)) pageSize = DefaultDevicePageSize;
            var task = await _sender.Send(new GetApplicationTaskDetail.Query(id, page < 1 ? 1 : page, pageSize), ct);
            if (task is null)
                return new JsonResult(new { error = "Application task not found." }) { StatusCode = StatusCodes.Status404NotFound };

            var devices = task.Devices.Select(d => new
            {
                d.Index,
                d.DeviceId,
                d.DeviceCode,
                d.ProductModel,
                escalationState = d.EscalationState.ToString(),
                d.AttemptCount,
                d.DispatchedAt,
                d.CompletedAt,
                d.ErrorCode,
                d.ErrorMessage,
                d.PackageName,
                d.Version
            });

            return new JsonResult(new
            {
                id           = task.Id,
                taskName     = task.TaskName,
                action       = task.Action.ToString(),
                targetName   = task.TargetName,
                deviceTotal  = task.DeviceTotal,
                timeoutHours = task.TimeoutHours,
                taskStatus   = task.TaskStatus.ToString(),
                createdBy    = task.CreatedBy,
                createdAt    = task.CreatedAt,
                startedAt    = task.StartedAt,
                completedAt  = task.CompletedAt,
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
        if (EnsurePermission(DeviceManagementPermissions.ApplicationTasks.Add) is { } forbidden) return forbidden;
        try
        {
            var result = await _sender.Send(new CreateApplicationTask.Command(
                Action, ApplicationPackageVersionId, DeviceIds, TaskName, TimeoutHours), ct);
            ToastSuccess($"satrted successfully.");
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
        if (EnsurePermission(DeviceManagementPermissions.ApplicationTasks.Abort) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new AbortApplicationTaskJob.Command(id, deviceId), ct);
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
