using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCMS.IoT.DeviceManagement.Configuration;
using NCMS.IoT.DeviceManagement.Contracts.Services;

namespace NCMS.IoT.DeviceManagement.BackgroundServices;

/// <summary>
/// Marks devices offline when their heartbeats stop arriving. A heartbeat only ever proves a
/// device is alive; silence produces no message, so absence cannot be caught by the inbound
/// handler — it is detected here by a periodic staleness sweep. Any online device whose
/// LastSeenAt is older than (Ztp:StatusIntervalSeconds × DevicePresence:OfflineAfterMissedHeartbeats)
/// is flipped offline. Runs in the same single-owner process as the other background workers.
/// </summary>
public sealed class DevicePresenceReaperService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DevicePresenceReaperService> _logger;
    private readonly TimeSpan _sweepInterval;
    private readonly TimeSpan _staleAfter;

    public DevicePresenceReaperService(
        IServiceScopeFactory scopeFactory,
        IOptions<ZtpOptions> ztp,
        IOptions<DevicePresenceOptions> presence,
        ILogger<DevicePresenceReaperService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var heartbeatSeconds = Math.Max(1, ztp.Value.StatusIntervalSeconds);
        var missed = Math.Max(1, presence.Value.OfflineAfterMissedHeartbeats);
        _sweepInterval = TimeSpan.FromSeconds(Math.Max(5, presence.Value.SweepIntervalSeconds));
        _staleAfter = TimeSpan.FromSeconds(heartbeatSeconds * missed);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DevicePresenceReaper started. Sweeping every {Sweep}s; marking devices offline after " +
            "{Stale}s without a heartbeat.",
            _sweepInterval.TotalSeconds, _staleAfter.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cutoff = DateTimeOffset.UtcNow - _staleAfter;

                using var scope = _scopeFactory.CreateScope();
                var presence = scope.ServiceProvider.GetRequiredService<IDevicePresenceService>();
                var marked = await presence.MarkStaleOfflineAsync(cutoff, stoppingToken);

                if (marked > 0)
                    _logger.LogInformation(
                        "DevicePresenceReaper marked {Count} device(s) offline (no heartbeat since {Cutoff:u}).",
                        marked, cutoff);
            }
            catch (Exception ex) { _logger.LogError(ex, "DevicePresenceReaper sweep error"); }

            try { await Task.Delay(_sweepInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
