using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using NCMS.IoT.Core.Domain.Interfaces;
using NCMS.IoT.MqttClient.Bootstrapping;
using NCMS.IoT.MqttClient.Configuration;
using NCMS.IoT.MqttClient.Handlers;

namespace NCMS.IoT.MqttClient.Workers;

/// <summary>
/// Long-running background service that owns a single <see cref="IManagedMqttClient"/>
/// and dispatches incoming messages to the correct <see cref="IMqttMessageHandler"/>
/// based on the topic suffix (all path segments after <c>d/{DeviceId}/</c>).
///
/// Also implements <see cref="IMqttPublisher"/> so other services can publish outbound
/// messages on the same connection without a separate MQTT client.
///
/// Topic → Handler mapping (configured via MqttOptions.Topics):
///   d/+/telemetry   →  TelemetryMessageHandler
///   d/+/heartbeat   →  DeviceStatusMessageHandler
///   d/+/events      →  DeviceEventMessageHandler
///   d/+/ota/res     →  FirmwareStatusMessageHandler  (registered by the DeviceManagement module)
///
/// Adding a new topic requires:
///   1. A new IMqttMessageHandler implementation.
///   2. Register it in the appropriate module.
///   3. Add its topic pattern to MqttOptions.Topics.
///   4. Add the corresponding Mosquitto ACL line for the backend user.
/// </summary>
public sealed class MqttIngestionWorker : BackgroundService, IMqttPublisher
{
    private readonly BackendCertificateBootstrapper _bootstrapper;
    private readonly BackendCertificateStore _certStore;
    private readonly IDeviceCertificateIssuer _certIssuer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MqttOptions> _mqttOptions;
    private readonly ILogger<MqttIngestionWorker> _logger;

    // Exposed for IMqttPublisher — set before ExecuteAsync enters its wait loop.
    private IManagedMqttClient? _client;

    // Completed once _client has been created and started (safe to EnqueueAsync onto — the
    // managed client itself queues messages while reconnecting). Callers that race the startup
    // window (e.g. a background dispatcher's very first tick) await this with a bounded timeout
    // instead of failing immediately just because ExecuteAsync hasn't reached that point yet.
    // Replaced with a fresh instance on every (re)connect attempt — see the retry loop in
    // ExecuteAsync — so a waiter can't observe "ready" left over from a since-failed attempt.
    private volatile TaskCompletionSource _clientReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public MqttIngestionWorker(
        BackendCertificateBootstrapper bootstrapper,
        BackendCertificateStore certStore,
        IDeviceCertificateIssuer certIssuer,
        IServiceScopeFactory scopeFactory,
        IOptions<MqttOptions> mqttOptions,
        ILogger<MqttIngestionWorker> logger)
    {
        _bootstrapper = bootstrapper;
        _certStore = certStore;
        _certIssuer = certIssuer;
        _scopeFactory = scopeFactory;
        _mqttOptions = mqttOptions;
        _logger = logger;
    }

    // ── IMqttPublisher ───────────────────────────────────────────────────────

    /// <summary>
    /// Enqueues an outbound message on the shared managed client.
    /// Thread-safe; safe to call before the client has fully connected — the managed
    /// client will deliver it once the connection is established.
    /// </summary>
    public async Task PublishAsync(string topic, byte[] payload, CancellationToken ct = default)
    {
        if (_client is null)
        {
            // Startup race: a caller (e.g. a dispatcher's first tick) can run before
            // ExecuteAsync finishes connecting. Wait briefly instead of failing outright —
            // treating "not ready yet" the same as "dispatch failed" was marking devices
            // TimedOut/Failed purely due to service startup ordering.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                await _clientReady.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    "MQTT client did not become ready within 15s. Ensure MqttIngestionWorker is running.");
            }
        }

