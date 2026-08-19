using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Contracts.Mqtt;
using NCMS.IoT.DeviceManagement.Data;
using NCMS.IoT.DeviceManagement.Entities;

namespace NCMS.IoT.DeviceManagement.BackgroundServices;

/// <summary>
/// Polls for NotStarted application tasks, transitions them to InProgress, and dispatches MQTT
/// install commands to all pending devices. Third instance of the FirmwareDispatcherService/
/// ConfigDispatcherService pattern — the one addition is the cross-domain busy check
/// (<see cref="DeviceBusyGuard"/>), since a device already running a firmware or config job
/// must not also receive an application deployment.
/// </summary>
public sealed class ApplicationDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApplicationDispatcherService> _logger;
    private readonly FileStorageOptions _opts;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public ApplicationDispatcherService(
        IServiceScopeFactory scopeFactory,
        IOptions<FileStorageOptions> opts,
        ILogger<ApplicationDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _opts = opts.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchPendingTasksAsync(stoppingToken);
                await DispatchPendingDevicesAsync(stoppingToken);
                await AutoCompleteFinishedTasksAsync(stoppingToken);
            }
            catch (Exception ex) { _logger.LogError(ex, "ApplicationDispatcher error"); }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DispatchPendingTasksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IApplicationMqttPublisher>();

        var tasks = await db.ApplicationTasks
            .Where(t => t.Status == ApplicationTaskStatus.NotStarted)
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            _logger.LogInformation("Starting application task {TaskId} ({Name})", task.Id, task.Name);

            var devices = await db.ApplicationTaskDevices
                .Include(d => d.ApplicationPackageVersion).ThenInclude(v => v.ApplicationPackage)
                .Where(d => d.ApplicationTaskId == task.Id && d.Status == ApplicationTaskDeviceStatus.Pending)
                .ToListAsync(ct);

            var busyElsewhere = await DeviceBusyGuard.GetDevicesBusyElsewhereAsync(
                db, devices.Select(d => d.DeviceId).ToList(), ct);

            foreach (var device in devices)
            {
                if (busyElsewhere.Contains(device.DeviceId))
                {
                    _logger.LogInformation(
                        "Deferring device {DeviceId} in application task {TaskId} — busy with a firmware/config job",
                        device.DeviceId, task.Id);
                    continue;
                }

                try
                {
                    var command = BuildCommand(device, task.Action);
                    await publisher.PublishApplicationCommandAsync(device.DeviceId, command, ct);
                    device.MarkDispatched();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch device {DeviceId} in task {TaskId}", device.DeviceId, task.Id);
                }
            }

            task.Reconcile(devices);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task DispatchPendingDevicesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IApplicationMqttPublisher>();

        var pendingDevices = await db.ApplicationTaskDevices
            .Include(d => d.ApplicationPackageVersion).ThenInclude(v => v.ApplicationPackage)
            .Include(d => d.ApplicationTask)
            .Where(d => d.Status == ApplicationTaskDeviceStatus.Pending
                && d.ApplicationTask.Status == ApplicationTaskStatus.InProgress)
            .ToListAsync(ct);

        if (pendingDevices.Count == 0) return;

        var busyElsewhere = await DeviceBusyGuard.GetDevicesBusyElsewhereAsync(
            db, pendingDevices.Select(d => d.DeviceId).ToList(), ct);

        foreach (var device in pendingDevices)
        {
            if (busyElsewhere.Contains(device.DeviceId)) continue;

            try
            {
                var command = BuildCommand(device, device.ApplicationTask.Action);
                await publisher.PublishApplicationCommandAsync(device.DeviceId, command, ct);
                device.MarkDispatched();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch pending device {DeviceId}", device.DeviceId);
            }
        }

        foreach (var taskId in pendingDevices.Select(d => d.ApplicationTaskId).Distinct())
            await TaskReconciliation.ReconcileApplicationTaskAsync(db, taskId, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task AutoCompleteFinishedTasksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();

        // Periodic backstop, mirrors FirmwareDispatcherService.AutoCompleteFinishedTasksAsync —
        // catches any completion missed by the live MQTT-driven transition paths.
        var inProgressTaskIds = await db.ApplicationTasks
            .Where(t => t.Status == ApplicationTaskStatus.InProgress)
            .Select(t => t.Id)
            .ToListAsync(ct);

        foreach (var taskId in inProgressTaskIds)
            await TaskReconciliation.ReconcileApplicationTaskAsync(db, taskId, ct);

        await db.SaveChangesAsync(ct);
    }

    private IApplicationCommand BuildCommand(ApplicationTaskDevice device, ApplicationTaskAction action)
    {
        var version = device.ApplicationPackageVersion;
        var packageName = version.ApplicationPackage.Name;
        // Wire format is lowercase for the device agent; the UI formats this same enum to
        // sentence case (Install/Upgrade/...) independently via Ui.cs/display helpers.
        var actionValue = action.ToString().ToLowerInvariant();

        return action == ApplicationTaskAction.Remove
            ? new ApplicationRemoveCommand(Action: actionValue, PackageName: packageName)
            : new ApplicationInstallCommand(
                Action: actionValue,
                PackageName: packageName,
                PackageUrl: _opts.BuildApplicationUrl(version.StoragePath),
                Size: version.SizeBytes,
                Md5: version.Md5Checksum ?? string.Empty,
                Sha256: version.Sha256Checksum);
    }
}
