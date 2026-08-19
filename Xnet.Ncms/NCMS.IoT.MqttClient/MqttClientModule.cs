using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCMS.IoT.MqttClient.Bootstrapping;
using NCMS.IoT.MqttClient.Configuration;
using NCMS.IoT.MqttClient.Workers;

namespace NCMS.IoT.MqttClient;

public static class MqttClientModule
{
    /// <summary>
    /// Registers the MQTT ingestion worker and bootstrapping components.
    ///
    /// The worker is registered as a singleton and exposed as both <see cref="IMqttPublisher"/>
    /// and <see cref="Microsoft.Extensions.Hosting.IHostedService"/> so other modules (e.g.,
    /// FirmwareManager) can inject <see cref="IMqttPublisher"/> and publish outbound messages on
    /// the same shared connection without running a second MQTT client.
    ///
    /// Domain message handlers (telemetry, status, events) are registered by their owning modules:
    ///   - Telemetry handlers → AddTelemetryModule() in NCMS.IoT.Telemetry
    ///   - Firmware OTA handler → AddFirmwareManagementModule() in NCMS.IoT.FirmwareManager
    ///
    /// To add support for a new device topic (e.g., d/+/logs):
    ///   1. Implement IMqttMessageHandler with TopicSuffix = "logs".
    ///   2. Register it with AddScoped in the owning module.
    ///   3. Add the topic pattern to appsettings Mqtt:Topics.
    ///   4. Add "topic read d/+/logs" to the Mosquitto ACL for the backend user.
    /// </summary>
    public static IServiceCollection AddMqttClientModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MqttOptions>(configuration.GetSection(MqttOptions.SectionName));

        services.AddSingleton<BackendCertificateStore>();
        services.AddSingleton<BackendCertificateBootstrapper>();

        // Register the worker as singleton so it can be resolved as IMqttPublisher.
        services.AddSingleton<MqttIngestionWorker>();
        services.AddSingleton<IMqttPublisher>(sp => sp.GetRequiredService<MqttIngestionWorker>());

        // Only one process should own the shared MQTT connection (a single broker
        // client id). When several apps share this module (e.g. API + Razor Host),
        // set "BackgroundWorkers:Enabled": false in the UI-only process so it does
        // not start a second ingestion client.
        if (BackgroundWorkersEnabled(configuration))
            services.AddHostedService(sp => sp.GetRequiredService<MqttIngestionWorker>());

        return services;
    }

    /// <summary>
    /// Background workers run by default. A process can opt out by setting
    /// <c>BackgroundWorkers:Enabled</c> to <c>false</c> (used by the UI-only Host).
    /// </summary>
    public static bool BackgroundWorkersEnabled(IConfiguration configuration) =>
        !string.Equals(configuration["BackgroundWorkers:Enabled"], "false", StringComparison.OrdinalIgnoreCase);
}
