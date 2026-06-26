# Unity Twin MQTT Handoff Summary

This file summarizes the work done in this chat so another Codex/chat session can continue without losing context.

## Original Goal

The Unity project is an existing Unity digital twin called `Unity Twin`.

The user's friend had another version of the project representing the actual factory asset. That version used PLC signals/tags through Siemens TIA Portal and an OPC UA/TCP bridge. The user provided those scripts in:

```text
M:/Study/SELF LEARNING/NTI/Digital_Twin/Technical/Project/New_scripts.rar
```

The new goal is:

- Do not let this Unity project decide the manufacturing sequence locally.
- Unity Twin should mirror the real factory state.
- Factory/PLC/TIA feedback should be mapped by Node-RED or another bridge into MQTT semantic JSON.
- Unity should receive MQTT `/control` messages and make those values control object motion, scene flow, payloads, warehouse visuals, conveyors, arms, and cars.
- `/state` topics are dashboard/monitoring only and should not control scene motion.
- The old TCP/OPC UA `UnityBridgeClient` bridge is no longer used for normal runtime.

## Friend Scripts Inspected

The `.rar` archive was extracted and inspected under:

```text
Temp/CodexNewScripts_FromFriend
```

Important scripts from that archive:

```text
ConveyorMotor.cs
CubeProcessor.cs
DataBus.cs
GenericCommandButton.cs
IO_Router.cs
ProcessingDefectReport.cs
Program.cs
QualityControlStation.cs
RobotArmController.cs
RobotCar1.cs
RobotCar2.cs
SceneResetListener.cs
SensorTrigger.cs
TagSubscriptionHelper.cs
UnityBridgeClient.cs
ValueChangeDetector.cs
WarehouseManager.cs
```

The project already contained many of these scripts under:

```text
Assets/NewScript
```

Those scripts had legacy PLC/bridge behavior. We reused the scene logic where useful, but redirected runtime control toward MQTT and local/offline `IO_Router` state.

## Main MQTT Architecture

Runtime scene control is now based on semantic MQTT control messages.

Primary topic format:

```text
factory/cell1/twin/{objectId}/control
```

Examples:

```text
factory/cell1/twin/arm1/control
factory/cell1/twin/arm2/control
factory/cell1/twin/arm3/control
factory/cell1/twin/car1/control
factory/cell1/twin/car2/control
factory/cell1/twin/conveyor1/control
factory/cell1/twin/conveyor2/control
factory/cell1/twin/warehouse/control
```

Runtime path:

1. `MqttTwinClient` subscribes to `factory/cell1/twin/+/control`.
2. MQTT callbacks enqueue messages in a thread-safe queue.
3. `MqttTwinClient.Update()` processes messages on Unity's main thread.
4. `MqttTwinMessageRouter` routes `/control` messages to `TwinControlRouter`.
5. `TwinControlRouter` parses semantic JSON and routes by `objectId` through `TwinObjectRegistry`.
6. Receiver components apply the latest target state and hold it until a new MQTT message arrives.

Important files:

```text
Assets/Scripts/DigitalTwinMQTT/MqttTwinClient.cs
Assets/Scripts/DigitalTwinMQTT/MqttTwinMessageRouter.cs
Assets/Scripts/DigitalTwinMQTT/MqttTopicParser.cs
Assets/Scripts/DigitalTwinMQTT/TwinControlRouter.cs
Assets/Scripts/DigitalTwinMQTT/TwinJsonPayloads.cs
Assets/Scripts/DigitalTwinMQTT/TwinObjectRegistry.cs
Assets/Scripts/DigitalTwinMQTT/TwinWarehouseStateStore.cs
Assets/Scripts/DigitalTwinMQTT/TwinObjectStatusStore.cs
```

New control receivers:

```text
Assets/Scripts/DigitalTwinControl/ArmControlReceiver.cs
Assets/Scripts/DigitalTwinControl/CarControlReceiver.cs
Assets/Scripts/DigitalTwinControl/ConveyorControlReceiver.cs
Assets/Scripts/DigitalTwinControl/WarehouseControlReceiver.cs
```

Documentation already created:

```text
Docs/PLC_MQTT_FEEDBACK.md
```

## Semantic MQTT Message Shape

Every runtime control message should use this envelope:

```json
{
  "objectId": "object_id_here",
  "type": "object_type_here",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {}
}
```

Unity routes by the `{objectId}` in the MQTT topic. If the payload `objectId` differs from the topic object id, the router warns and uses the topic object id.

## Object Registry

`TwinObjectRegistry` should contain entries for:

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

