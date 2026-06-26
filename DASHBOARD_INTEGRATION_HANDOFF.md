# Robo-Cell Unity Dashboard Integration Handoff

Date: 2026-06-26

This document summarizes the dashboard-related work done in this chat so another Codex/chat session can continue without needing the full conversation.

## Project Context

Project:

```text
C:\Unity Projects\Robo-Cell-Digital-Twin
```

Unity project:

```text
Robo-Cell-Digital-Twin / Unity Twin
```

Main scene used for dashboard work:

```text
Assets/Scenes/Test.unity
```

The project already had MQTT support before this dashboard work. Unity subscribes through `MqttTwinClient` to:

```text
factory/cell1/twin/+/control
factory/cell1/twin/+/status
```

Important design rule from the user:

- The 3D object dashboard displays status only.
- It must not send control commands.
- Later, an overlay dashboard may show warnings, notifications, and suggested actions.
- Actual Unity-to-factory commands should only be sent after user confirmation.
- MQTT status data should come from:

```text
factory/cell1/twin/{objectId}/status
```

## Migration Package

The user provided this package:

```text
M:\Study\SELF LEARNING\NTI\Digital_Twin\Technical\Project\Robocell Twin\Temp\CodexHandoff\DigitalTwinDashboard_Migration.zip
```

It was extracted for inspection only to:

```text
C:\Users\PC\AppData\Local\Temp\CodexDashboardMigration
```

The first important file in the package was:

```text
README_MIGRATION.md
```

The README said:

- Core dashboard scripts were under `Assets/Scripts/DigitalTwinDashboard/`.
- Dashboard prefab was under `Assets/My Prefabs/Dashboard.prefab`.
- `TelemetryManager.cs` in the package was reference-only.
- Robo-Cell already has its own MQTT `TelemetryManager`, so the package `TelemetryManager.cs` must not be imported.
- Correct integration is to adapt the dashboard to Robo-Cell's existing MQTT status/telemetry store.

## Package Files Reviewed

Core dashboard scripts:

```text
DashboardManager.cs
ObjectDashboard.cs
DashboardVisualEnhancer.cs
ClickableTwinObject.cs
```

Optional view-mode scripts:

```text
ViewModeManager.cs
FirstPersonWalkController.cs
ViewModeMenu.cs
```

Reference-only script:

```text
TelemetryManager.cs
```

Dashboard prefab:

```text
Dashboard.prefab
Dashboard.prefab.meta
```

Example payload file:

```text
example-status-payloads.json
```

## Important Package Findings

`DashboardManager.cs`:

- Singleton.
- Owns one active world-space dashboard instance.
- Can spawn the dashboard prefab near a clicked object.
- Supports raycast-based click detection.
- Supports Unity Input System and legacy input conditionally.
- Uses `ClickableTwinObject.objectId`.
- Calls `ObjectDashboard.SetTargetObject(objectId)`.
- Optionally adds `DashboardVisualEnhancer`.

`ObjectDashboard.cs` from the package:

- Originally read:

```csharp
TelemetryManager.Instance.GetObjectTelemetry(targetObjectId)
```

- It expected telemetry keys:

```text
speed
temperature
vibration
ai_status
```

- This was not compatible with Robo-Cell's current status store without adaptation.

`ClickableTwinObject.cs`:

- Has:

```csharp
public string objectId;
```

- Uses `OnMouseDown()`.
- Calls:

```csharp
DashboardManager.Instance.ShowDashboard(objectId.Trim(), GetDashboardAnchorPosition());
```

`DashboardVisualEnhancer.cs`:

- Runtime visual polish only.
- Adds industrial HUD styling, row backgrounds, live badge, updated-age text, and tether line.
- Does not publish MQTT or send commands.

Package `TelemetryManager.cs`:

- Not imported.
- It conflicts conceptually and by class name with Robo-Cell's existing:

