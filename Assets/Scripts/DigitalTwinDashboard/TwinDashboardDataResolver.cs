using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum TwinDashboardObjectKind
{
    Unknown,
    Conveyor,
    Arm
}

public readonly struct TwinDashboardField
{
    public readonly string Label;
    public readonly string Value;

    public TwinDashboardField(string label, string value)
    {
        Label = label ?? string.Empty;
        Value = value ?? string.Empty;
    }
}

public sealed class TwinDashboardDataSnapshot
{
    public string ObjectId;
    public string DisplayName;
    public TwinDashboardObjectKind ObjectKind;
    public readonly List<TwinDashboardField> Fields = new List<TwinDashboardField>();
}

/// <summary>
/// Single read-only source for dashboard display content. It reads local status and
/// telemetry caches only; it never publishes commands or sends MQTT messages.
/// </summary>
public static class TwinDashboardDataResolver
{
    const string MissingValue = "N/A";

    public static TwinDashboardDataSnapshot Resolve(string objectId)
    {
        string trimmedObjectId = string.IsNullOrWhiteSpace(objectId) ? string.Empty : objectId.Trim();
        TwinObjectStatus status = default;
        bool hasStatus = TwinObjectStatusStore.Instance != null &&
                         TwinObjectStatusStore.Instance.TryGetStatus(trimmedObjectId, out status);

        IReadOnlyDictionary<string, TelemetryRecord> telemetry =
            TelemetryManager.Instance != null ? TelemetryManager.Instance.GetObjectTelemetry(trimmedObjectId) : null;

        TwinDashboardObjectKind objectKind = ResolveKind(trimmedObjectId, hasStatus ? status.ObjectType : string.Empty, telemetry);
        var snapshot = new TwinDashboardDataSnapshot
        {
            ObjectId = trimmedObjectId,
            DisplayName = ResolveDisplayName(trimmedObjectId, hasStatus ? status.DisplayName : string.Empty, telemetry),
            ObjectKind = objectKind
        };

        switch (objectKind)
        {
            case TwinDashboardObjectKind.Arm:
                AddArmFields(snapshot, status, hasStatus, telemetry);
                break;
            case TwinDashboardObjectKind.Conveyor:
            default:
                AddConveyorFields(snapshot, status, hasStatus, telemetry);
                break;
        }

        return snapshot;
    }

    static void AddConveyorFields(
        TwinDashboardDataSnapshot snapshot,
        TwinObjectStatus status,
        bool hasStatus,
        IReadOnlyDictionary<string, TelemetryRecord> telemetry)
    {
        snapshot.Fields.Add(new TwinDashboardField(
            "Speed",
            ResolveNumber(
                hasStatus ? status.Speed : 0f,
                hasStatus,
                "0.## m/s",
                telemetry,
                "speed",
                "currentSpeed",
                "belt_speed",
                "tel_BeltSpeed")));

        snapshot.Fields.Add(new TwinDashboardField(
            "Temperature",
            ResolveNumber(
                hasStatus ? status.Temperature : 0f,
                hasStatus,
                "0.## C",
                telemetry,
                "temperature",
                "motor_temperature",
                "tel_MotorTemperature")));

        snapshot.Fields.Add(new TwinDashboardField(
            "Vibration",
            ResolveNumber(
                hasStatus ? status.Vibration : 0f,
                hasStatus,
                "0.## mm/s",
                telemetry,
                "vibration",
                "vibration_rms",
                "tel_Vibration")));

        snapshot.Fields.Add(new TwinDashboardField(
            "AI Status",
            FirstNonEmpty(
                hasStatus ? status.AiStatus : string.Empty,
                TryGetTelemetryValue(telemetry, "aiStatus", "ai_status", "ai_status_text"),
                MissingValue)));
    }

