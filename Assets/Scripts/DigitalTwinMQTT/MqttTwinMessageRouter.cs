using UnityEngine;

public class MqttTwinMessageRouter : MonoBehaviour
{
    public static MqttTwinMessageRouter Instance { get; private set; }

    [Header("References")]
    public TelemetryManager telemetryManager;
    public TwinObjectStatusStore statusStore;
    public TwinControlRouter controlRouter;

    [Header("Behavior")]
    public bool autoCreateDependencies = true;
    public bool mirrorStatusToTelemetryManager = true;

    [Header("Diagnostics")]
    [SerializeField] int routedMessageCount;
    [SerializeField] int statusMessageCount;
    [SerializeField] int controlMessageCount;
    [SerializeField] int ignoredMessageCount;
    [SerializeField] string lastRoutedTopic = "";
    [SerializeField] string lastRoutedType = "";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureDependencies();
    }

    public bool RouteMessage(string topic, string cellId, string objectId, string messageType, string payload)
    {
        EnsureDependencies();

        routedMessageCount++;
        lastRoutedTopic = topic ?? string.Empty;
        lastRoutedType = messageType ?? string.Empty;

        switch (messageType)
        {
            case "status":
                if (statusStore == null)
                {
                    Debug.LogWarning("[MqttTwinMessageRouter] No TwinObjectStatusStore is assigned.");
                    return false;
                }

                statusMessageCount++;
                statusStore.UpdateStatus(cellId, objectId, payload);

                if (mirrorStatusToTelemetryManager && telemetryManager != null)
                {
                    telemetryManager.UpdateTelemetry(cellId, objectId, "status", payload);
                }

                return true;
            case "control":
                if (controlRouter == null)
                {
                    Debug.LogWarning("[MqttTwinMessageRouter] No TwinControlRouter is assigned.");
                    return false;
                }

                controlMessageCount++;
                return controlRouter.ApplyControl(cellId, objectId, payload);
            default:
                ignoredMessageCount++;
                Debug.LogWarning($"[MqttTwinMessageRouter] Ignored unsupported message type '{messageType}' on topic '{topic}'.");
                return false;
        }
    }

    void EnsureDependencies()
    {
        if (telemetryManager == null)
        {
            telemetryManager = TelemetryManager.Instance;
        }

        if (statusStore == null)
        {
            statusStore = TwinObjectStatusStore.Instance;
        }

        if (controlRouter == null)
        {
            controlRouter = TwinControlRouter.Instance;
        }

        if (statusStore == null && autoCreateDependencies)
        {
            statusStore = FindFirstObjectByType<TwinObjectStatusStore>();
        }

        if (controlRouter == null && autoCreateDependencies)
        {
            controlRouter = FindFirstObjectByType<TwinControlRouter>();
        }

        if (statusStore == null && autoCreateDependencies)
        {
            var storeObject = new GameObject("TwinObjectStatusStore");
            statusStore = storeObject.AddComponent<TwinObjectStatusStore>();
        }

        if (controlRouter == null && autoCreateDependencies)
        {
            var routerObject = new GameObject("TwinControlRouter");
            controlRouter = routerObject.AddComponent<TwinControlRouter>();
        }
    }
}
