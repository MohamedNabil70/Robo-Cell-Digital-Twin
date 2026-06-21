# realvirtual MCP Usage Guide

## General Workflow

### Always Start with a Screenshot
Before making changes to a scene, **always capture a screenshot first** to understand the current state:
```
screenshot_scene()    # 3D view of the scene
screenshot_editor()   # Full editor window with hierarchy/inspector
```
This helps analyze the problem and verify the current setup before taking action.

## Scene Analysis Best Practices

### Use Specialized Tools First

When analyzing a scene, **always prefer specialized high-level tools** over generic `component_get_all` calls. Each specialized tool returns structured data for its domain in a single call, replacing many individual queries.

| To understand... | Use this tool | NOT this |
|---|---|---|
| LogicStep flows | `logic_step_get_flow()` | `component_get_all` on each step |
| All drives | `drive_list()` | `component_get_all` on each drive |
| All sensors | `sensor_list()` | `component_get_all` on each sensor |
| All signals | `signal_list()` | `component_get_all` on each signal |
| All grips | `grip_list()` | `component_get_all` on each grip |
| All MUs | `m_u_list()` | `component_get_all` on each MU |
| Grip targets | `grip_target_list()` | `component_get_all` on each target |
| Transport surfaces | `transport_surface_list()` | `component_get_all` on each surface |
| Distance between objects | `transform_measure_distance()` | Multiple `scene_get_transform` + manual math |
| Object visual extents | `transform_get_bounds()` | `component_get_all` + manual scale math |
| Surface-to-surface gap | `transform_measure_surface_distance()` | `transform_get_bounds` on both + manual math |

### Recommended Analysis Order

When starting work on a scene or subsystem:

1. **Screenshot first**: `screenshot_scene()` — understand the visual layout
2. **Find objects**: `scene_find()` or `scene_hierarchy()` — discover structure
3. **Specialized lists**: `drive_list()`, `sensor_list()`, `grip_list()` etc. — get domain overview
4. **LogicStep flows**: `logic_step_get_flow()` — understand automation logic
5. **Individual details**: `component_get()` / `component_get_all()` — only for specific components not covered above

**Example**: To understand a pick-and-place cell, use 4-5 calls instead of 30+:
```python
screenshot_scene()                              # Visual overview
scene_hierarchy(root="MyCell", depth=4)         # Structure
drive_list()                                    # All drives with positions and speeds
grip_list()                                     # All grips with pick signals
logic_step_get_flow(name="MyCell/MainCycle")    # Complete automation flow
```

### Measuring Distances

Use `transform_measure_distance()` to check distances between GameObjects instead of fetching multiple transforms and calculating manually. Returns both 3D distance and XZ (horizontal plane) distance in meters and millimeters.

```python
# Check if robot arm can reach a table
transform_measure_distance(objectA="Robot/Base/Axis1", objectB="Table/PlaceTarget")
# Returns: distance_m, distance_mm, distanceXZ_m, distanceXZ_mm, delta, heightDifference_m

# Check arm segment lengths (for reachability analysis)
transform_measure_distance(objectA="Robot/Base/Axis1", objectB="Robot/Base/Axis1/Axis2")         # Link 1
transform_measure_distance(objectA="Robot/Base/Axis1/Axis2", objectB="Robot/Base/Axis1/Axis2/Axis3")  # Link 2
# Max reach = Link1 + Link2. Compare with distance to target.
```

**Common use cases:**
- Robot arm reachability: measure arm segment lengths and compare sum with distance to target
- Sensor placement: verify sensor is within detection range of conveyor/part
- Gripper reach: check if gripper is close enough for GripTarget (compare with `GripRange`)

### Bounding Box Measurements

Use `transform_get_bounds()` to get the world-space axis-aligned bounding box (AABB) of a GameObject based on its Renderers. Use `transform_measure_surface_distance()` to measure the closest distance between bounding box surfaces of two objects.

These tools work with **visual extents** (meshes), not just pivot points — useful for verifying placement, checking collisions, and analyzing spatial relationships.

```python
# Get bounding box of a table (includes all child meshes)
transform_get_bounds(name="Demo2/Table")
# Returns: center, size, min, max (in both m and mm)

# Get bounds of just the parent object (no children)
transform_get_bounds(name="Demo2/Table", includeChildren=false)

# Measure surface-to-surface distance between Part and Table
transform_measure_surface_distance(objectA="Demo2/Part", objectB="Demo2/Table")
# Returns: surfaceDistance_m/mm, gapX/Y/Z, overlapping, touching, penetration (if overlapping)
```

**When to use which tool:**

| Question | Tool |
|---|---|
| How far apart are two pivots? | `transform_measure_distance()` |
| What are the visual extents of an object? | `transform_get_bounds()` |
| Does object A sit on top of object B? | `transform_measure_surface_distance()` |
| Is there a gap between two objects? | `transform_measure_surface_distance()` |
| Are two objects overlapping/penetrating? | `transform_measure_surface_distance()` |