```text
Assets/Scripts/DigitalTwinMQTT/TelemetryManager.cs
```

## Imported Assets

Imported into the Robo-Cell project:

```text
Assets/MyPrefabs/Dashboard.prefab
Assets/MyPrefabs/Dashboard.prefab.meta
Assets/Scripts/DigitalTwinDashboard/DashboardManager.cs
Assets/Scripts/DigitalTwinDashboard/DashboardManager.cs.meta
Assets/Scripts/DigitalTwinDashboard/ObjectDashboard.cs
Assets/Scripts/DigitalTwinDashboard/ObjectDashboard.cs.meta
Assets/Scripts/DigitalTwinDashboard/ClickableTwinObject.cs
Assets/Scripts/DigitalTwinDashboard/ClickableTwinObject.cs.meta
Assets/Scripts/DigitalTwinDashboard/DashboardVisualEnhancer.cs
Assets/Scripts/DigitalTwinDashboard/DashboardVisualEnhancer.cs.meta
```

Not imported:

```text
Assets/Scripts/DigitalTwinDashboard/TelemetryManager.cs
Assets/Scripts/DigitalTwinDashboard/ViewModeManager.cs
Assets/Scripts/DigitalTwinDashboard/FirstPersonWalkController.cs
Assets/Scripts/DigitalTwinDashboard/ViewModeMenu.cs
Assets/Scripts/DigitalTwinDashboard/Close button.controller
```

The project already had:

```text
Assets/MyPrefabs
```

So the dashboard prefab was placed there instead of creating `Assets/My Prefabs`.

## Dashboard Prefab Notes

Prefab:

```text
Assets/MyPrefabs/Dashboard.prefab
```

The prefab is a world-space Canvas with:

- `Canvas`
- `CanvasScaler`
- `GraphicRaycaster`
- root `ObjectDashboard` component
- close button wired to `ObjectDashboard.Close()`

Serialized `ObjectDashboard` TMP fields:

```text
objectNameText  -> ObjectName - data
speedText       -> speed - data
temperatureText -> temp - data (2)
vibrationText   -> vibration - data (4)
aiStatusText    -> Ai status - data (3)
```

Important GUID correction:

- `Dashboard.prefab` references `ObjectDashboard` by GUID:

```text
67e5e862479997349ba4d57f1a5b47ad
```

- `ObjectDashboard.cs.meta` was corrected to that GUID so the prefab script reference stays valid.

## Adapted Dashboard Behavior

`ObjectDashboard.cs` was rewritten/adapted to use Robo-Cell's existing status store:

```csharp
TwinObjectStatusStore.Instance.TryGetStatus(targetObjectId, out TwinObjectStatus status)
```

The dashboard now displays:

- object ID
- speed
- temperature
- vibration
- AI status

It does not publish MQTT.
It does not call `TwinCommandPublisher`.
It does not send factory commands.

Current display units are hardcoded in `ObjectDashboard`:

```text
speed       -> m/s
temperature -> C
vibration   -> mm/s
```

The dashboard refreshes periodically and also listens to:

```csharp
TwinObjectStatusStore.StatusUpdated
```

## Status Payload Parsing

Robo-Cell originally expected:

```json
{
  "aiStatus": "Normal"
}
```

The user said the real status payloads will use:

```json
{
  "ai_status": "Normal"
}
```

So `TwinJsonPayloads.cs` was updated to include both:

```csharp
public string aiStatus;
public string ai_status;
```

`TwinObjectStatusStore.cs` now maps AI status like this:

```csharp
AiStatus = !string.IsNullOrWhiteSpace(payload?.aiStatus)
    ? payload.aiStatus
    : payload?.ai_status ?? string.Empty;
```

This allows either camelCase or snake_case payloads.

## Example Status Payloads

Example topic:

```text
factory/cell1/twin/conveyor1/status
```

Example payload:

