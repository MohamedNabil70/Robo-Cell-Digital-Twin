# Handoff: Payload Visual Ownership Rule

## Context

This Unity C# Digital Twin project receives MQTT JSON control messages and routes them to scene receivers for cars, conveyors, warehouse visuals, arms, and related objects.

The latest user request introduced a payload visual ownership rule:

- When `WarehouseControlMode` is `EventDrivenPayloadFlow`, `WarehouseManager` must be the single source of truth for payload visuals.
- In that mode, car scripts, conveyor scripts, and arm scripts may still expose/control state such as `carrying`, `objectDetected`, `atPickTarget`, `atDropTarget`, and finger states.
- They must not independently spawn, hide, instantiate, destroy, or move payload visuals while `WarehouseManager` owns the visual flow.

This was requested to prevent duplicate payloads, conflicting visibility, and inconsistent payload ownership.

## Files Changed

- `Assets/NewScript/WarehouseManager.cs`
- `Assets/Scripts/DigitalTwinControl/CarControlReceiver.cs`
- `Assets/Scripts/DigitalTwinControl/ConveyorControlReceiver.cs`

## What Was Implemented

### WarehouseManager

Added a new enum:

```csharp
public enum WarehouseControlMode
{
    DirectSnapshot,
    EventDrivenPayloadFlow
}
```

Added an Inspector field:

```csharp
public WarehouseControlMode warehouseControlMode = WarehouseControlMode.DirectSnapshot;
```

Behavior:

- `DirectSnapshot` preserves the existing `warehouse/control` behavior using `remainingInA` and `remainingInB`.
- `EventDrivenPayloadFlow` makes `WarehouseManager` the intended owner of payload visuals.
- `WarehouseManager` now applies payload ownership mode in `Awake()`, `Start()`, and if the mode changes during Play Mode.
- It finds all `CarControlReceiver` and `ConveyorControlReceiver` instances, including inactive ones, and sets their local payload visual switch.
- It has a context menu method:

```csharp
ApplyPayloadVisualOwnershipMode()
```

- When `warehouseControlMode == EventDrivenPayloadFlow`, `ApplyExternalWarehouseSnapshot(...)` now ignores direct count snapshots and logs one warning. This prevents old `remainingInA/remainingInB` messages from still driving shelves while the event-driven mode is active.

### CarControlReceiver

Added:

```csharp
public bool allowLocalPayloadVisuals = true;

public void SetLocalPayloadVisualsAllowed(bool allowed)
{
    allowLocalPayloadVisuals = allowed;
}
```

Behavior:

- Car movement and MQTT state tracking still work.
- `lastCarrying`, `lastAtPickTarget`, `lastAtDropTarget`, diagnostics, and motion are still updated.
- When `allowLocalPayloadVisuals == false`, the script no longer:
  - Shows or hides payloads on the car.
  - Instantiates `payloadOnCarPrefab`.
  - Hides a ShelvingA item on Car1 pickup.
  - Places manufactured objects into ShelvingB on Car2 drop.

### ConveyorControlReceiver

Added:

```csharp
public bool allowLocalPayloadVisuals = true;

public void SetLocalPayloadVisualsAllowed(bool allowed)
{
    allowLocalPayloadVisuals = allowed;
}
```

Behavior:

- Conveyor MQTT state tracking still works.
- `objectDetected`, `running`, `goalSpeed`, and IO tag simulation still update.
- When `allowLocalPayloadVisuals == false`, the script no longer:
  - Instantiates a cube or turbine on the conveyor.
  - Hides conveyor payload visuals.
  - Moves the locally owned `objectOnConveyor`.

## Arm Receiver

`ArmControlReceiver.cs` was inspected but not changed.

Reason:

- It currently controls joint and finger state only.
- It does not spawn, hide, destroy, or move payload visuals.

## Important Assumptions

- `WarehouseManager` does not yet implement the full event-driven payload flow described in the larger pasted request.
- This change only establishes safe ownership boundaries so the future `WarehouseManager` flow can be built without local car/conveyor scripts creating duplicate visuals.
- Default mode remains `DirectSnapshot` to preserve existing behavior unless the user switches `warehouseControlMode` to `EventDrivenPayloadFlow` in the Inspector.

## Validation Done

Unity compile was requested after the edits.

Results:

- Unity console errors: `0`
- `CarControlReceiver.cs` validation: `0` errors
- `ConveyorControlReceiver.cs` validation: `0` errors
- `WarehouseManager.cs` validation: `0` errors

Unity MCP reported only generic existing-style warnings, such as `FixedUpdate` suggestions and string-concatenation warnings.

## Next Suggested Work

If continuing the event-driven payload flow, the next chat should implement the actual `WarehouseManager` transition/state machine for:

- ShelvingA -> Car1
- Car1 -> Arm1
- Arm1 -> Conveyor1
- Conveyor1 -> Arm2
- Arm2 -> CNC
- CNC cube -> turbine conversion
- CNC/output conveyor -> Arm3
- Arm3 -> Car2
- Car2 -> ShelvingB

Use edge detection so repeated MQTT state messages do not repeat visual events.
