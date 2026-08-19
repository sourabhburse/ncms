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
/// Retries or terminates config-device records that have been in progress past their
/// configured timeout. Mirrors JobTimeoutMonitorService.
/// </summary>
public sealed class ConfigTimeoutMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConfigTimeoutMonitorService> _logger;
    private readonly FileStorageOptions _opts;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan DefaultDeviceTimeout = TimeSpan.FromMinutes(10);

    public ConfigTimeoutMonitorService(
        IServiceScopeFactory scopeFactory,
        IOptions<FileStorageOptions> opts,
        ILogger<ConfigTimeoutMonitorService> logger)
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
            catch (Exception ex) { _logger.LogError(ex, "ConfigTimeoutMonitor error"); }

            try { await Task.Delay(CheckInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task CheckTimeoutsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IConfigMqttPublisher>();

        var now = DateTimeOffset.UtcNow;

        var stuckDevices = await db.ConfigureTaskDevices
            .Include(d => d.ConfigureTask).ThenInclude(t => t.Profile)
            .Where(d => d.Status == DeviceConfigStatus.ConfigInProgress && d.DispatchedAt != null)
            .ToListAsync(ct);

        var timedOut = stuckDevices.Where(d =>
        {
            var timeout = d.ConfigureTask.ConfigTimeout ?? DefaultDeviceTimeout;
            return d.DispatchedAt!.Value.Add(timeout) < now;
        }).ToList();

        foreach (var device in timedOut)
        {
            if (device.AttemptCount < device.MaxAttempts)
            {
                _logger.LogWarning("Config device {DeviceId} timed out, retrying (attempt {N}/{Max})",
                    device.DeviceId, device.AttemptCount + 1, device.MaxAttempts);
                try
                {
                    var task = device.ConfigureTask;
                    var profile = task.Profile;
                    var expiresAt = task.ConfigTimeout.HasValue
                        ? DateTime.UtcNow.Add(task.ConfigTimeout.Value)
                        : DateTime.UtcNow.AddHours(1);

                    var command = new ConfigCommandMessage(
                        CommandId: Guid.NewGuid().ToString(),
                        TaskDeviceId: device.Id.ToString(),
                        TaskId: task.Id.ToString(),
                        ProfileName: profile.ProfileName,
                        ConfigUrl: _opts.BuildConfigUrl(profile.StoragePath),
                        Size: profile.Size,
                        Md5: profile.Md5,
                        ExpiresAt: expiresAt);

                    await publisher.PublishConfigCommandAsync(device.DeviceId, command, ct);
                    device.MarkDispatched();
                }
                catch (Exception ex)
                {
                    // A failed retry attempt still counts against MaxAttempts, but isn't
                    // terminal by itself — a transient publish failure (e.g. the MQTT client
                    // briefly not ready) shouldn't permanently fail a device that would have
                    // succeeded on the next tick. Only mark TimedOut once attempts are
                    // genuinely exhausted.
                    _logger.LogError(ex, "Retry dispatch failed for config device {DeviceId} (attempt {N}/{Max})",
                        device.DeviceId, device.AttemptCount + 1, device.MaxAttempts);
                    device.RecordFailedAttempt();
                    if (device.AttemptCount >= device.MaxAttempts)
                        device.MarkTimedOut("Config timeout elapsed and retry dispatch failed.");
                }
            }
            else
            {
                _logger.LogWarning("Config device {DeviceId} exceeded max attempts, terminating", device.DeviceId);
                device.MarkTimedOut("Config timeout elapsed.");
            }
        }

        if (timedOut.Count > 0)
        {
            // Timing out the last in-flight device may complete the task.
            foreach (var taskId in timedOut.Select(d => d.ConfigureTaskId).Distinct())
                await TaskReconciliation.ReconcileConfigureTaskAsync(db, taskId, ct);

            await db.SaveChangesAsync(ct);
        }
    }
}
