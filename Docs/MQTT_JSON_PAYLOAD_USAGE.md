# MQTT JSON Payload Usage

This document explains the JSON messages Unity currently expects from MQTT and what each key changes in the scene.

Unity subscribes to:

```text
factory/cell1/twin/+/control
factory/cell1/twin/+/status
```

Use `/control` to move or update scene objects. Use `/status` for dashboard/status data only.

Raw PLC tag topics such as `factory/cell1/plc/...` are not used by Unity anymore. Node-RED or the factory middleware should convert raw PLC/TIA tags into the semantic JSON shown here before publishing to Unity.

## Important Rules

- JSON field names are case-sensitive. Use `currentJ1`, not `CurrentJ1`.
- The object id in the MQTT topic is the routing key. If the payload `objectId` is different, Unity logs a warning and still routes by the topic.
- Unknown extra keys are ignored.
- Missing number fields become `0`.
- Missing boolean fields become `false`.
- Missing string fields become empty/null.
- `timestamp` is stored for diagnostics. It does not directly move anything.
- `source` and `type` are useful metadata, but Unity routes by topic object id and `TwinObjectRegistry`, not by the `type` string.

## Common Control Envelope

All recommended scene-control messages use this shape:

```json
{
  "objectId": "object_id_here",
  "type": "object_type_here",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {}
}
```

Common keys:

| Key | Type | Expected values | Scene effect |
| --- | --- | --- | --- |
| `objectId` | string | `arm1`, `arm2`, `arm3`, `car1`, `car2`, `conveyor1`, `conveyor2`, `warehouse` | Used as a consistency check. The MQTT topic object id is still the real route. |
| `type` | string | Suggested: `robot_arm`, `car`, `conveyor`, `warehouse` | Metadata only. Does not choose the receiver in the current code. |
| `source` | string | Suggested: `node_red`, `mqtt`, `factory`, `offline` | Metadata only. |
| `timestamp` | long/integer | Unix milliseconds from PLC/Node-RED if available | Stored in diagnostics and registry state. Does not affect scene motion. |
| `control` | object | Object-specific keys shown below | This is the data that affects the scene. |

## Robot Arm Control

Topic examples:

```text
factory/cell1/twin/arm1/control
factory/cell1/twin/arm2/control
factory/cell1/twin/arm3/control
```

Recommended JSON:

```json
{
  "objectId": "arm1",
  "type": "robot_arm",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "currentJ1": 30.0,
    "currentJ2": -20.0,
    "currentJ3": 45.0,
    "currentJ4": 0.0,
    "currentJ5": 10.0,
    "currentJ6": 90.0,
    "finger1State": true,
    "finger2State": true,
    "status": "Picking",
    "atPickTarget": true,
    "atDropTarget": false
  }
}
```

Control keys:

| Key | Type | Expected values | Scene effect |
| --- | --- | --- | --- |
| `currentJ1` | float | Joint angle in degrees | Moves joint 1 toward this angle. Axis comes from `RobotArmController.j1Axis`. |
| `currentJ2` | float | Joint angle in degrees | Moves joint 2 toward this angle. |
| `currentJ3` | float | Joint angle in degrees | Moves joint 3 toward this angle. |
| `currentJ4` | float | Joint angle in degrees | Moves joint 4 toward this angle. |
| `currentJ5` | float | Joint angle in degrees | Moves joint 5 toward this angle. |
| `currentJ6` | float | Joint angle in degrees | Moves joint 6 toward this angle. |
| `finger1State` | bool | `true` or `false` | `true` closes/moves the left finger to `localPosition.x = -0.04`; `false` opens it to `0`. |
| `finger2State` | bool | `true` or `false` | `true` closes/moves the right finger to `localPosition.x = 0.04`; `false` opens it to `0`. |
| `status` | string | Any label, for example `Idle`, `Moving`, `Picking`, `Dropping`, `Faulted` | Stored in diagnostics/registry. Does not directly move the arm. |
| `atPickTarget` | bool | `true` when the real arm has reached its pick position | Stored in diagnostics/registry. Can be used by other logic later. |
| `atDropTarget` | bool | `true` when the real arm has reached its drop position | Stored in diagnostics/registry. Can be used by other logic later. |