**Common use cases:**
- Placement verification: check if a Part is resting on a Table (Y gap should be ~0)
- Collision checking: detect if objects are overlapping (`overlapping: true`)
- Reachability analysis: measure surface distance from gripper to part
- Layout planning: verify clearances between machines

## Scene Structure Rules

### realvirtual Root GameObject
The top-level `realvirtual` GameObject (the one with the `realvirtualController` component) must NEVER be modified directly. Do not add children, components, or change properties on it.

When creating new GameObjects, always create them as **siblings** (at the same hierarchy level) of the `realvirtual` GameObject, not inside it. The `realvirtual` GameObject is a managed prefab and its internal structure must not be altered.

**Correct:**
```
Scene Root
  ├── realvirtual          ← DO NOT TOUCH
  ├── MyNewObject          ← Create new objects here (parallel)
  └── AnotherObject        ← Same level as realvirtual
```

**Wrong:**
```
Scene Root
  └── realvirtual
        └── MyNewObject    ← NEVER add inside realvirtual
```

### GameObject Identification
Always use full hierarchy paths when referencing GameObjects (e.g., `Robot/Axis1/Motor` instead of just `Motor`). Use `scene_find` to discover the correct path before operating on objects.

## Simulation Workflow

- Always stop the simulation (`sim_stop`) before recompiling scripts
- Sequence: `sim_stop` -> `editor_recompile` -> `editor_wait_ready` -> `sim_play`
- `component_set` does not work reliably during play mode

## LogicStep Rules

LogicSteps must be organized using containers. Follow these rules strictly:

### One LogicStep Per GameObject
Never place multiple LogicStep components on the same GameObject. Each LogicStep must be on its own dedicated child GameObject.

### Always Use Containers
LogicSteps must always be placed inside a `LogicStep_SerialContainer` or `LogicStep_ParallelContainer`:

- **SerialContainer**: Executes child LogicSteps one after another (in hierarchy order). **Auto-loops**: When all steps complete, it automatically restarts from the first step.
- **ParallelContainer**: Executes all child LogicSteps simultaneously, waits for all to finish before proceeding.

### Nesting Is Supported
Containers can be nested inside other containers:

```
LogicStep_SerialContainer (GameObject)
  ├── LogicStep_Delay (child GameObject)
  ├── LogicStep_WaitForSignalBool (child GameObject)
  ├── LogicStep_ParallelContainer (child GameObject)
  │     ├── LogicStep_DriveToPosition (child GameObject)
  │     └── LogicStep_SetSignalBool (child GameObject)
  └── LogicStep_Enable (child GameObject)
```

### Execution Order
The execution order within a container is determined by the GameObject hierarchy order in the Inspector (top to bottom).

### Naming Convention
Name GameObjects with numbered prefixes for clear execution order:
```
01 Start Entry Conveyor
02 Wait for Part at Sensor
03 Stop Entry Conveyor
04 Wait Part Taken
```

### Available LogicStep Types

| Component | Blocking | Purpose |
|---|---|---|
| `LogicStep_SerialContainer` | Yes | Executes children sequentially, auto-loops |
| `LogicStep_ParallelContainer` | Yes | Executes all children simultaneously |
| `LogicStep_SetSignalBool` | No | Sets a boolean signal value |
| `LogicStep_WaitForSensor` | Yes | Waits for sensor occupied/not-occupied |
| `LogicStep_WaitForSignalBool` | Yes | Waits for a boolean signal to be true/false |
| `LogicStep_Delay` | Yes | Waits for a duration in seconds |
| `LogicStep_DriveToPosition` | Yes | Moves a drive to target position |
| `LogicStep_SetDriveSpeed` | No | Sets drive speed |
| `LogicStep_Enable` | No | Enables/disables a GameObject |
| `LogicStep_Pause` | Yes | Pauses editor for debugging (breakpoint) |

### Reading & Debugging LogicSteps

**ALWAYS use `logic_step_get_flow()` to inspect existing LogicSteps.** This single call returns the complete hierarchy with step types, signal/sensor references, current values, drive positions, active step state, and cycle times. Do NOT use `component_get_all` on individual steps — that wastes many round-trips.

```python
# Get ALL LogicStep flows in the scene (finds top-level containers automatically)
logic_step_get_flow()

# Get flows under a specific parent
logic_step_get_flow(name="MyCell/PickAndPlaceCycle")

# Get a specific container's flow
logic_step_get_flow(name="MyCell/PickAndPlaceCycle/MainCycle")
```

