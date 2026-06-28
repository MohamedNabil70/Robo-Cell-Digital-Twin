# Unity Twin Control Overview

The twin is controlled through semantic MQTT `/control` messages.

Unity does not decide the production sequence by itself. The real factory, PLC, TIA/Node-RED, or a test MQTT publisher sends JSON messages. Unity receives those messages and mirrors the values in the scene.

## Main Control Flow

MQTT topic pattern:

```text
factory/cell1/twin/{objectId}/control
```

Examples:

```text
factory/cell1/twin/car1/control
factory/cell1/twin/car2/control
factory/cell1/twin/conveyor1/control
factory/cell1/twin/conveyor2/control
factory/cell1/twin/warehouse/control
factory/cell1/twin/arm1/control
```

Unity routes by the `{objectId}` in the topic. For example:

```text
factory/cell1/twin/car1/control
```

controls `car1`, even if the JSON payload has a different `objectId`.

Recommended payload shape:

```json
{
  "objectId": "car1",
  "type": "car",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "...": "..."
  }
}
```

`/status` and `/state` messages do not move scene objects. They are only for dashboards and monitoring.

## Cars

Control topics:

```text
factory/cell1/twin/car1/control
factory/cell1/twin/car2/control
```

Example:

```json
{
  "objectId": "car1",
  "type": "car",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "currentX": 4.0,
    "currentZ": 6.0,
    "carrying": true,
    "atPickTarget": true,
    "atDropTarget": false,
    "status": "Moving"
  }
}
```

Key values:

- `currentX`: target world X position.
- `currentZ`: target world Z position.
- `carrying`: shows or hides the payload on the car.
- `atPickTarget`: used for pickup transition logic.
- `atDropTarget`: used for drop transition logic.
- `status`: diagnostic/status text.

Behavior:

- The car keeps its current Y position.
- `carrying=true` shows one payload on the car.
- `carrying=false` hides/removes the payload.
- Missing `carrying` is not treated as `false`.
- Movement is controlled by `/control`, not `/status`.

## Conveyors

Control topics:

```text
factory/cell1/twin/conveyor1/control
factory/cell1/twin/conveyor2/control
```

Example:

```json
{
  "objectId": "conveyor1",
  "type": "conveyor",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "objectDetected": true,
    "running": true,
    "currentSpeed": 1.25
  }
}
```

Key values:

- `objectDetected`: shows or hides the object on the conveyor.
- `running`: starts or stops movement of the visible conveyor object.
- `currentSpeed`: target movement speed.

Behavior:

- `conveyor1` shows a cube.
- `conveyor2` shows a turbine.
- `objectDetected=true` shows exactly one object.
- `objectDetected=false` hides/removes the conveyor object.
- `running=true` moves the visible object.
- `running=false` stops the visible object.
- Negative `currentSpeed` is clamped to `0`.
- Conveyor cube scale is copied from scene object `C1`.
- Conveyor2 turbine scale and rotation are copied from `TurbineExample`.

## Warehouse

Control topic:

```text
factory/cell1/twin/warehouse/control
```

Example:

```json
{
  "objectId": "warehouse",
  "type": "warehouse",
  "source": "node_red",
  "timestamp": 1710000000000,
  "control": {
    "remainingInA": 10,
    "remainingInB": 5,
    "batchStatus": "Running"
  }
}
```

Key values:

- `remainingInA`: number of cube/raw objects visible on Shelving A.
- `remainingInB`: number of turbine/finished objects visible on Shelving B.
- `batchStatus`: diagnostic/status text.

Behavior:

- Valid shelf count range is `0` to `25`.
- `remainingInA=0` shows zero Shelving A cubes.
- `remainingInA=25` shows 25 Shelving A cubes.
- `remainingInA>25` is rejected and does not change the shelf.
- `remainingInB=0` shows zero Shelving B turbines.
- `remainingInB=25` shows 25 Shelving B turbines.
- `remainingInB>25` is rejected and does not change the shelf.
- Negative values are clamped to `0`.
- Before the first valid warehouse `/control` message, local batch startup should not initialize or change the warehouse visuals.

## Robot Arms

Control topics:

```text
factory/cell1/twin/arm1/control
factory/cell1/twin/arm2/control
factory/cell1/twin/arm3/control
```

Example:

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
    "status": "Picking"
  }
}
```

Key values:

- `currentJ1` through `currentJ6`: target joint angles in degrees.
- `finger1State`: left finger state.
- `finger2State`: right finger state.
- `status`: diagnostic/status text.

Behavior:

- Existing smooth joint movement is preserved.
- Joint values are treated as degrees.
- Finger states control the gripper visuals.

## Key Rule

Only this topic family controls scene motion and visuals:

```text
factory/cell1/twin/{objectId}/control
```

The important values are inside:

```json
"control": {}
```

Practical summary:

- Cars: `currentX`, `currentZ`, `carrying`
- Conveyors: `objectDetected`, `running`, `currentSpeed`
- Warehouse: `remainingInA`, `remainingInB`
- Arms: `currentJ1` through `currentJ6`, `finger1State`, `finger2State`
