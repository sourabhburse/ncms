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

public sealed class JobTimeoutMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobTimeoutMonitorService> _logger;
    private readonly FileStorageOptions _opts;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultDeviceTimeout = TimeSpan.FromMinutes(10);

    public JobTimeoutMonitorService(
        IServiceScopeFactory scopeFactory,
        IOptions<FileStorageOptions> opts,
        ILogger<JobTimeoutMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _opts = opts.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CheckTimeoutsAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "JobTimeoutMonitor error"); }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckTimeoutsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IFirmwareMqttPublisher>();

        var now = DateTimeOffset.UtcNow;

        var stuckDevices = await db.UpgradeTaskDevices
            .Include(d => d.UpgradeTask).ThenInclude(t => t.Firmware)
            .Where(d => d.Status == UpgradeDeviceStatus.InProgress && d.DispatchedAt != null)
            .ToListAsync(ct);

        var timedOut = stuckDevices.Where(d =>
        {
            var timeout = d.UpgradeTask.UpgradeTimeout ?? DefaultDeviceTimeout;
            return d.DispatchedAt!.Value.Add(timeout) < now;
        }).ToList();

        foreach (var device in timedOut)
        {
            if (device.AttemptCount < device.MaxAttempts)
            {
                _logger.LogWarning("Device {DeviceId} timed out, retrying (attempt {N}/{Max})",
                    device.DeviceId, device.AttemptCount + 1, device.MaxAttempts);
                try
                {
                    var fw = device.UpgradeTask.Firmware;
                    var expiresAt = device.UpgradeTask.UpgradeTimeout.HasValue
                        ? DateTime.UtcNow.Add(device.UpgradeTask.UpgradeTimeout.Value)
                        : DateTime.UtcNow.AddHours(1);

                    var command = new FirmwareCommandMessage(
                        CommandId: Guid.NewGuid().ToString(),
                        TaskDeviceId: device.Id.ToString(),
                        TaskId: device.UpgradeTaskId.ToString(),
                        TargetVersion: fw.Version,
                        PackageUrl: _opts.BuildFirmwareUrl(fw.StoragePath),
                        Size: fw.Size,
                        Md5: fw.Md5,
                        Sha256: fw.BinaryChecksum,
                        RollbackOnFailure: true,
                        ExpiresAt: expiresAt);

                    await publisher.PublishFirmwareCommandAsync(device.DeviceId, command, ct);
                    device.MarkDispatched();
                }
                catch (Exception ex)
                {
                    // A failed retry attempt still counts against MaxAttempts, but isn't
                    // terminal by itself — a transient publish failure (e.g. the MQTT client
                    // briefly not ready) shouldn't permanently fail a device that would have
                    // succeeded on the next tick. Only mark TimedOut once attempts are
                    // genuinely exhausted.
                    _logger.LogError(ex, "Retry dispatch failed for device {DeviceId} (attempt {N}/{Max})",
                        device.DeviceId, device.AttemptCount + 1, device.MaxAttempts);
                    device.RecordFailedAttempt();
                    if (device.AttemptCount >= device.MaxAttempts)
                        device.MarkTimedOut("Upgrade timeout elapsed and retry dispatch failed.");
                }
            }
            else
            {
                _logger.LogWarning("Device {DeviceId} exceeded max attempts, marking TimedOut", device.DeviceId);
                device.MarkTimedOut("Upgrade timeout elapsed.");
            }
        }

        if (timedOut.Count > 0)
        {
            // Timing out the last in-flight device may complete the task.
            foreach (var taskId in timedOut.Select(d => d.UpgradeTaskId).Distinct())
                await TaskReconciliation.ReconcileUpgradeTaskAsync(db, taskId, ct);

            await db.SaveChangesAsync(ct);
        }
    }
}