The registry entries can reference:

```text
RobotArmController
ConveyorMotor
car Transform
ArmControlReceiver
CarControlReceiver
ConveyorControlReceiver
WarehouseControlReceiver
optional IO tags
```

`TwinControlRouter.autoAddReceiverComponents` is enabled by default, so it can add receiver components at runtime. For reliable visuals, add them in the scene and assign references in the Inspector.

## Arm Control

Controllable objects:

```text
arm1
arm2
arm3
```

Topics:

```text
factory/cell1/twin/arm1/control
factory/cell1/twin/arm2/control
factory/cell1/twin/arm3/control
```

Example payload for `arm1`:

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

Same shape for `arm2`:

```json
{
  "objectId": "arm2",
  "type": "robot_arm",
  "source": "node_red",
  "timestamp": 1710000000100,
  "control": {
    "currentJ1": 0.0,
    "currentJ2": 35.0,
    "currentJ3": -15.0,
    "currentJ4": 20.0,
    "currentJ5": 5.0,
    "currentJ6": 0.0,
    "finger1State": false,
    "finger2State": false,
    "status": "MovingToCNC",
    "atPickTarget": false,
    "atDropTarget": false
  }
}
```

Same shape for `arm3`:

```json
{
  "objectId": "arm3",
  "type": "robot_arm",
  "source": "node_red",
  "timestamp": 1710000000200,
  "control": {
    "currentJ1": -25.0,
    "currentJ2": 10.0,
    "currentJ3": 60.0,
    "currentJ4": -10.0,
    "currentJ5": 15.0,
    "currentJ6": 45.0,
    "finger1State": true,
    "finger2State": true,
    "status": "DropToCar2",
    "atPickTarget": false,
    "atDropTarget": true
  }
}
```

Receiver behavior:

- `ArmControlReceiver` smoothly moves each joint toward the latest target angle.
- Joint axes are taken from the existing `RobotArmController`.
- Joint units are assumed to be degrees.
- Finger rules:
  - `finger1State=true`: left finger `localPosition.x = -0.04`
  - `finger1State=false`: left finger `localPosition.x = 0`
  - `finger2State=true`: right finger `localPosition.x = 0.04`
  - `finger2State=false`: right finger `localPosition.x = 0`

Inspector references:

```text
RobotArmController
joint1..joint6
leftFingerTransform
rightFingerTransform
```

## Car Control

Controllable objects:

```text
car1
car2
```

Topics:

```text
factory/cell1/twin/car1/control
factory/cell1/twin/car2/control
```

Example payload for `car1`:

```json
{
  "objectId": "car1",
  "type": "car",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "currentX": 4.0,
    "currentZ": 6.0,
    "status": "AtPickTarget",
    "atPickTarget": true,
    "atDropTarget": false,
    "carrying": true
  }
}
```

Example payload for `car2`:

```json
{
  "objectId": "car2",
  "type": "car",
  "source": "node_red",
  "timestamp": 1710000000300,
  "control": {
    "currentX": -3.0,
    "currentZ": 8.5,
    "status": "AtDropTarget",
    "atPickTarget": false,
    "atDropTarget": true,
    "carrying": true
  }
}
```

Receiver behavior:

- `CarControlReceiver` moves the car smoothly in world X/Z.
- The car keeps its current Y from the scene.
- Car payload actions are transition-based to avoid duplicates.
- Car1:
  - If `atPickTarget=true` and `carrying=true`, show payload on the car.
  - Hide one assigned Shelving A item.
  - This happens only on transition, not every frame/message.
- Car2:
  - If `atDropTarget=true` and `carrying=true`, place one manufactured object in the next Shelving B slot.
  - This also happens only on transition.

Inspector references:

```text
carTransform
carPayloadAnchor
payloadOnCarObject or payloadOnCarPrefab
shelvingAItems
shelvingBSlots
manufacturedObjectPrefab
```

## Conveyor Control

Controllable objects:

```text
conveyor1
conveyor2
```

Topics:

```text
factory/cell1/twin/conveyor1/control
factory/cell1/twin/conveyor2/control
```

Example payload for `conveyor1`:

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

Example payload for `conveyor2`:

```json
{
  "objectId": "conveyor2",
  "type": "conveyor",
  "source": "node_red",
  "timestamp": 1710000000400,
  "control": {
    "currentSpeed": 0.75,
    "running": false,
    "objectDetected": false
  }
}
```

Receiver behavior:

- `ConveyorControlReceiver` sets `ConveyorMotor.speed`.
- It can feed `running` and `objectDetected` into existing local tag callbacks using `IO_Router.SimulateInput`.
- It can force `ConveyorMotor.offlineMode=false` when a running tag is configured, so existing conveyor logic uses the simulated tag.
- If no running tag is configured, it can enable/disable the `ConveyorMotor` directly.

Inspector references:

```text
ConveyorMotor
runningTagOverride
objectDetectedTagOverride
```

## Warehouse Control

Controllable object:

```text
warehouse
```

Topic:

```text
factory/cell1/twin/warehouse/control
```

Example payload:

```json
{
  "objectId": "warehouse",
  "type": "warehouse",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "remainingInA": 8,
    "remainingInB": 2,
    "batchStatus": "InProgress"
  }
}
```

Alternative payload if the source uses delivered count:

```json
{
  "objectId": "warehouse",
  "type": "warehouse",
  "source": "node_red",
  "timestamp": 1710000000500,
  "control": {
    "remainingInA": 6,
    "deliveredToB": 4,
    "batchStatus": "Running"
  }
}
```

Receiver behavior:

- `WarehouseControlReceiver` reuses `WarehouseManager.ApplyExternalWarehouseSnapshot`.
- If `WarehouseManager` is not available, it can use fallback item arrays for Shelving A and Shelving B.
- `remainingInB` is currently mapped into the existing delivered-to-B visual count because the existing Unity API uses delivered-to-B semantics.

TODO:

- Confirm whether the real factory source should publish `remainingInB` or `deliveredToB`.
- Current code supports both, but the meaning should be standardized.

Inspector references:

```text
WarehouseManager
shelvingAItems
shelvingBItems
```

## Status And State Topics

The project still has dashboard/status support.

Status topics:

```text
factory/cell1/twin/{objectId}/status
```

Examples:

```text
factory/cell1/twin/arm1/status
factory/cell1/twin/car1/status
factory/cell1/twin/conveyor1/status
factory/cell1/twin/warehouse/status
```

These are stored by `TwinObjectStatusStore` and may mirror into `TelemetryManager`.

Important rule:

```text
/control controls scene motion and visuals.
/state is dashboard/monitoring only.
/status is status/dashboard data only.
```

Do not drive object motion from `/state`.

## Raw PLC Tag MQTT Support Removed

The MQTT manager no longer subscribes to raw PLC tag topic families such as `factory/cell1/plc/...` or `factory/cell1/tag/...`.

Removed MQTT topic families:

```text
factory/cell1/plc/tag/#
factory/cell1/plc/tags/#
factory/cell1/plc/feedback/#
factory/cell1/tag/#
factory/cell1/tags/#
```

Removed code paths:

```text
MqttTwinClient.plcTagSubscribeTopics
MqttTopicParser.TryParsePlcTagFeedbackTopic()
MqttTwinMessageRouter.RoutePlcTagFeedback()
TwinPlcTagAlias
```

Current rule:

```text
Node-RED or the factory middleware maps raw PLC/TIA tags into semantic /control JSON before Unity receives them.
Unity does not subscribe to raw PLC tag MQTT topics for production control.
```

Local/offline tag simulation was not removed. Existing scripts can still call `IO_Router.SimulateInput()` or local tag callbacks inside Unity. Only the raw PLC MQTT subscription path was removed.

## Old TCP Bridge Disabled

The project previously used `UnityBridgeClient` to connect to an OPC UA/TCP bridge program. The user said this bridge is no longer used.

Changes made:

```text
Assets/NewScript/UnityBridgeClient.cs
Assets/NewScript/IO_Router.cs
Assets/NewScript/SensorTrigger.cs
```

Current behavior:

- `UnityBridgeClient.enableTcpBridge = false` by default.
- When disabled, it does not connect, retry, send, or log bridge warnings.
- `IO_Router.sendOutputsToBridge = false` by default.
- `IO_Router.autoFindBridgeClient = false` by default.
- `IO_Router.SetValue()` and `SetValueWithHandoff()` only call `bridge.Send()` when `sendOutputsToBridge=true`.
- `IO_Router` debug mode reports `MQTT` when bridge sending is disabled.
- Sensor/bridge mismatch warnings are gated behind `sendOutputsToBridge`.

If somebody intentionally wants the old bridge again, both flags must be enabled:

```text
UnityBridgeClient.enableTcpBridge = true
IO_Router.sendOutputsToBridge = true
```

## PLC Tag Warning Cleanup

The Unity log showed warnings like:

```text
[TAG-SUB | FirstRobot] BARE TAG NAME: 'OPC_In_Arm1_Restart_Bit'
[TAG-SUB | C3] Tag 'OPC_In_Process_Cube3_BIt' NEVER received after 5 retries.
```

