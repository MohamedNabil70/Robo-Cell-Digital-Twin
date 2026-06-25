using System;
using System.Globalization;
using System.Text;
using UnityEngine;

public class TwinCommandPublisher : MonoBehaviour
{
    [Header("Topic")]
    public string factoryRoot = "factory";
    public string cellId = "cell1";
    public string source = "unity_twin";

    [Header("Client")]
    public MqttTwinClient mqttClient;

    [Header("Button Defaults")]
    public string defaultObjectId = "conveyor1";
    public float defaultSpeed = 1.5f;

    [Header("Optional Test Input")]
    public bool enableKeyboardTestInput = false;
    public KeyCode testPublishKey = KeyCode.F8;

    void Awake()
    {
        if (mqttClient == null)
        {
            mqttClient = MqttTwinClient.Instance;
        }
    }

    void Update()
    {
        if (enableKeyboardTestInput && Input.GetKeyDown(testPublishKey))
        {
            PublishSetSpeed(defaultObjectId, defaultSpeed);
        }
    }

    public void PublishStartForDefaultObject()
    {
        PublishStart(defaultObjectId);
    }

    public void PublishStopForDefaultObject()
    {
        PublishStop(defaultObjectId);
    }

    public void PublishResetForDefaultObject()
    {
        PublishReset(defaultObjectId);
    }

    public void PublishSetSpeedForDefaultObject()
    {
        PublishSetSpeed(defaultObjectId, defaultSpeed);
    }

    public void PublishTestStart()
    {
        PublishStartForDefaultObject();
    }

    public void PublishTestSetSpeed()
    {
        PublishSetSpeedForDefaultObject();
    }

    public void PublishStart(string objectId)
    {
        PublishCustomCommand(objectId, "start");
    }

    public void PublishStop(string objectId)
    {
        PublishCustomCommand(objectId, "stop");
    }

    public void PublishReset(string objectId)
    {
        PublishCustomCommand(objectId, "reset");
    }

    public void PublishSetSpeed(string objectId, float speed)
    {
        PublishCommand(objectId, "set_speed", speed, "m/s", true);
    }

    public void PublishCustomCommand(string objectId, string command, object value = null)
    {
        PublishCommand(objectId, command, value, null, value != null);
    }

    public async void PublishCommand(string objectId, string command, object value, string unit = null, bool includeValue = false)
    {
        if (mqttClient == null)
        {
            mqttClient = MqttTwinClient.Instance;
        }

        if (mqttClient == null)
        {
            Debug.LogWarning("[TwinCommandPublisher] Cannot publish because no MqttTwinClient exists in the scene.");
            return;
        }

        if (string.IsNullOrWhiteSpace(objectId))
        {
            Debug.LogWarning("[TwinCommandPublisher] Cannot publish command with empty objectId.");
            return;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            Debug.LogWarning($"[TwinCommandPublisher] Cannot publish empty command for '{objectId}'.");
            return;
        }

        string topic = BuildCommandTopic(objectId);
        string payload = BuildCommandPayload(objectId, command, value, unit, includeValue);
        await mqttClient.PublishAsync(topic, payload);
    }

    string BuildCommandTopic(string objectId)
    {
        return $"{factoryRoot}/{cellId}/twin/commands/{objectId}";
    }

    string BuildCommandPayload(string objectId, string command, object value, string unit, bool includeValue)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        AppendJsonProperty(builder, "command", command);
        builder.Append(',');
        AppendJsonProperty(builder, "objectId", objectId);

        if (includeValue)
        {
            builder.Append(',');
            AppendJsonProperty(builder, "value", value);
        }

        if (!string.IsNullOrWhiteSpace(unit))
        {
            builder.Append(',');
            AppendJsonProperty(builder, "unit", unit);
        }

        builder.Append(',');
        AppendJsonProperty(builder, "source", source);
        builder.Append(',');
        AppendJsonProperty(builder, "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        builder.Append('}');
        return builder.ToString();
    }

    static void AppendJsonProperty(StringBuilder builder, string name, object value)
    {
        builder.Append('"');
        builder.Append(EscapeJson(name));
        builder.Append("\":");
        builder.Append(ToJsonValue(value));
    }

    static string ToJsonValue(object value)
    {
        if (value == null)
        {
            return "null";
        }

        if (value is string stringValue)
        {
            return $"\"{EscapeJson(stringValue)}\"";
        }

        if (value is bool boolValue)
        {
            return boolValue ? "true" : "false";
        }

        if (value is float floatValue)
        {
            return floatValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value is double doubleValue)
        {
            return doubleValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value is decimal decimalValue)
        {
            return decimalValue.ToString(CultureInfo.InvariantCulture);
        }

        if (value is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture);
        }

        return $"\"{EscapeJson(value.ToString())}\"";
    }

    static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