Cases:

| Case | Values to publish | Expected scene result |
| --- | --- | --- |
| Move arm only | Set `currentJ1` to `currentJ6`; keep fingers as current/desired values | Arm joints smoothly move toward the published angles. |
| Close gripper | `finger1State=true`, `finger2State=true` | Both configured finger transforms move to closed positions. |
| Open gripper | `finger1State=false`, `finger2State=false` | Both configured finger transforms move to open positions. |
| Mark pick reached | `atPickTarget=true`, `atDropTarget=false` | No direct motion, but Unity records that the arm is at the pick target. |
| Mark drop reached | `atPickTarget=false`, `atDropTarget=true` | No direct motion, but Unity records that the arm is at the drop target. |

Legacy arm JSON still accepted:

```json
{
  "objectId": "arm1",
  "joints": [30.0, -20.0, 45.0, 0.0, 10.0, 90.0],
  "finger1State": true,
  "finger2State": true,
  "status": "Picking",
  "atPickTarget": true,
  "atDropTarget": false,
  "timestamp": 1710000000000
}
```

Legacy arm rule: `joints` must contain at least six numbers. If it is missing or shorter than six values, Unity ignores the arm message.

## Car Control

Topic examples:

```text
factory/cell1/twin/car1/control
factory/cell1/twin/car2/control
```

Recommended JSON:

```json
{
  "objectId": "car1",
  "type": "car",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "currentX": 4.0,
    "currentZ": 6.0,
    "status": "MovingToArm1",
    "atPickTarget": true,
    "atDropTarget": false,
    "carrying": true
  }
}
```

Control keys:

| Key | Type | Expected values | Scene effect |
| --- | --- | --- | --- |
| `currentX` | float | World X position | Moves the car toward this X position. |
| `currentZ` | float | World Z position | Moves the car toward this Z position. |
| `status` | string | Any label, for example `Idle`, `MovingToPickup`, `MovingToDrop`, `Loading`, `Unloading` | Stored in diagnostics/registry. Does not directly move the car. |
| `atPickTarget` | bool | `true` when the real car reached its pick/loading point | Used with `carrying` for Car1 payload pickup visuals. |
| `atDropTarget` | bool | `true` when the real car reached its drop/unloading point | Used with `carrying` for Car2 shelf placement visuals. |
| `carrying` | bool | `true` when the car should visually carry a payload | Shows/attaches payload if configured. Hides payload when `false` if `hidePayloadWhenNotCarrying` is enabled. |

Cases:

| Case | Values to publish | Expected scene result |
| --- | --- | --- |
| Move car | Set `currentX` and `currentZ` | Car moves smoothly to the target X/Z position while keeping its current Y. |
| Car carries payload | `carrying=true` | Payload object is shown or instantiated and attached to the car payload anchor if configured. |
| Car no longer carries payload | `carrying=false` | Payload object is hidden if `hidePayloadWhenNotCarrying` is enabled. |
| Car1 pickup | Topic object id `car1`, `atPickTarget=true`, `carrying=true` | Unity shows payload on Car1 and hides the next assigned Shelving A item. |
| Car2 drop | Topic object id `car2`, `atDropTarget=true`, `carrying=true` | Unity places the carried/manufactured object into the next assigned Shelving B slot. |

Payload pickup/drop actions are transition-based. Repeating the same unchanged message should not keep creating duplicate shelf objects.

Legacy car JSON still accepted:

```json
{
  "objectId": "car1",
  "currentX": 4.0,
  "currentZ": 6.0,
  "status": "MovingToArm1",
  "atPickTarget": true,
  "atDropTarget": false,
  "carrying": true,
  "timestamp": 1710000000000
}
```

## Conveyor Control

Topic examples:

```text
factory/cell1/twin/conveyor1/control
factory/cell1/twin/conveyor2/control
```

Recommended JSON:

```json
{
  "objectId": "conveyor1",
  "type": "conveyor",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "currentSpeed": 1.25,
    "running": true,
    "objectDetected": true
  }
}
```

Control keys:

