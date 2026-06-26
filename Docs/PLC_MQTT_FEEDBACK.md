# MQTT Control For Unity Twin

Unity Twin mirrors the external factory/PLC state. It must not decide the manufacturing sequence locally.

Node-RED or the factory bridge should map raw PLC/TIA tags into semantic MQTT JSON before publishing to Unity.

## Topics

Scene control uses only:

```text
factory/cell1/twin/{objectId}/control
```

Examples:

```text
factory/cell1/twin/arm1/control
factory/cell1/twin/car1/control
factory/cell1/twin/conveyor1/control
factory/cell1/twin/warehouse/control
```

`/state` topics are for dashboard/monitoring only and should not drive scene motion.
`/status` topics may also be subscribed by the MQTT manager for dashboard/status data, but they do not drive scene motion.

## Runtime Path

1. `MqttTwinClient` subscribes to `factory/cell1/twin/+/control` for scene control and `factory/cell1/twin/+/status` for status/dashboard data.
2. MQTT callbacks enqueue messages into a thread-safe queue.
3. `MqttTwinClient.Update()` processes queued messages on Unity's main thread.
4. `MqttTwinMessageRouter` forwards `/control` messages to `TwinControlRouter`.
5. `TwinControlRouter` routes by `objectId` through `TwinObjectRegistry`.
6. Control receiver components apply the latest target state and hold it until a newer MQTT message arrives.

## Scripts

Existing/updated:

```text
Assets/Scripts/DigitalTwinMQTT/MqttTwinClient.cs
Assets/Scripts/DigitalTwinMQTT/MqttTwinMessageRouter.cs
Assets/Scripts/DigitalTwinMQTT/MqttTopicParser.cs
Assets/Scripts/DigitalTwinMQTT/TwinControlRouter.cs
Assets/Scripts/DigitalTwinMQTT/TwinJsonPayloads.cs
Assets/Scripts/DigitalTwinMQTT/TwinObjectRegistry.cs
```

New receivers:

```text
Assets/Scripts/DigitalTwinControl/ArmControlReceiver.cs
Assets/Scripts/DigitalTwinControl/CarControlReceiver.cs
Assets/Scripts/DigitalTwinControl/ConveyorControlReceiver.cs
Assets/Scripts/DigitalTwinControl/WarehouseControlReceiver.cs
```

The router can auto-add receiver components at runtime when `autoAddReceiverComponents` is enabled. For best control, add the receiver components in the scene and assign the Inspector references explicitly.

## Inspector Assignments

`TwinObjectRegistry` should contain entries for:

```text
arm1, arm2, arm3
car1, car2
conveyor1, conveyor2
warehouse
```

If `warehouse` is not in the registry, `TwinControlRouter` falls back to `WarehouseManager`.

Receiver references to assign when available:

- `ArmControlReceiver`: `RobotArmController`, six joint transforms, left finger transform, right finger transform.
- `CarControlReceiver`: car transform, payload anchor, payload object/prefab, optional Shelving A items, optional Shelving B slots, manufactured object prefab.
- `ConveyorControlReceiver`: `ConveyorMotor`, running tag, object-detected tag.
- `WarehouseControlReceiver`: `WarehouseManager`, or fallback item arrays for Shelving A/B.

## Control JSON

All runtime control messages use:

```json
{
  "objectId": "object_id_here",
  "type": "object_type_here",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {}
}
```

### Arm

```text
Topic: factory/cell1/twin/arm1/control
```

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

The receiver moves each joint smoothly toward the target angle using the axes from `RobotArmController`.

Finger rules:

- `finger1State=true`: left finger `localPosition.x = -0.04`
- `finger1State=false`: left finger `localPosition.x = 0`
- `finger2State=true`: right finger `localPosition.x = 0.04`
- `finger2State=false`: right finger `localPosition.x = 0`

### Car

```text
Topic: factory/cell1/twin/car1/control
```

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

The receiver moves X/Z smoothly and preserves the current Y.

Payload actions are transition based:

- Car1 pickup hides one assigned Shelving A item and shows/attaches one car payload.
- Car2 drop places one manufactured object into the next assigned Shelving B slot.
- Repeated identical MQTT states do not create duplicate payloads.

### Conveyor

```text
Topic: factory/cell1/twin/conveyor1/control
```

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

The receiver sets `ConveyorMotor.speed` and feeds the existing run/object-detected tags through `IO_Router.SimulateInput`.

### Warehouse

```text
Topic: factory/cell1/twin/warehouse/control
```

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

`WarehouseControlReceiver` reuses `WarehouseManager.ApplyExternalWarehouseSnapshot`.

TODO: source naming may use either `RemainingInB` or `DeliveredToB`. The current Unity API uses delivered-to-B semantics, so `remainingInB` is mapped to that value for visual shelf count.

## Assumptions

- Joint units are degrees.
- Car coordinates are world X/Z values.
- Car Y coordinate remains controlled by the scene.
- Arm axes come from the existing `RobotArmController`.
- Finger references must be assigned if finger motion is required.
- Payload/shelf references must be assigned if payload transfer visuals are required.
- No local sequence controller is created; Unity Twin mirrors latest MQTT state only.