The response includes for each step:
- `type`: Step type (SerialContainer, DriveTo, SetSignalBool, Delay, etc.)
- `stepActive` / `isWaiting`: Whether the step is currently active or waiting
- `params`: Type-specific parameters (drive path + position, signal path + value, duration, etc.)
- `activeStep` / `completedCycles` / `cycleTime`: Container statistics

**During simulation debugging**, call `logic_step_get_flow()` to see which step is currently active (`stepActive: true`) and where the flow is stuck (`isWaiting: true`).

### Configuring LogicSteps via MCP

**Setting Signal references** on LogicStep components uses hierarchy paths as string values:
```python
# Set the Signal property on LogicStep_SetSignalBool
component_set(
    name="MainCycle/01 Start Conveyor",
    componentType="LogicStep_SetSignalBool",
    properties='{"Signal": "DemoCell/PLCInterface/--- Conveyor/EntryConveyorStart", "SetToTrue": true}'
)

# Set Sensor reference on LogicStep_WaitForSensor
component_set(
    name="MainCycle/02 Wait Part",
    componentType="LogicStep_WaitForSensor",
    properties='{"Sensor": "DemoCell/Sensors/EntrySensor", "WaitForOccupied": true}'
)

# Set Signal reference on LogicStep_WaitForSignalBool
component_set(
    name="MainCycle/03 Wait Signal",
    componentType="LogicStep_WaitForSignalBool",
    properties='{"Signal": "DemoCell/PLCInterface/RobotIsLoading", "WaitForTrue": true}'
)
```

**Important**: Signal and Sensor references are Unity Object references. Pass the **hierarchy path as a string** — the MCP server resolves it to the actual component.

### Architecture Pattern: Independent Parallel Processes

For complex automation cells, use **multiple independent SerialContainers** under a **plain GameObject** (no LogicStep component on the parent). Each SerialContainer auto-loops independently. Synchronization happens through signals and sensors.

**IMPORTANT**: Do NOT use a `LogicStep_ParallelContainer` as the parent. A ParallelContainer waits for ALL children to finish before restarting any of them. For truly independent auto-looping processes, the parent must be a plain GameObject without any LogicStep component.

```
MainCycle (plain GameObject — NO LogicStep component!)
  ├── LogicStep_SerialContainer (Entry Conveyor)    ← auto-loops independently
  │     ├── 01 Start Entry Conveyor         (SetSignalBool)
  │     ├── 02 Wait Part at Entry Sensor    (WaitForSensor: occupied)
  │     ├── 03 Stop Entry Conveyor          (SetSignalBool)
  │     └── 04 Wait Part Taken              (WaitForSensor: not occupied)
  │          ↑ auto-loops back to 01
  │
  ├── LogicStep_SerialContainer (Robot and Machine Cycle)    ← auto-loops independently
  │     ├── 01 Wait Part at Entry Sensor    (WaitForSensor: occupied)
  │     ├── 02 Open Door                    (SetSignalBool)
  │     ├── 03 Start LoadCell               (SetSignalBool)
  │     ├── 04 Wait Loading Started         (WaitForSignalBool: RobotIsLoading=true)
  │     ├── 05 Wait Loading Done            (WaitForSignalBool: RobotIsLoading=false)
  │     ├── 06 Clear LoadCell               (SetSignalBool)
  │     ├── 07 Close Door                   (SetSignalBool)
  │     ├── 08 Start Machining              (SetSignalBool)
  │     ├── 09 Machining Delay              (Delay)
  │     ├── 10 Stop Machining               (SetSignalBool)
  │     ├── 11 Open Door                    (SetSignalBool)
  │     ├── 12 Start UnloadCell             (SetSignalBool)
  │     ├── 13 Wait Unloading Started       (WaitForSignalBool: RobotIsUnloading=true)
  │     ├── 14 Wait Unloading Done          (WaitForSignalBool: RobotIsUnloading=false)
  │     ├── 15 Clear UnloadCell             (SetSignalBool)
  │     └── 16 Close Door                   (SetSignalBool)
  │          ↑ auto-loops back to 01
  │
  └── LogicStep_SerialContainer (Exit Conveyor)    ← auto-loops independently
        ├── 01 Wait Unloading Started       (WaitForSignalBool: RobotIsUnloading=true)
        ├── 02 Wait Unloading Done          (WaitForSignalBool: RobotIsUnloading=false)
        ├── 03 Start Exit Conveyor          (SetSignalBool)
        ├── 04 Wait Part at Exit Sensor     (WaitForSensor: occupied)
        ├── 05 Wait Part Left               (WaitForSensor: not occupied)
        └── 06 Stop Exit Conveyor           (SetSignalBool)
             ↑ auto-loops back to 01
```

Each process runs independently and continuously loops. Synchronization happens naturally through signals and sensors (e.g., Entry Conveyor waits for sensor to clear before delivering next part, Machine Cycle waits for robot signals).