| Key | Type | Expected values | Scene effect |
| --- | --- | --- | --- |
| `currentSpeed` | float | Conveyor speed. Negative values are clamped to `0`. | Sets `ConveyorMotor.speed`. |
| `running` | bool | `true` or `false` | Simulates the conveyor run tag through `IO_Router.SimulateInput()` when a run tag exists. If no run tag exists, Unity can enable/disable the conveyor component. |
| `objectDetected` | bool | `true` when a real sensor detects an object | Simulates the configured object-detected input tag through `IO_Router.SimulateInput()` when that tag is configured. |

Cases:

| Case | Values to publish | Expected scene result |
| --- | --- | --- |
| Start conveyor | `running=true`, `currentSpeed` greater than `0` | Conveyor motor speed is updated, run input tag is simulated as true, conveyor moves if configured correctly. |
| Stop conveyor | `running=false` | Run input tag is simulated as false. If no run tag exists, the conveyor component can be disabled. |
| Object arrives at sensor | `objectDetected=true` | Object-detected input tag is simulated as true. |
| Sensor clears | `objectDetected=false` | Object-detected input tag is simulated as false. |
| Bad negative speed | `currentSpeed=-1` | Unity clamps speed to `0`. |

Legacy conveyor JSON still accepted:

```json
{
  "objectId": "conveyor1",
  "currentSpeed": 1.25,
  "running": true,
  "objectDetected": true,
  "timestamp": 1710000000000
}
```

## Warehouse Control

Topic:

```text
factory/cell1/twin/warehouse/control
```

Recommended JSON:

```json
{
  "objectId": "warehouse",
  "type": "warehouse",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "remainingInA": 8,
    "remainingInB": 2,
    "deliveredToB": 2,
    "batchStatus": "InProgress"
  }
}
```

Control keys:

| Key | Type | Expected values | Scene effect |
| --- | --- | --- | --- |
| `remainingInA` | int | `0` or greater. Negative values are clamped to `0`. | Sets how many fallback Shelving A items remain visible, or updates the warehouse manager snapshot. |
| `remainingInB` | int | `0` or greater. Negative values are clamped to `0`. | Sets how many fallback Shelving B items remain visible, or updates the warehouse manager delivered-to-B count. |
| `deliveredToB` | int | `0` or greater | Used as a fallback when `remainingInB` is `0` and `deliveredToB` is greater than `0`. |
| `batchStatus` | string | Any label, for example `Idle`, `InProgress`, `Complete`, `Blocked`, `Faulted` | Stored in the warehouse state and passed to `WarehouseManager.ApplyExternalWarehouseSnapshot()`. |

Cases:

| Case | Values to publish | Expected scene result |
| --- | --- | --- |
| Full shelf A, empty shelf B | `remainingInA=10`, `remainingInB=0`, `deliveredToB=0` | Warehouse manager/fallback visuals show A count as 10 and B count as 0. |
| One object moved from A to B | `remainingInA=9`, `remainingInB=1` | Warehouse snapshot updates counts. |
| Use delivered count naming | `remainingInA=9`, `remainingInB=0`, `deliveredToB=1` | Unity treats B count as 1 because `deliveredToB` is used when `remainingInB` is 0. |
| Batch finished | `batchStatus="Complete"` | Warehouse status is recorded as complete. Visual count still comes from the count keys. |

Legacy warehouse JSON still accepted:

```json
{
  "objectId": "warehouse",
  "remainingInA": 8,
  "deliveredToB": 2,
  "remainingInB": 2,
  "batchStatus": "InProgress",
  "timestamp": 1710000000000
}
```

## Status JSON

Status messages are received on `/status` topics. They update `TwinObjectStatusStore` and can mirror into `TelemetryManager`. They do not move arms, cars, conveyors, warehouse shelves, or payloads.

Topic examples:

```text
factory/cell1/twin/arm1/status
factory/cell1/twin/car1/status
factory/cell1/twin/conveyor1/status
factory/cell1/twin/warehouse/status
```

Example JSON:

```json
{
  "objectId": "conveyor1",
  "displayName": "Conveyor 1",
  "state": "Running",
  "status": "Running",
  "health": "Good",
  "running": true,
  "speed": 1.25,
  "speedUnit": "m/s",
  "temperature": 42.5,
  "temperatureUnit": "C",
  "vibration": 0.02,
  "vibrationUnit": "g",
  "aiStatus": "Normal",
  "aiConfidence": 0.98,
  "message": "Operating normally",
  "recommendedAction": "None",
  "timestamp": 1710000000000,
  "timestampUtc": "2026-06-26T10:00:00Z"
}
```

Status keys:

| Key | Type | Expected values | Scene effect |
| --- | --- | --- | --- |
| `objectId` | string | Same object id as topic when possible | Stored in status. If missing, topic object id is used. |
| `displayName` | string | Human-readable object name | Currently parsed but not stored in `TwinObjectStatus`. Safe metadata. |
| `state` | string | Preferred state label | Stored as the object's status state. Takes priority over `status`. |
| `status` | string | Fallback state label | Used only if `state` is missing/empty. |
| `health` | string | Suggested: `Good`, `Warning`, `Faulted`, `Unknown` | Stored for dashboard/status use. |
| `running` | bool | `true` or `false` | Stored for dashboard/status use. Does not start/stop scene motion. |
| `speed` | float | Any numeric speed | Stored for dashboard/status use. Does not set conveyor speed. |
| `speedUnit` | string | Example: `m/s`, `rpm` | Parsed but not stored in current status struct. |
| `temperature` | float | Numeric temperature | Stored for dashboard/status use. |
| `temperatureUnit` | string | Example: `C` | Parsed but not stored in current status struct. |
| `vibration` | float | Numeric vibration value | Stored for dashboard/status use. |
| `vibrationUnit` | string | Example: `g`, `mm/s` | Parsed but not stored in current status struct. |
| `aiStatus` | string | Example: `Normal`, `Anomaly`, `MaintenanceRecommended` | Stored for dashboard/status use. |
| `ai_status` | string | Same meaning as `aiStatus` | Fallback accepted if `aiStatus` is missing/empty. |
| `aiConfidence` | float | Usually `0.0` to `1.0` | Stored for dashboard/status use. |
| `message` | string | Any readable message | Stored for dashboard/status use. |
| `recommendedAction` | string | Any readable action | Stored for dashboard/status use. |
| `timestamp` | long/integer | Unix milliseconds from source | Stored as source timestamp. |
| `timestampUtc` | string | ISO UTC timestamp if available | Parsed but current store uses Unity receive time for `UtcTimestamp`. |

## Quick Topic And Payload Map

| Object type | Topic | Required scene-control keys |
| --- | --- | --- |
| Robot arm | `factory/cell1/twin/{armId}/control` | `control.currentJ1` to `control.currentJ6`, `control.finger1State`, `control.finger2State` |
| Car | `factory/cell1/twin/{carId}/control` | `control.currentX`, `control.currentZ`, `control.carrying` |
| Conveyor | `factory/cell1/twin/{conveyorId}/control` | `control.currentSpeed`, `control.running`, `control.objectDetected` |
| Warehouse | `factory/cell1/twin/warehouse/control` | `control.remainingInA`, `control.remainingInB` or `control.deliveredToB`, `control.batchStatus` |
| Any object status | `factory/cell1/twin/{objectId}/status` | Optional status/dashboard keys only |

## Node-RED Mapping Guidance

Node-RED should read raw PLC/TIA tags and publish semantic messages. For example:

| Raw PLC meaning | Recommended MQTT result |
| --- | --- |
| Arm joint actual positions | Publish `currentJ1` to `currentJ6` on the arm `/control` topic. |
| Gripper closed/open bits | Publish `finger1State` and `finger2State`. |
| Car X/Z actual position | Publish `currentX` and `currentZ` on the car `/control` topic. |
| Car load sensor or state bit | Publish `carrying`. |
| Conveyor motor run feedback | Publish `running`. |
| Conveyor actual speed | Publish `currentSpeed`. |
| Conveyor sensor feedback | Publish `objectDetected`. |
| Warehouse inventory counts | Publish `remainingInA`, `remainingInB`, and/or `deliveredToB`. |

Unity should receive meaningful object state, not raw PLC tag names.