    static void AddArmFields(
        TwinDashboardDataSnapshot snapshot,
        TwinObjectStatus status,
        bool hasStatus,
        IReadOnlyDictionary<string, TelemetryRecord> telemetry)
    {
        snapshot.Fields.Add(new TwinDashboardField(
            "rul_cycles",
            ResolveNumber(
                hasStatus ? status.RulCycles : 0f,
                hasStatus && status.RulCycles > 0f,
                "0",
                telemetry,
                "rul_cycles",
                "rulCycles",
                "remaining_useful_life_cycles")));

        snapshot.Fields.Add(new TwinDashboardField(
            "health_score",
            ResolveNumber(
                hasStatus ? status.HealthScore : 0f,
                hasStatus && status.HealthScore > 0f,
                "0.##",
                telemetry,
                "health_score",
                "healthScore")));

        snapshot.Fields.Add(new TwinDashboardField(
            "health_state",
            FirstNonEmpty(
                hasStatus ? status.HealthState : string.Empty,
                hasStatus ? status.Health : string.Empty,
                TryGetTelemetryValue(telemetry, "health_state", "healthState"),
                MissingValue)));

        snapshot.Fields.Add(new TwinDashboardField(
            "maintenance_required",
            ResolveBool(hasStatus ? status.MaintenanceRequired : false, hasStatus, telemetry, "maintenance_required", "maintenanceRequired")));

        snapshot.Fields.Add(new TwinDashboardField(
            "Status / fault_type",
            $"{FirstNonEmpty(hasStatus ? status.State : string.Empty, TryGetTelemetryValue(telemetry, "status", "state"), MissingValue)} / {FirstNonEmpty(hasStatus ? status.FaultType : string.Empty, TryGetTelemetryValue(telemetry, "fault_type", "faultType"), "None")}"));
    }

    static TwinDashboardObjectKind ResolveKind(
        string objectId,
        string explicitType,
        IReadOnlyDictionary<string, TelemetryRecord> telemetry)
    {
        string type = FirstNonEmpty(explicitType, TryGetTelemetryValue(telemetry, "objectType", "object_type"));
        if (Contains(type, "arm") || Contains(objectId, "arm"))
        {
            return TwinDashboardObjectKind.Arm;
        }

        if (Contains(type, "conveyor") || Contains(objectId, "conveyor"))
        {
            return TwinDashboardObjectKind.Conveyor;
        }

        return TwinDashboardObjectKind.Unknown;
    }

    static string ResolveDisplayName(
        string objectId,
        string statusDisplayName,
        IReadOnlyDictionary<string, TelemetryRecord> telemetry)
    {
        return FirstNonEmpty(statusDisplayName, TryGetTelemetryValue(telemetry, "displayName", "display_name"), objectId, MissingValue);
    }

    static string ResolveNumber(
        float statusValue,
        bool canUseStatusValue,
        string format,
        IReadOnlyDictionary<string, TelemetryRecord> telemetry,
        params string[] keys)
    {
        string telemetryValue = TryGetTelemetryValue(telemetry, keys);
        if (!string.IsNullOrWhiteSpace(telemetryValue))
        {
            return telemetryValue;
        }

        return canUseStatusValue ? statusValue.ToString(format, CultureInfo.InvariantCulture) : MissingValue;
    }

    static string ResolveBool(
        bool statusValue,
        bool hasStatus,
        IReadOnlyDictionary<string, TelemetryRecord> telemetry,
        params string[] keys)
    {
        string telemetryValue = TryGetTelemetryValue(telemetry, keys);
        if (!string.IsNullOrWhiteSpace(telemetryValue))
        {
            return telemetryValue;
        }

        return hasStatus ? statusValue.ToString() : MissingValue;
    }

    static string TryGetTelemetryValue(IReadOnlyDictionary<string, TelemetryRecord> telemetry, params string[] keys)
    {
        if (telemetry == null || keys == null)
        {
            return string.Empty;
        }

        foreach (string key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            foreach (KeyValuePair<string, TelemetryRecord> pair in telemetry)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value.Value;
                }
            }
        }

        return string.Empty;
    }

    static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
        {
            return string.Empty;
        }

        foreach (string value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    static bool Contains(string value, string fragment)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