```json
{
  "objectId": "conveyor1",
  "state": "running",
  "health": "healthy",
  "running": true,
  "speed": 1.25,
  "temperature": 66.1,
  "vibration": 1.13,
  "ai_status": "Normal",
  "aiConfidence": 0.94,
  "message": "Conveyor operating normally",
  "recommendedAction": "No action required",
  "timestamp": 1782458400000
}
```

Example for a warning:

```text
factory/cell1/twin/shelvingA/status
```

```json
{
  "objectId": "shelvingA",
  "state": "loaded",
  "health": "warning",
  "running": false,
  "speed": 0,
  "temperature": 42.5,
  "vibration": 0.08,
  "ai_status": "Warning",
  "aiConfidence": 0.87,
  "message": "Shelf occupancy mismatch detected",
  "recommendedAction": "Verify inventory count",
  "timestamp": 1782458402000
}
```

## DashboardManager Scene Setup

A new GameObject was added directly to:

```text
Assets/Scenes/Test.unity
```

GameObject name:

```text
Dashboard Manager
```

It has:

```text
DashboardManager
```

Assigned fields:

```text
dashboardPrefab -> Assets/MyPrefabs/Dashboard.prefab
dashboardOffset -> (0, 2, 0)
enableRaycastClickDetection -> true
clickCamera -> Main Camera
clickableLayers -> everything
faceCameraOnOpen -> true
useEnhancedStyle -> true
presentationMode -> WorldSpace
```

`DashboardManager.cs` was also improved so that when it instantiates the world-space Canvas, it assigns:

```csharp
dashboardCanvas.worldCamera = cameraToUse;
```

This helps the close button and world-space UI raycasts.

## Original Target Object IDs

The original dashboard target IDs were:

```text
arm1
arm2
arm3
car1
car2
conveyor1
conveyor2
warehouse
```

After later changes, `warehouse` was replaced with:

```text
shelvingA
shelvingB
```

Current active target IDs are:

```text
arm1
arm2
arm3
car1
car2
conveyor1
conveyor2
shelvingA
shelvingB
```

## ClickableTwinObject Scene Wiring

`ClickableTwinObject` components were added directly to scene objects in `Test.unity`.

Current clickable IDs:

```text
conveyor1
conveyor2
car1
car2
arm1
arm2
arm3
shelvingA
shelvingB
```

The old `warehouse` clickable component was removed.

Current shelf mappings:

```text
ShelvingA scene object -> objectId: shelvingA
ShelvingB scene object -> objectId: shelvingB
```

The scene object names are currently:

```text
ShelvingA
ShelvingB
```

The MQTT/code IDs are:

```text
shelvingA
shelvingB
```

## Twin Object Registry Changes

GameObject:

```text
Twin Object Registry
```

Script:

```text
TwinObjectRegistry.cs
```

The old registry entry:

```text
objectId: warehouse
objectType: Warehouse
targetObject: Warehouse
```

was replaced with two entries:

```text
objectId: shelvingA
objectType: Warehouse
targetObject: ShelvingA
```

```text
objectId: shelvingB
objectType: Warehouse
targetObject: ShelvingB
```

This allows code using:

```csharp
TwinObjectRegistry.TryGetEntry("shelvingA", out entry)
TwinObjectRegistry.TryGetEntry("shelvingB", out entry)
```

to resolve them independently.

## MQTT Topics After Shelving Change

Use these status topics:

```text
factory/cell1/twin/shelvingA/status
factory/cell1/twin/shelvingB/status
```

Use these control topics:

```text
factory/cell1/twin/shelvingA/control
factory/cell1/twin/shelvingB/control
```

Old topic:

```text
factory/cell1/twin/warehouse/status
factory/cell1/twin/warehouse/control
```

is no longer wired to a clickable dashboard target or registry entry in `Test.unity`.

## Warehouse/Shelving Control Routing

`TwinControlRouter.cs` originally had a special fallback for object ID:

