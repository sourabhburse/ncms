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
/// Mirrors JobTimeoutMonitorService / ConfigTimeoutMonitorService. A stuck
/// ApplicationTaskDevice (InProgress past its task's Timeout) is retried up to MaxAttempts, or
/// marked TimedOut once attempts are exhausted.
/// </summary>
public sealed class ApplicationTimeoutMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApplicationTimeoutMonitorService> _logger;
    private readonly FileStorageOptions _opts;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultDeviceTimeout = TimeSpan.FromMinutes(10);

    public ApplicationTimeoutMonitorService(
        IServiceScopeFactory scopeFactory,
        IOptions<FileStorageOptions> opts,
        ILogger<ApplicationTimeoutMonitorService> logger)
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
            catch (Exception ex) { _logger.LogError(ex, "ApplicationTimeoutMonitor error"); }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckTimeoutsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IApplicationMqttPublisher>();

        var now = DateTimeOffset.UtcNow;

        var stuckDevices = await db.ApplicationTaskDevices
            .Include(d => d.ApplicationTask)
            .Include(d => d.ApplicationPackageVersion).ThenInclude(v => v.ApplicationPackage)
            .Where(d => d.Status == ApplicationTaskDeviceStatus.InProgress && d.DispatchedAt != null)
            .ToListAsync(ct);

        var timedOut = stuckDevices.Where(d =>
        {
            var timeout = d.ApplicationTask.Timeout ?? DefaultDeviceTimeout;
            return d.DispatchedAt!.Value.Add(timeout) < now;
        }).ToList();

        if (timedOut.Count == 0) return;

        foreach (var device in timedOut)
        {
            if (device.AttemptCount < device.MaxAttempts)
            {
                _logger.LogWarning("Application task device {DeviceId} timed out, retrying (attempt {N}/{Max})",
                    device.DeviceId, device.AttemptCount + 1, device.MaxAttempts);
                try
                {
                    var action = device.ApplicationTask.Action;
                    var version = device.ApplicationPackageVersion;
                    var packageName = version.ApplicationPackage.Name;
                    // Wire format is lowercase for the device agent; the UI formats this same
                    // enum to sentence case (Install/Upgrade/...) independently.
                    var actionValue = action.ToString().ToLowerInvariant();
                    IApplicationCommand command = action == ApplicationTaskAction.Remove
                        ? new ApplicationRemoveCommand(
                            Action: actionValue,
                            PackageName: packageName)
                        : new ApplicationInstallCommand(
                            Action: actionValue,
                            PackageName: packageName,
                            PackageUrl: _opts.BuildApplicationUrl(version.StoragePath),
                            Size: version.SizeBytes,
                            Md5: version.Md5Checksum ?? string.Empty,
                            Sha256: version.Sha256Checksum);

                    await publisher.PublishApplicationCommandAsync(device.DeviceId, command, ct);
                    device.MarkDispatched();
                }
                catch (Exception ex)
                {
                    // A failed retry attempt still counts against MaxAttempts, but isn't
                    // terminal by itself — a transient publish failure (e.g. the MQTT client
                    // briefly not ready) shouldn't permanently fail a device that would have
                    // succeeded on the next tick. Only mark TimedOut once attempts are
                    // genuinely exhausted.
                    _logger.LogError(ex, "Retry dispatch failed for application task device {DeviceId} (attempt {N}/{Max})",
                        device.DeviceId, device.AttemptCount + 1, device.MaxAttempts);
                    device.RecordFailedAttempt();
                    if (device.AttemptCount >= device.MaxAttempts)
                        device.MarkTimedOut("Deployment timeout elapsed and retry dispatch failed.");
                }
            }
            else
            {
                _logger.LogWarning("Application task device {DeviceId} exceeded max attempts, marking TimedOut", device.DeviceId);
                device.MarkTimedOut("Deployment timeout elapsed.");
            }
        }

        // Timing out the last in-flight device may complete the task.
        foreach (var taskId in timedOut.Select(d => d.ApplicationTaskId).Distinct())
            await TaskReconciliation.ReconcileApplicationTaskAsync(db, taskId, ct);

        await db.SaveChangesAsync(ct);
    }
}