        var client = _client
            ?? throw new InvalidOperationException("MQTT client is not started. Ensure MqttIngestionWorker is running.");

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await client.EnqueueAsync(message);
    }

    // ── BackgroundService ────────────────────────────────────────────────────

    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Since .NET 6, an unhandled exception from a BackgroundService's ExecuteAsync stops the
    /// *entire host* by default (BackgroundServiceExceptionBehavior.StopHost) — so letting a
    /// transient failure here (PKI server unreachable, cert file locked, broker DNS hiccup)
    /// propagate would take down the whole app (web UI, API, everything), not just MQTT
    /// connectivity. Retry with a fixed backoff instead, so the rest of the host stays up
    /// while this keeps trying to (re)connect.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MqttIngestionWorker starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndRunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "MqttIngestionWorker failed to start or run; retrying in {Delay}s.",
                    StartupRetryDelay.TotalSeconds);
                _client = null;
                // Fresh instance for the next attempt — a waiter must not observe "ready" left
                // over from this failed attempt (the old TCS is simply abandoned; any caller
                // already awaiting it will time out on its own bounded wait in PublishAsync).
                _clientReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                try { await Task.Delay(StartupRetryDelay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("MqttIngestionWorker stopped.");
    }

    private async Task ConnectAndRunAsync(CancellationToken stoppingToken)
    {
        // Pre-flight: ensure the backend has a valid mTLS certificate.
        await _bootstrapper.EnsureCertificateAsync(stoppingToken);

        var rootCaCert = X509CertificateLoader.LoadCertificate(
            Encoding.UTF8.GetBytes(_certIssuer.GetRootCaPem()));

        var factory = new MqttFactory();
        _client = factory.CreateManagedMqttClient();

        var opts = _mqttOptions.Value;
        var clientOptions = BuildClientOptions(opts, _certStore.Certificate, rootCaCert);

        if (opts.AllowUntrustedServerCertificate)
            _logger.LogWarning(
                "Mqtt:AllowUntrustedServerCertificate is TRUE — server certificate validation is disabled. " +
                "This must NOT be used in production.");

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(clientOptions)
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(opts.ReconnectDelaySeconds))
            .WithMaxPendingMessages(opts.MaxPendingMessages)
            .Build();

        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
        _client.ApplicationMessageReceivedAsync += msg => DispatchAsync(msg, stoppingToken);

        await _client.StartAsync(managedOptions);
        _clientReady.TrySetResult();

        // Subscribe to all configured topic patterns with QoS 1.
        var subscriptions = opts.Topics.All()
            .Select(topic =>
                new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build())
            .ToList();

        await _client.SubscribeAsync(subscriptions);

        _logger.LogInformation(
            "MQTT client connected. Broker={Host}:{Port}, Topics=[{Topics}]",
            opts.BrokerHost, opts.BrokerPort,
            string.Join(", ", opts.Topics.All()));

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        await _client.StopAsync();
        _client.Dispose();
        _client = null;
    }

    // ── Message dispatcher ───────────────────────────────────────────────────

    private async Task DispatchAsync(
        MqttApplicationMessageReceivedEventArgs args,
        CancellationToken ct)
    {
        var topic = args.ApplicationMessage.Topic;

        if (!TryParseTopic(topic, out var deviceId, out var suffix))
        {
            _logger.LogWarning("Received message on unrecognised topic '{Topic}'; skipping.", topic);
            args.IsHandled = true;
            return;
        }
        _logger.LogInformation("Received message on topic '{Topic}'", topic);
        string payloadJson;
        try
        {
            payloadJson = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
            using var _ = JsonDocument.Parse(payloadJson); // validate JSON before dispatching
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Malformed JSON on topic '{Topic}' from DeviceId={DeviceId}.",
                topic, deviceId);
            args.IsHandled = true;
            return;
        }

        // Each message gets its own DI scope so handlers get a fresh DbContext.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IMqttMessageHandler>();

        var handler = handlers.FirstOrDefault(h =>
            string.Equals(h.TopicSuffix, suffix, StringComparison.OrdinalIgnoreCase));

        if (handler is null)
        {
            _logger.LogWarning(
                "No handler registered for topic suffix '{Suffix}'. Topic={Topic}",
                suffix, topic);
            args.IsHandled = true;
            return;
        }

        try
        {
            await handler.HandleAsync(
                deviceId,
                topic,
                payloadJson,
                qosLevel: (byte)args.ApplicationMessage.QualityOfServiceLevel,
                ct);
        }
        catch (Exception ex)
        {
            // Never propagate exceptions to the MQTT layer — it would stall message delivery.
            _logger.LogError(ex,
                "Handler '{Handler}' failed for DeviceId={DeviceId}, Topic={Topic}.",
                handler.GetType().Name, deviceId, topic);
        }

        args.IsHandled = true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <c>d/{DeviceId}/{suffix}</c> into its components.
    /// The suffix may contain slashes (e.g., "ota/res") — everything after
    /// the device GUID segment is returned joined as the suffix.
    /// Returns false for any topic that doesn't start with <c>d/{guid}/</c>.
    /// </summary>
    private static bool TryParseTopic(string topic, out Guid deviceId, out string suffix)
    {
        deviceId = Guid.Empty;
        suffix = string.Empty;

        var parts = topic.Split('/');
        if (parts.Length < 3 || parts[0] != "d")
            return false;

        if (!Guid.TryParse(parts[1], out deviceId))
            return false;

        suffix = string.Join("/", parts[2..]);
        return !string.IsNullOrEmpty(suffix);
    }

    private static MqttClientOptions BuildClientOptions(
        MqttOptions opts,
        X509Certificate2 clientCert,
        X509Certificate2 rootCaCert)
    {
        return new MqttClientOptionsBuilder()
            .WithTcpServer(opts.BrokerHost, opts.BrokerPort)
            .WithClientId(opts.ClientId)
            .WithCredentials(opts.Username, password: (string?)null)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(opts.KeepAliveSeconds))
            .WithCleanSession(false)
            .WithTlsOptions(tls => tls
                .UseTls()
                .WithClientCertificates(new X509Certificate2Collection { clientCert })
                .WithCertificateValidationHandler(ctx =>
                    ValidateServerCertificate(ctx, rootCaCert, opts.AllowUntrustedServerCertificate)))
            .Build();
    }

    private static bool ValidateServerCertificate(
        MqttClientCertificateValidationEventArgs ctx,
        X509Certificate2 rootCaCert,
        bool allowUntrusted)
    {
        if (allowUntrusted)
            return true;

        var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(rootCaCert);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        var serverCert = ctx.Certificate as X509Certificate2
            ?? new X509Certificate2(ctx.Certificate);

        return chain.Build(serverCert);
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        _logger.LogInformation("MQTT connected. ResultCode={Code}", args.ConnectResult.ResultCode);
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        if (args.Exception is not null)
            _logger.LogWarning(args.Exception, "MQTT disconnected with exception.");
        else
            _logger.LogWarning("MQTT disconnected. Reason={Reason}. Reconnecting...", args.ReasonString);
        return Task.CompletedTask;
    }
}