```text
warehouse
```

That fallback remains for backward compatibility, but `shelvingA` and `shelvingB` now route through the `TwinObjectRegistry` as `TwinObjectType.Warehouse`.

Important change:

Shelf-specific warehouse control messages are stored independently in:

```text
TwinWarehouseStateStore
```

by object ID.

To prevent both shelf messages from accidentally driving the same global scene snapshot, `TwinControlRouter.ApplyWarehouseControl()` now applies `WarehouseManager.ApplyExternalWarehouseSnapshot()` only for the legacy whole-warehouse path:

```csharp
bool isLegacyWholeWarehouse = entry == null ||
    string.Equals(objectId, "warehouse", StringComparison.OrdinalIgnoreCase);

if (applyWarehouseSnapshotsToScene && isLegacyWholeWarehouse)
{
    warehouseManager.ApplyExternalWarehouseSnapshot(
        payload.remainingInA,
        payload.deliveredToB,
        payload.batchStatus);
}
```

This means:

- `warehouse` control can still drive the global warehouse snapshot.
- `shelvingA` and `shelvingB` control store independent warehouse-state records.
- `shelvingA` and `shelvingB` do not both mutate the same global `WarehouseManager` visual state.

## Example Shelving Control Payload

Topic:

```text
factory/cell1/twin/shelvingA/control
```

Payload:

```json
{
  "objectId": "shelvingA",
  "remainingInA": 4,
  "deliveredToB": 0,
  "batchStatus": "Shelf A loaded",
  "timestamp": 1782458405000
}
```

Topic:

```text
factory/cell1/twin/shelvingB/control
```

Payload:

```json
{
  "objectId": "shelvingB",
  "remainingInA": 0,
  "deliveredToB": 3,
  "batchStatus": "Shelf B receiving finished parts",
  "timestamp": 1782458408000
}
```

These messages should update `TwinWarehouseStateStore` independently by object ID.

## Example Dashboard Status Test Flow

1. Enter Play Mode in Unity.
2. Publish:

```text
factory/cell1/twin/shelvingA/status
```

with payload:

```json
{
  "objectId": "shelvingA",
  "state": "ready",
  "health": "healthy",
  "speed": 0,
  "temperature": 39.2,
  "vibration": 0.04,
  "ai_status": "Normal",
  "message": "Shelving A ready",
  "recommendedAction": "No action required",
  "timestamp": 1782458410000
}
```

3. Click `ShelvingA` in the scene.
4. The world-space dashboard should open near `ShelvingA`.
5. It should show:

```text
Object: shelvingA
Speed: 0 m/s
Temperature: 39.2 C
Vibration: 0.04 mm/s
AI Status: Normal
```

## Fake Data Publisher Request

At the end of this chat, the user requested:

> make the twin status fake data publisher and assign it to the DashboardManager so that I can control it in runtime

Work on this was started conceptually but not implemented before the user requested this Markdown handoff file.

Recommended next implementation:

- Create a script such as:

```text
Assets/Scripts/DigitalTwinDashboard/TwinStatusFakeDataPublisher.cs
```

- Attach it to:

```text
Dashboard Manager
```

- Add a public/serialized reference in `DashboardManager`:

```csharp
public TwinStatusFakeDataPublisher fakeStatusPublisher;
```

- The fake publisher should write into:

```csharp
TwinObjectStatusStore.Instance.UpdateStatus("cell1", objectId, rawJson);
```

- It should not publish MQTT commands.
- It should not call `TwinCommandPublisher`.
- It should simulate only status data.

Suggested fake publisher controls:

```csharp
public bool publishOnStart = true;
public bool autoPublish = true;
public float publishInterval = 1f;
public string cellId = "cell1";
public string[] objectIds =
{
    "arm1",
    "arm2",
    "arm3",
    "car1",
    "car2",
    "conveyor1",
    "conveyor2",
    "shelvingA",
    "shelvingB"
};
```

