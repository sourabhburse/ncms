using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using NCMS.Backend.Core.Entities;
using NCMS.Backend.Infrastructure.Data;

namespace NCMS.Backend.Worker;

public class Worker : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IConfiguration configuration,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<Worker> logger)
    {
        _configuration = configuration;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mqttFactory = new MqttFactory();
        using var mqttClient = mqttFactory.CreateMqttClient();

        var brokerUrl = _configuration["Mqtt:BrokerUrl"] ?? "localhost";
        var brokerPort = int.TryParse(_configuration["Mqtt:BrokerPort"], out var port) ? port : 1883;
        var clientId = _configuration["Mqtt:ClientId"] ?? $"ncms-backend-worker-{Guid.NewGuid():N}";

        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerUrl, brokerPort)
            .WithClientId(clientId)
            .WithCleanSession()
            .Build();

        mqttClient.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                await ProcessMessageAsync(e.ApplicationMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message on topic {Topic}", e.ApplicationMessage.Topic);
            }
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!mqttClient.IsConnected)
                {
                    _logger.LogInformation("Connecting to MQTT broker at {BrokerUrl}:{BrokerPort}...", brokerUrl, brokerPort);
                    await mqttClient.ConnectAsync(mqttClientOptions, stoppingToken);

                    var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic("d/+/telemetry"))
                        .Build();

                    await mqttClient.SubscribeAsync(mqttSubscribeOptions, stoppingToken);
                    _logger.LogInformation("Connected and subscribed to telemetry topics.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to MQTT broker. Retrying in 5 seconds...");
            }

            try
            {
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (mqttClient.IsConnected)
        {
            await mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        }
    }

    private async Task ProcessMessageAsync(MqttApplicationMessage message)
    {
        var topic = message.Topic;
        var payload = message.PayloadSegment.Count > 0
            ? System.Text.Encoding.UTF8.GetString(message.PayloadSegment)
            : string.Empty;

        _logger.LogDebug("Received MQTT message on topic {Topic}", topic);

        string[] parts = topic.Split('/');
        if (parts.Length != 3 || parts[0] != "d" || parts[2] != "telemetry")
        {
            _logger.LogWarning("Ignoring message on invalid topic structure: {Topic}", topic);
            return;
        }

        if (!Guid.TryParse(parts[1], out var deviceId))
        {
            _logger.LogWarning("Ignoring message with invalid Device ID format: {DeviceIdString}", parts[1]);
            return;
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogWarning("Ignoring empty payload for device: {DeviceId}", deviceId);
            return;
        }

        TelemetryPayload? payloadData;
        try
        {
            payloadData = JsonSerializer.Deserialize<TelemetryPayload>(payload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize JSON telemetry payload: {Payload}", payload);
            return;
        }

        if (payloadData is null)
        {
            return;
        }

        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NcmsDbContext>();

            var deviceExists = await dbContext.Devices.AnyAsync(d => d.Id == deviceId);
            if (!deviceExists)
            {
                _logger.LogWarning("Discarding telemetry for unknown device: {DeviceId}", deviceId);
                return;
            }

            var telemetry = new DeviceTelemetry
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                Timestamp = DateTime.UtcNow,
                CpuUsagePercent = payloadData.CpuUsagePercent,
                RamUsageMb = payloadData.RamUsageMb,
                RamTotalMb = payloadData.RamTotalMb,
                StorageUsedMb = payloadData.StorageUsedMb,
                StorageTotalMb = payloadData.StorageTotalMb,
                UptimeSeconds = payloadData.UptimeSeconds,
                WanIp = payloadData.WanIp,
                TemperatureCelsius = payloadData.TemperatureCelsius,
                SignalStrengthRssi = payloadData.SignalStrengthRssi,
                SignalQualityRsrp = payloadData.SignalQualityRsrp
            };

            dbContext.DeviceTelemetries.Add(telemetry);

            var device = await dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
            if (device is not null)
            {
                device.LastSeenAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(payloadData.WanIp))
                {
                    device.WanIpAddress = payloadData.WanIp;
                }
                dbContext.Devices.Update(device);
            }

            await dbContext.SaveChangesAsync();

            _logger.LogInformation("Persisted telemetry for device {DeviceId} (CPU: {Cpu}%, RAM: {Ram}/{RamTotal} MB, Uptime: {Uptime}s)",
                deviceId, telemetry.CpuUsagePercent, telemetry.RamUsageMb, telemetry.RamTotalMb, telemetry.UptimeSeconds);
        }
    }

    private sealed class TelemetryPayload
    {
        [JsonPropertyName("cpu_usage_percent")]
        public double CpuUsagePercent { get; set; }

        [JsonPropertyName("ram_usage_mb")]
        public double RamUsageMb { get; set; }

        [JsonPropertyName("ram_total_mb")]
        public double RamTotalMb { get; set; }

        [JsonPropertyName("storage_used_mb")]
        public double StorageUsedMb { get; set; }

        [JsonPropertyName("storage_total_mb")]
        public double StorageTotalMb { get; set; }

        [JsonPropertyName("uptime_seconds")]
        public long UptimeSeconds { get; set; }

        [JsonPropertyName("wan_ip")]
        public string? WanIp { get; set; }

        [JsonPropertyName("temperature_celsius")]
        public double? TemperatureCelsius { get; set; }

        [JsonPropertyName("signal_strength_rssi")]
        public double? SignalStrengthRssi { get; set; }

        [JsonPropertyName("signal_quality_rsrp")]
        public double? SignalQualityRsrp { get; set; }
    }
}
