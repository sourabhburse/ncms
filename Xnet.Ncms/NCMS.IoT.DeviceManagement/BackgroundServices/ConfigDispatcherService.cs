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
/// Polls for NotStarted config tasks, transitions them to InProgress, and dispatches
/// MQTT config commands to all not-started devices. Mirrors FirmwareDispatcherService.
/// </summary>
public sealed class ConfigDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfigDispatcherService> _logger;
    private readonly FileStorageOptions _opts;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    public ConfigDispatcherService(
        IServiceScopeFactory scopeFactory,
        IOptions<FileStorageOptions> opts,
        ILogger<ConfigDispatcherService> logger)
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
            catch (Exception ex) { _logger.LogError(ex, "ConfigDispatcher error"); }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task DispatchPendingTasksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IConfigMqttPublisher>();

        var tasks = await db.ConfigureTasks
            .Include(t => t.Profile)
            .Where(t => t.Status == ConfigureTaskStatus.NotStarted)
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            _logger.LogInformation("Starting config task {TaskId} ({Number})", task.Id, task.TaskNumber);

            var devices = await db.ConfigureTaskDevices
                .Where(d => d.ConfigureTaskId == task.Id && d.Status == DeviceConfigStatus.NotStarted)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            var expiresAt = task.ConfigTimeout.HasValue ? now.Add(task.ConfigTimeout.Value) : now.AddHours(1);

            foreach (var device in devices)
            {
                try
                {
                    await publisher.PublishConfigCommandAsync(device.DeviceId, BuildCommand(task, device, expiresAt), ct);
                    device.MarkDispatched();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch device {DeviceId} in config task {TaskId}", device.DeviceId, task.Id);
                }
            }

            // Derive task status from the devices we just dispatched (NotStarted → InProgress).
            task.Reconcile(devices);

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task DispatchPendingDevicesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IConfigMqttPublisher>();

        var pendingDevices = await db.ConfigureTaskDevices
            .Include(d => d.ConfigureTask).ThenInclude(t => t.Profile)
            .Where(d => d.Status == DeviceConfigStatus.NotStarted
                && d.ConfigureTask.Status == ConfigureTaskStatus.InProgress)
            .ToListAsync(ct);

        if (pendingDevices.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var device in pendingDevices)
        {
            try
            {
                var expiresAt = device.ConfigureTask.ConfigTimeout.HasValue
                    ? now.Add(device.ConfigureTask.ConfigTimeout.Value)
                    : now.AddHours(1);

                await publisher.PublishConfigCommandAsync(device.DeviceId, BuildCommand(device.ConfigureTask, device, expiresAt), ct);
                device.MarkDispatched();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch pending config device {DeviceId}", device.DeviceId);
            }
        }

        // Re-derive each affected task's status from its devices after this dispatch round.
        foreach (var taskId in pendingDevices.Select(d => d.ConfigureTaskId).Distinct())
            await TaskReconciliation.ReconcileConfigureTaskAsync(db, taskId, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task AutoCompleteFinishedTasksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();

        var inProgressTaskIds = await db.ConfigureTasks
            .Where(t => t.Status == ConfigureTaskStatus.InProgress)
            .Select(t => t.Id)
            .ToListAsync(ct);

        // Periodic reconciliation backstop: recompute each running task's status from its devices.
        foreach (var taskId in inProgressTaskIds)
            await TaskReconciliation.ReconcileConfigureTaskAsync(db, taskId, ct);

        await db.SaveChangesAsync(ct);
    }

    private ConfigCommandMessage BuildCommand(
        Entities.ConfigureTask task,
        Entities.ConfigureTaskDevice device,
        DateTime expiresAt)
    {
        var profile = task.Profile;
        return new ConfigCommandMessage(
            CommandId: Guid.NewGuid().ToString(),
            TaskDeviceId: device.Id.ToString(),
            TaskId: task.Id.ToString(),
            ProfileName: profile.ProfileName,
            ConfigUrl: _opts.BuildConfigUrl(profile.StoragePath),
            Size: profile.Size,
            Md5: profile.Md5,
            ExpiresAt: expiresAt);
    }
}