Suggested runtime methods:

```csharp
[ContextMenu("Publish Once")]
public void PublishOnce();

[ContextMenu("Start Publishing")]
public void StartPublishing();

[ContextMenu("Stop Publishing")]
public void StopPublishing();
```

Suggested generated fake payload:

```json
{
  "objectId": "conveyor1",
  "state": "running",
  "health": "healthy",
  "running": true,
  "speed": 1.34,
  "temperature": 62.8,
  "vibration": 0.41,
  "ai_status": "Normal",
  "aiConfidence": 0.91,
  "message": "Fake status update",
  "recommendedAction": "No action required",
  "timestamp": 1782458420000
}
```

## Verification Notes

Static checks performed:

- Confirmed the reference package `TelemetryManager.cs` was not imported into `Assets/Scripts/DigitalTwinDashboard`.
- Confirmed only the existing Robo-Cell `TelemetryManager` remains under:

```text
Assets/Scripts/DigitalTwinMQTT/TelemetryManager.cs
```

- Confirmed dashboard prefab references the adapted `ObjectDashboard`.
- Confirmed `ObjectDashboard.cs.meta` GUID matches the prefab's script reference.
- Confirmed old `warehouse` registry/clickable target was removed from `Test.unity`.
- Confirmed `shelvingA` and `shelvingB` registry/click targets exist in `Test.unity`.

Unity compile verification limitation:

- `dotnet build` could not run because no .NET SDK is installed.
- Unity batchmode compile attempts were blocked/ineffective because the project appeared to be open/locked by existing Unity editor processes.
- One earlier Unity editor log showed compile errors from an intermediate version of `ObjectDashboard` that referenced extra fields (`DisplayName`, `SpeedUnit`, etc.).
- Those references were removed afterward.
- A clean final Unity compile was not obtained in this chat.

Git status limitation:

- `git status` could not run because Git reports dubious ownership for:

```text
C:/Unity Projects/Robo-Cell-Digital-Twin
```

- Global Git config was not changed.

## Important Files Changed

Dashboard files:

```text
Assets/MyPrefabs/Dashboard.prefab
Assets/MyPrefabs/Dashboard.prefab.meta
Assets/Scripts/DigitalTwinDashboard/DashboardManager.cs
Assets/Scripts/DigitalTwinDashboard/DashboardManager.cs.meta
Assets/Scripts/DigitalTwinDashboard/ObjectDashboard.cs
Assets/Scripts/DigitalTwinDashboard/ObjectDashboard.cs.meta
Assets/Scripts/DigitalTwinDashboard/ClickableTwinObject.cs
Assets/Scripts/DigitalTwinDashboard/ClickableTwinObject.cs.meta
Assets/Scripts/DigitalTwinDashboard/DashboardVisualEnhancer.cs
Assets/Scripts/DigitalTwinDashboard/DashboardVisualEnhancer.cs.meta
```

MQTT/status files:

```text
Assets/Scripts/DigitalTwinMQTT/TwinJsonPayloads.cs
Assets/Scripts/DigitalTwinMQTT/TwinObjectStatusStore.cs
Assets/Scripts/DigitalTwinMQTT/TwinControlRouter.cs
```

Scene:

```text
Assets/Scenes/Test.unity
```

Handoff document:

```text
DASHBOARD_INTEGRATION_HANDOFF.md
```

## Recommended Next Chat Prompt

Use something like:

```text
Continue from DASHBOARD_INTEGRATION_HANDOFF.md.
Implement TwinStatusFakeDataPublisher and attach it to the Dashboard Manager in Assets/Scenes/Test.unity.
It should feed TwinObjectStatusStore with fake status JSON for arm1, arm2, arm3, car1, car2, conveyor1, conveyor2, shelvingA, and shelvingB.
Do not publish commands. Do not import the old dashboard TelemetryManager.
```