Those warnings came from `TagSubscriptionHelper`, which assumed live TIA/bridge usage.

Changes made:

```text
Assets/NewScript/TagSubscriptionHelper.cs
Assets/NewScript/ConveyorMotor.cs
Assets/NewScript/IO_Router.cs
Assets/NewScript/RobotArmController.cs
Assets/NewScript/SensorTrigger.cs
```

Current behavior:

- Bare/unqualified tag warnings are suppressed in MQTT/local mode.
- "Never received after retries" warnings are suppressed in MQTT/local mode.
- Tag registration logs show `[MQTT/local]` when the bridge is disabled.
- `TagSubscriptionHelper.DiagnoseAll()` reports `Mode: MQTT/local` and `Bridge: disabled`.
- Conveyor empty `plcTag` warnings are gated behind `sendOutputsToBridge`.
- Arm startup PLC trigger self-check warnings are gated behind bridge mode.

PLC/TIA-specific validation now only runs when:

```text
IO_Router.sendOutputsToBridge = true
```

## Offline Mode And Local Signals

The user wants the scene controlled by either:

1. MQTT semantic control variables from the real factory/Node-RED.
2. Existing offline/local script signals.

This is supported:

- MQTT `/control` messages directly control object receivers.
- `IO_Router.SimulateInput()` still works for local/offline tag-style control.
- `IO_Router.SetValue()` updates local callbacks even when bridge sending is disabled.
- Old PLC bridge sending is not required.

## Important Assumptions

- Node-RED maps raw PLC/TIA tags into semantic JSON before Unity receives them.
- Unity should not need raw PLC tag names for production control.
- Joint values are degrees.
- Car coordinates are world X/Z.
- Car Y remains controlled by the scene.
- Receiver components hold the last received state.
- Repeated identical MQTT messages must not duplicate payloads or shelf objects.
- `/state` is not motion control.

## Verification Done

Several Unity/Bee/Roslyn compile checks were run using Unity 6000.4.6f1 project response files.

Result:

```text
Compilation completed with exit code 0.
```

The compiler produced pre-existing warnings from the project and Unity analyzer-host warnings when run outside Unity's normal host, but no C# errors from the MQTT receiver/control work or warning cleanup.

Unity editor log also showed a successful script compile after adding the receiver folder.

## Files Most Likely Needed For Further Work

MQTT routing and DTOs:

```text
Assets/Scripts/DigitalTwinMQTT/MqttTwinClient.cs
Assets/Scripts/DigitalTwinMQTT/MqttTwinMessageRouter.cs
Assets/Scripts/DigitalTwinMQTT/MqttTopicParser.cs
Assets/Scripts/DigitalTwinMQTT/TwinControlRouter.cs
Assets/Scripts/DigitalTwinMQTT/TwinJsonPayloads.cs
Assets/Scripts/DigitalTwinMQTT/TwinObjectRegistry.cs
```

Control receivers:

```text
Assets/Scripts/DigitalTwinControl/ArmControlReceiver.cs
Assets/Scripts/DigitalTwinControl/CarControlReceiver.cs
Assets/Scripts/DigitalTwinControl/ConveyorControlReceiver.cs
Assets/Scripts/DigitalTwinControl/WarehouseControlReceiver.cs
```

Legacy/local logic reused:

```text
Assets/NewScript/RobotArmController.cs
Assets/NewScript/RobotCar1.cs
Assets/NewScript/RobotCar2.cs
Assets/NewScript/ConveyorMotor.cs
Assets/NewScript/WarehouseManager.cs
Assets/NewScript/IO_Router.cs
Assets/NewScript/TagSubscriptionHelper.cs
Assets/NewScript/UnityBridgeClient.cs
```

Docs:

```text
Docs/PLC_MQTT_FEEDBACK.md
Docs/CHAT_HANDOFF_SUMMARY.md
```

## Recommended Next Steps

1. Clear the Unity Console so old warnings do not confuse new testing.
2. Ensure `IO_Router.sendOutputsToBridge=false`.
3. Ensure `UnityBridgeClient.enableTcpBridge=false`.
4. Ensure `MqttTwinClient` connects to the correct broker host/port.
5. Confirm `TwinObjectRegistry` has object IDs matching MQTT topic object IDs.
6. Assign receiver Inspector references for payload/finger/shelf visuals.
7. Publish one test MQTT `/control` message for each object type.
8. Verify the scene mirrors the latest MQTT state and does not run a local sequence.