### When to Use ParallelContainer

Use `LogicStep_ParallelContainer` only when you need **multiple actions to happen simultaneously within a single step** of a sequence — for example, closing a door AND starting a machine at the same time. Do NOT use it to group independent processes that should auto-loop separately.

### Building LogicSteps via MCP: Step-by-Step

1. **Create a plain parent GameObject** (NO LogicStep component for independent processes):
   ```python
   game_object_create(name="MainCycle", parent="DemoCell")
   # Do NOT add a LogicStep component to MainCycle — it's just an organizer

   game_object_create(name="Entry Conveyor", parent="DemoCell/MainCycle")
   component_add(name="DemoCell/MainCycle/Entry Conveyor", componentType="LogicStep_SerialContainer")
   ```

2. **Create step GameObjects as children** of their container:
   ```python
   game_object_create(name="01 Start Entry Conveyor", parent="DemoCell/MainCycle/Entry Conveyor")
   component_add(name="..../01 Start Entry Conveyor", componentType="LogicStep_SetSignalBool")
   ```

3. **Configure component properties** (signals, sensors, values):
   ```python
   component_set(name="..../01 Start Entry Conveyor", componentType="LogicStep_SetSignalBool",
       properties='{"Signal": "DemoCell/PLCInterface/.../EntryConveyorStart", "SetToTrue": true}')
   ```

4. **Test**: `sim_play()`, then use `logic_step_get_flow()` to monitor progress — it shows `activeStep`, `stepActive`, `isWaiting`, and `completedCycles` for all containers at once.

### Important: Execution Order Is Hierarchy Order

The execution order of steps within a container is determined by **child index** (hierarchy order in Unity), NOT by the GameObject name. Renaming a step does not change its execution position. To reorder steps, use `transform_set_sibling_index`:

```python
# Move "03 Start Exit Conveyor" to be the first child (index 0)
transform_set_sibling_index(name="MainCycle/Exit Conveyor/03 Start Exit Conveyor", index=0)

# Move to last position
transform_set_sibling_index(name="MainCycle/Exit Conveyor/01 Wait Part", index=-1)
```

### Signal Pre-Setting for Reliable Operation

Some signals should be pre-set in **edit mode** (before pressing Play) to ensure reliable operation:

- **Entry conveyor start signal**: Pre-set to `true` so the conveyor starts running immediately
- **Trigger signals** (e.g., LoadCell, UnloadCell): Pre-set to `false` to ensure clean rising edge detection

```python
# Pre-set signals in edit mode (before sim_play)
component_set(name=".../EntryConveyorStart", componentType="PLCOutputBool",
    properties='{"Status": {"Value": true}}')
component_set(name=".../LoadCell", componentType="PLCOutputBool",
    properties='{"Status": {"Value": false}}')
```

### Working with DrivesRecorder / ReplayRecording

When a robot uses `DrivesRecorder` with `ReplayRecording` components (pre-recorded motion sequences), integrate with LogicSteps using the replay status signals:

- **`ReplayRecording.StartOnSignal`**: A `PLCOutputBool` — set to `true` to trigger the replay (rising edge only)
- **`ReplayRecording.IsReplayingSignal`**: A `PLCInputBool` — becomes `true` while replaying, `false` when done

**Key rules:**
- Set `DrivesRecorder.PlayOnStart = false` to prevent timing conflicts with LogicStep-triggered replays
- `ReplayRecording` triggers on **positive flank only** (`StartOnSignal` goes from false → true)
- After triggering, **clear the trigger signal** (set back to false) so the next cycle has a clean flank
- DrivesRecorder replays only reproduce **drive positions** — digital output signals (e.g., robot position signals) do NOT fire during replay
- Use `RobotIsLoading` / `RobotIsUnloading` status signals from `ReplayRecording.IsReplayingSignal` instead of robot position signals

**Pattern for triggering and waiting on a replay:**
```python
# Step 1: Set trigger signal (starts replay on rising edge)
component_set(name="03 Start LoadCell", componentType="LogicStep_SetSignalBool",
    properties='{"Signal": ".../LoadCell", "SetToTrue": true}')

# Step 2: Wait for replay to actually start
component_set(name="04 Wait Loading Started", componentType="LogicStep_WaitForSignalBool",
    properties='{"Signal": ".../RobotIsLoading", "WaitForTrue": true}')

# Step 3: Wait for replay to finish
component_set(name="05 Wait Loading Done", componentType="LogicStep_WaitForSignalBool",
    properties='{"Signal": ".../RobotIsLoading", "WaitForTrue": false}')

# Step 4: Clear trigger signal for next cycle
component_set(name="06 Clear LoadCell", componentType="LogicStep_SetSignalBool",
    properties='{"Signal": ".../LoadCell", "SetToTrue": false}')
```
