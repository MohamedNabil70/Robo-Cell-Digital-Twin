using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using UnityEngine;

public class MqttTwinClient : MonoBehaviour
{
    public static MqttTwinClient Instance { get; private set; }

    [Header("Broker")]
    public string brokerHost = "localhost";
    public int brokerPort = 1883;
    public bool useTls = false;
    public string username = "";
    public string password = "";
    public string clientId = "unity_twin";
    public string subscribeTopic = "factory/cell1/twin/+/+";

    [Header("Runtime")]
    public bool connectOnStart = true;
    public bool reconnectOnDisconnect = true;
    public float reconnectDelaySeconds = 5f;
    public bool dontDestroyOnLoad = true;
    public TelemetryManager telemetryManager;

    readonly ConcurrentQueue<IncomingMqttMessage> incomingMessages = new ConcurrentQueue<IncomingMqttMessage>();
    readonly ConcurrentQueue<QueuedLog> mainThreadLogs = new ConcurrentQueue<QueuedLog>();

    IMqttClient mqttClient;
    MqttClientOptions mqttOptions;
    CancellationTokenSource lifetimeCancellation;
    int mainThreadId;
    bool connectInProgress;
    bool reconnectScheduled;
    bool shuttingDown;

    public bool IsConnected => mqttClient != null && mqttClient.IsConnected;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        mainThreadId = Thread.CurrentThread.ManagedThreadId;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        lifetimeCancellation = new CancellationTokenSource();
    }

    void Start()
    {
        EnsureTelemetryManager();

        if (connectOnStart)
        {
            Connect();
        }
    }

    void Update()
    {
        FlushQueuedLogs();
        ProcessIncomingMessages();
    }

    void OnDestroy()
    {
        shuttingDown = true;
        lifetimeCancellation?.Cancel();
        _ = DisconnectAsync();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public async void Connect()
    {
        await ConnectAsync();
    }

    public async Task ConnectAsync()
    {
        if (lifetimeCancellation == null || lifetimeCancellation.IsCancellationRequested)
        {
            lifetimeCancellation = new CancellationTokenSource();
        }

        shuttingDown = false;

        if (connectInProgress)
        {
            return;
        }

        if (IsConnected)
        {
            LogInfo("[MQTT Twin] Already connected.");
            return;
        }

        if (string.IsNullOrWhiteSpace(brokerHost))
        {
            LogWarning("[MQTT Twin] Broker host is empty. MQTT connection skipped.");
            return;
        }

        connectInProgress = true;

        try
        {
            var factory = new MqttFactory();
            mqttClient = factory.CreateMqttClient();
            mqttClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceived;
            mqttClient.DisconnectedAsync += OnDisconnected;
            mqttClient.ConnectedAsync += OnConnected;

            mqttOptions = BuildClientOptions(factory);

            LogInfo($"[MQTT Twin] Connecting to {brokerHost}:{brokerPort} TLS={useTls} as '{mqttOptions.ClientId}'...");
            await mqttClient.ConnectAsync(mqttOptions, lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            LogInfo("[MQTT Twin] MQTT connection canceled.");
        }
        catch (Exception ex)
        {
            LogWarning($"[MQTT Twin] Failed to connect; scene will continue. Reason: {ex.GetType().Name}: {ex.Message}");
            mqttClient?.Dispose();
            mqttClient = null;
            ScheduleReconnect();
        }
        finally
        {
            connectInProgress = false;
        }
    }

    public async Task<bool> PublishAsync(string topic, string payload)
    {
        if (!IsConnected)
        {
            LogWarning($"[MQTT Twin] Cannot publish to '{topic}' because the client is not connected.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            LogWarning("[MQTT Twin] Cannot publish MQTT message with empty topic.");
            return false;
        }

        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload ?? string.Empty)
                .Build();

            await mqttClient.PublishAsync(message, lifetimeCancellation.Token);
            LogInfo($"[MQTT Twin] Published '{topic}': {payload}");
            return true;
        }
        catch (OperationCanceledException)
        {
            LogWarning($"[MQTT Twin] Publish canceled for '{topic}'.");
            return false;
        }
        catch (Exception ex)
        {
            LogWarning($"[MQTT Twin] Failed to publish '{topic}'; scene will continue. Reason: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public async void Disconnect()
    {
        shuttingDown = true;
        await DisconnectAsync();
    }

    async Task DisconnectAsync()
    {
        if (mqttClient == null)
        {
            return;
        }

        try
        {
            if (mqttClient.IsConnected)
            {
                await mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
                LogInfo("[MQTT Twin] Disconnected.");
            }
        }
        catch (Exception ex)
        {
            LogWarning($"[MQTT Twin] Disconnect failed: {ex.Message}");
        }
        finally
        {
            mqttClient.Dispose();
            mqttClient = null;
        }
    }

    MqttClientOptions BuildClientOptions(MqttFactory factory)
    {
        string resolvedClientId = string.IsNullOrWhiteSpace(clientId)
            ? $"unity_twin_{Guid.NewGuid():N}"
            : clientId;

        var builder = factory.CreateClientOptionsBuilder()
            .WithClientId(resolvedClientId)
            .WithTcpServer(brokerHost, brokerPort)
            .WithCleanSession()
            .WithTimeout(TimeSpan.FromSeconds(10));

        if (useTls)
        {
            builder.WithTlsOptions(options => options.UseTls());
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            builder.WithCredentials(username, password);
        }

        return builder.Build();
    }

    async Task OnConnected(MqttClientConnectedEventArgs args)
    {
        reconnectScheduled = false;
        QueueLog(LogLevel.Info, $"[MQTT Twin] Connected. Subscribing to '{subscribeTopic}'.");

        if (string.IsNullOrWhiteSpace(subscribeTopic))
        {
            QueueLog(LogLevel.Warning, "[MQTT Twin] Subscribe topic is empty. No telemetry subscription created.");
            return;
        }

        try
        {
            var subscribeOptions = new MqttFactory()
                .CreateSubscribeOptionsBuilder()
                .WithTopicFilter(subscribeTopic)
                .Build();

            await mqttClient.SubscribeAsync(subscribeOptions, lifetimeCancellation.Token);
            QueueLog(LogLevel.Info, $"[MQTT Twin] Subscribed to '{subscribeTopic}'.");
        }
        catch (Exception ex)
        {
            QueueLog(LogLevel.Warning, $"[MQTT Twin] Subscribe failed: {ex.Message}");
        }
    }

    Task OnApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        var message = args.ApplicationMessage;
        string payload = DecodePayload(message.PayloadSegment);

        incomingMessages.Enqueue(new IncomingMqttMessage(message.Topic, payload));
        return Task.CompletedTask;
    }

    Task OnDisconnected(MqttClientDisconnectedEventArgs args)
    {
        if (shuttingDown)
        {
            return Task.CompletedTask;
        }

        QueueLog(LogLevel.Warning, $"[MQTT Twin] Disconnected from broker. Reason: {args.Reason}");
        ScheduleReconnect();
        return Task.CompletedTask;
    }

    void ProcessIncomingMessages()
    {
        EnsureTelemetryManager();

        while (incomingMessages.TryDequeue(out IncomingMqttMessage message))
        {
            if (!MqttTopicParser.TryParseTwinTelemetryTopic(
                    message.Topic,
                    out string cellId,
                    out string objectId,
                    out string metric))
            {
                LogWarning($"[MQTT Twin] Ignored invalid telemetry topic '{message.Topic}'.");
                continue;
            }

            if (message.Payload.Length == 0)
            {
                LogWarning($"[MQTT Twin] Received empty payload for '{message.Topic}'.");
            }

            telemetryManager.UpdateTelemetry(cellId, objectId, metric, message.Payload);
        }
    }

    void EnsureTelemetryManager()
    {
        if (telemetryManager != null)
        {
            return;
        }

        telemetryManager = TelemetryManager.Instance;

        if (telemetryManager == null)
        {
            telemetryManager = FindFirstObjectByType<TelemetryManager>();
        }

        if (telemetryManager == null)
        {
            var telemetryObject = new GameObject("TelemetryManager");
            telemetryManager = telemetryObject.AddComponent<TelemetryManager>();
            LogInfo("[MQTT Twin] Created runtime TelemetryManager because none was present in the scene.");
        }
    }

    void ScheduleReconnect()
    {
        if (reconnectScheduled ||
            !reconnectOnDisconnect ||
            shuttingDown ||
            lifetimeCancellation == null ||
            lifetimeCancellation.IsCancellationRequested)
        {
            return;
        }

        reconnectScheduled = true;
        _ = ReconnectAfterDelayAsync();
    }

    async Task ReconnectAfterDelayAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0.1f, reconnectDelaySeconds)), lifetimeCancellation.Token);

            if (!shuttingDown && !IsConnected)
            {
                QueueLog(LogLevel.Info, "[MQTT Twin] Attempting MQTT reconnect...");
                reconnectScheduled = false;
                await ConnectAsync();
            }
        }
        catch (OperationCanceledException)
        {
            reconnectScheduled = false;
        }
    }

    static string DecodePayload(ArraySegment<byte> payloadSegment)
    {
        if (payloadSegment.Array == null || payloadSegment.Count == 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(payloadSegment.Array, payloadSegment.Offset, payloadSegment.Count);
    }

    void LogInfo(string message)
    {
        Log(LogLevel.Info, message);
    }

    void LogWarning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    void Log(LogLevel level, string message)
    {
        if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
        {
            WriteLog(level, message);
        }
        else
        {
            QueueLog(level, message);
        }
    }

    void QueueLog(LogLevel level, string message)
    {
        mainThreadLogs.Enqueue(new QueuedLog(level, message));
    }

    void FlushQueuedLogs()
    {
        while (mainThreadLogs.TryDequeue(out QueuedLog log))
        {
            WriteLog(log.Level, log.Message);
        }
    }

    static void WriteLog(LogLevel level, string message)
    {
        switch (level)
        {
            case LogLevel.Warning:
                Debug.LogWarning(message);
                break;
            default:
                Debug.Log(message);
                break;
        }
    }

    readonly struct IncomingMqttMessage
    {
        public readonly string Topic;
        public readonly string Payload;

        public IncomingMqttMessage(string topic, string payload)
        {
            Topic = topic ?? string.Empty;
            Payload = payload ?? string.Empty;
        }
    }

    readonly struct QueuedLog
    {
        public readonly LogLevel Level;
        public readonly string Message;

        public QueuedLog(LogLevel level, string message)
        {
            Level = level;
            Message = message ?? string.Empty;
        }
    }

    enum LogLevel
    {
        Info,
        Warning
    }
}
