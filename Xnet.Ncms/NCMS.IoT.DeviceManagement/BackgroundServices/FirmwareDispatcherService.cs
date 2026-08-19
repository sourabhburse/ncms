using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Enums;
using NCMS.IoT.DeviceManagement.Contracts.Mqtt;
using NCMS.IoT.DeviceManagement.Data;

namespace NCMS.IoT.DeviceManagement.BackgroundServices;

/// <summary>
/// Polls for NotStarted upgrade tasks, transitions them to InProgress,
/// and dispatches MQTT firmware commands to all pending devices.
/// </summary>
public sealed class FirmwareDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FirmwareDispatcherService> _logger;
    private readonly FileStorageOptions _opts;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public FirmwareDispatcherService(
        IServiceScopeFactory scopeFactory,
        IOptions<FileStorageOptions> opts,
        ILogger<FirmwareDispatcherService> logger)
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
            catch (Exception ex) { _logger.LogError(ex, "FirmwareDispatcher error"); }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DispatchPendingTasksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IFirmwareMqttPublisher>();

        var tasks = await db.UpgradeTasks
            .Include(t => t.Firmware)
            .Where(t => t.Status == UpgradeTaskStatus.NotStarted)
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            _logger.LogInformation("Starting upgrade task {TaskId} ({Name})", task.Id, task.Name);

            var devices = await db.UpgradeTaskDevices
                .Where(d => d.UpgradeTaskId == task.Id && d.Status == UpgradeDeviceStatus.Pending)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            var expiresAt = task.UpgradeTimeout.HasValue
                ? now.Add(task.UpgradeTimeout.Value)
                : now.AddHours(0.1);

            foreach (var device in devices)
            {
                try
                {
                    var command = BuildCommand(task, device, expiresAt);
                    await publisher.PublishFirmwareCommandAsync(device.DeviceId, command, ct);
                    device.MarkDispatched();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch device {DeviceId} in task {TaskId}", device.DeviceId, task.Id);
                }
            }

            // Derive task status from the devices we just dispatched (NotStarted → InProgress).
            // 'devices' is this NotStarted task's complete child set.
            task.Reconcile(devices);

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task DispatchPendingDevicesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IFirmwareMqttPublisher>();

        var pendingDevices = await db.UpgradeTaskDevices
            .Include(d => d.UpgradeTask).ThenInclude(t => t.Firmware)
            .Where(d => d.Status == UpgradeDeviceStatus.Pending
                && d.UpgradeTask.Status == UpgradeTaskStatus.InProgress)
            .ToListAsync(ct);

        if (pendingDevices.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var device in pendingDevices)
        {
            try
            {
                var expiresAt = device.UpgradeTask.UpgradeTimeout.HasValue
                    ? now.Add(device.UpgradeTask.UpgradeTimeout.Value)
                    : now.AddHours(1);

                var command = BuildCommand(device.UpgradeTask, device, expiresAt);
                await publisher.PublishFirmwareCommandAsync(device.DeviceId, command, ct);
                device.MarkDispatched();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch pending device {DeviceId}", device.DeviceId);
            }
        }

        // Re-derive each affected task's status from its devices after this dispatch round.
        foreach (var taskId in pendingDevices.Select(d => d.UpgradeTaskId).Distinct())
            await TaskReconciliation.ReconcileUpgradeTaskAsync(db, taskId, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task AutoCompleteFinishedTasksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();

        var inProgressTaskIds = await db.UpgradeTasks
            .Where(t => t.Status == UpgradeTaskStatus.InProgress)
            .Select(t => t.Id)
            .ToListAsync(ct);

        // Periodic reconciliation backstop: recompute each running task's status from its
        // devices, catching any completion missed by the live transition paths.
        foreach (var taskId in inProgressTaskIds)
            await TaskReconciliation.ReconcileUpgradeTaskAsync(db, taskId, ct);

        await db.SaveChangesAsync(ct);
    }

    private FirmwareCommandMessage BuildCommand(
        Entities.UpgradeTask task,
        Entities.UpgradeTaskDevice device,
        DateTime expiresAt)
    {
        var fw = task.Firmware;
        return new FirmwareCommandMessage(
            CommandId: Guid.NewGuid().ToString(),
            TaskDeviceId: device.Id.ToString(),
            TaskId: task.Id.ToString(),
            TargetVersion: fw.Version,
            PackageUrl: _opts.BuildFirmwareUrl(fw.StoragePath),
            Size: fw.Size,
            Md5: fw.Md5,
            Sha256: fw.BinaryChecksum,
            RollbackOnFailure: true,
            ExpiresAt: expiresAt);
    }
}
