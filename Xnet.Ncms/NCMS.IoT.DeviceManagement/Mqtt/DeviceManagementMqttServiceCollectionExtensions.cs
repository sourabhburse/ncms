using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCMS.IoT.DeviceManagement.BackgroundServices;
using NCMS.IoT.DeviceManagement.Contracts.Mqtt;
using NCMS.IoT.MqttClient;
using NCMS.IoT.MqttClient.Handlers;

namespace NCMS.IoT.DeviceManagement.Mqtt;

/// <summary>
/// Registers the Device Management MQTT adapter: outbound publishers, inbound message
/// handlers, and the dispatch/timeout background workers. This is intentionally NOT part
/// of <c>DeviceManagementModule</c> so the HTTP API can use the domain (registration +
/// queries) without depending on the MQTT layer. Only the MQTT worker process calls this.
/// </summary>
public static class DeviceManagementMqttServiceCollectionExtensions
{
    public static IServiceCollection AddDeviceManagementMqtt(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Outbound publishers (wrap the shared IMqttPublisher from MqttClientModule).
        services.AddSingleton<IFirmwareMqttPublisher, FirmwareMqttPublisher>();
        services.AddSingleton<IConfigMqttPublisher, ConfigMqttPublisher>();
        services.AddSingleton<IApplicationMqttPublisher, ApplicationMqttPublisher>();

        // Inbound message handlers — discovered by the ingestion worker via TopicSuffix.
        services.AddScoped<IMqttMessageHandler, TelemetryMessageHandler>();
        services.AddScoped<IMqttMessageHandler, DeviceStatusMessageHandler>();
        services.AddScoped<IMqttMessageHandler, DeviceEventMessageHandler>();
        services.AddScoped<IMqttMessageHandler, FirmwareStatusMessageHandler>();
        services.AddScoped<IMqttMessageHandler, ConfigStatusMessageHandler>();
        services.AddScoped<IMqttMessageHandler, ApplicationStatusMessageHandler>();

        // Firmware + config + application dispatch and timeout monitoring. Gated by the same
        // flag as the MQTT client so exactly one process owns dispatching (avoids duplicate
        // commands).
        if (MqttClientModule.BackgroundWorkersEnabled(configuration))
        {
            services.AddHostedService<FirmwareDispatcherService>();
            services.AddHostedService<JobTimeoutMonitorService>();
            services.AddHostedService<ConfigDispatcherService>();
            services.AddHostedService<ConfigTimeoutMonitorService>();
            services.AddHostedService<ApplicationDispatcherService>();
            services.AddHostedService<ApplicationTimeoutMonitorService>();
            // Marks devices offline when their heartbeats stop arriving (the absence-detection
            // counterpart to DeviceStatusMessageHandler, which only processes heartbeats that arrive).
            services.AddHostedService<DevicePresenceReaperService>();
        }

        return services;
    }
}
