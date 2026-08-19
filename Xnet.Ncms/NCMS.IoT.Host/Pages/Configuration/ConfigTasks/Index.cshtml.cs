using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using NCMS.IoT.DeviceManagement.Contracts.Dtos;
using NCMS.IoT.DeviceManagement.Contracts.Permissions;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Features.V1.Configuration.ConfigureTasks;
using NCMS.IoT.DeviceManagement.Features.V1.Configuration.Profiles;
using NCMS.IoT.Host.Application;
using NCMS.IoT.Host.Models;

namespace NCMS.IoT.Host.Pages.Configuration.ConfigTasks;

[Authorize(Policy = DeviceManagementPermissions.ConfigTasks.List)]
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
    [BindProperty(SupportsGet = true)] public string? TaskNumber { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? FilterProductId { get; set; }
    [BindProperty(SupportsGet = true)] public ConfigureTaskStatus? Status { get; set; }

    public int[] PageSizeOptions { get; } = [25, 50, 100];

    public IDictionary<string, string?> FilterRouteValues()
    {
        var values = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(TaskNumber)) values["taskNumber"] = TaskNumber;
        if (FilterProductId is { } p) values["filterProductId"] = p.ToString();
        if (Status is { } s) values["status"] = s.ToString();
        if (!string.IsNullOrWhiteSpace(Sort)) values["sort"] = Sort;
        values["pageSize"] = PageSize.ToString();
        return values;
    }

    [BindProperty] public Guid ProfileId { get; set; }
    [BindProperty] public decimal Duration { get; set; } = 1.0m;
    [BindProperty] public List<Guid> DeviceIds { get; set; } = [];

    public ConfigureTaskPagedResult Result { get; private set; } = default!;
    public PagerInfo Pager { get; private set; } = default!;
    public List<SelectListItem> ProductOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (PageNumber <= 0) PageNumber = 1;
        if (!PageSizeOptions.Contains(PageSize)) PageSize = 25;

        Result = await _sender.Send(new ListConfigureTasks.Query(PageNumber, PageSize, Sort, TaskNumber, FilterProductId, Status), ct);
        Pager = new PagerInfo
        {
            Page = Result.Page,
            PageSize = Result.PageSize,
            Total = Result.Total,
            PageName = "/Configuration/ConfigTasks/Index",
            Align = "center",
            PageSizeOptions = PageSizeOptions,
            Query = FilterRouteValues()
        };

        var products = await _sender.Send(new ListConfigurableProducts.Query(), ct);
        ProductOptions = products
            .Select(p => new SelectListItem(p.Name, p.Id.ToString()))
            .ToList();

        return PageOrPartial("_Results");
    }

    // ── JSON read endpoints consumed by the modals ────────────────────────────

    public async Task<IActionResult> OnGetProfilesAsync(Guid productId, CancellationToken ct)
    {
        var profiles = await _sender.Send(new ListProfiles.Query(productId, OnlyEnabled: true), ct);
        return new JsonResult(profiles.Select(p => new { id = p.Id, name = p.ProfileName }));
    }

    public async Task<IActionResult> OnGetDevicesAsync(
        Guid? productId, string? serialNumber, string? status, [FromQuery] int page, int pageSize, CancellationToken ct)
    {
        if (!DevicePageSizes.Contains(pageSize)) pageSize = DefaultDevicePageSize;
        var result = await _sender.Send(
            new SearchConfigDevices.Query(productId, null, serialNumber, status, page < 1 ? 1 : page, pageSize), ct);
        return new JsonResult(new { items = result.Items, total = result.Total, page = page < 1 ? 1 : page, pageSize });
    }

    public async Task<IActionResult> OnGetDetailAsync(Guid id, [FromQuery] int page, int pageSize, CancellationToken ct)
    {
        try
        {
            if (!DevicePageSizes.Contains(pageSize)) pageSize = DefaultDevicePageSize;
            var task = await _sender.Send(new GetConfigureTaskDetail.Query(id, page < 1 ? 1 : page, pageSize), ct);
            if (task is null)
                return new JsonResult(new { error = "Config task not found." }) { StatusCode = StatusCodes.Status404NotFound };

            var devices = task.Devices.Select(d => new
            {
                d.Index,
                d.DeviceId,
                d.DeviceName,
                d.ProductModel,
                configState = d.ConfigState.ToString(),
                d.AttemptCount,
                d.DispatchedAt,
                d.CompletedAt,
                d.FailureReason
            });

            return new JsonResult(new
            {
                id           = task.Id,
                taskNumber   = task.TaskNumber,
                profileName  = task.ProfileName,
                productModel = task.ProductModel,
                deviceTotal  = task.DeviceTotal,
                configDurationHours = task.ConfigDurationHours,
                taskStatus   = task.TaskStatus.ToString(),
                createdBy    = task.CreatedBy,
                createdAt    = task.CreatedAt,
                startedAt    = task.StartedAt,
                completedAt  = task.CompletedAt,
                progress = new
                {
                    total      = task.Progress.Total,
                    notStarted = task.Progress.NotStarted,
                    inProgress = task.Progress.InProgress,
                    complete   = task.Progress.Complete,
                    terminated = task.Progress.Terminated
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
        if (EnsurePermission(DeviceManagementPermissions.ConfigTasks.Add) is { } forbidden) return forbidden;
        try
        {
            var result = await _sender.Send(
                new CreateConfigureTask.Command(ProfileId, DeviceIds, Duration), ct);
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
        if (EnsurePermission(DeviceManagementPermissions.ConfigTasks.Abort) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new AbortConfigureTaskJob.Command(id, deviceId), ct);
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

    // ── Task abort (AJAX, consumed by the detail modal) ────────────────────────
    public async Task<IActionResult> OnPostAbortTaskAsync(Guid id, CancellationToken ct)
    {
        if (EnsurePermission(DeviceManagementPermissions.ConfigTasks.Terminate) is { } forbidden) return forbidden;
        try
        {
            await _sender.Send(new AbortConfigureTask.Command(id), ct);
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
