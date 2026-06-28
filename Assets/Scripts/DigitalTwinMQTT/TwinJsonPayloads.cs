using System;

[Serializable]
public class TwinStatusPayload
{
    public string objectId;
    public string displayName;
    public string state;
    public string status;
    public string health;
    public bool running;
    public float speed;
    public string speedUnit;
    public float temperature;
    public string temperatureUnit;
    public float vibration;
    public string vibrationUnit;
    public string aiStatus;
    public string ai_status;
    public float aiConfidence;
    public string message;
    public string recommendedAction;
    public long timestamp;
    public string timestampUtc;
}

[Serializable]
public class TwinRobotArmControlPayload
{
    public string objectId;
    public float[] joints;
    public bool finger1State;
    public bool finger2State;
    public string status;
    public bool atPickTarget;
    public bool atDropTarget;
    public long timestamp;
}

[Serializable]
public class TwinCarControlPayload
{
    public string objectId;
    public float Car1_Target;
    public float Car2_Target;
    public float currentX;
    public float currentZ;
    public string status;
    public bool atPickTarget;
    public bool atDropTarget;
    public bool carrying;
    public long timestamp;
}

[Serializable]
public class TwinConveyorControlPayload
{
    public string objectId;
    public float currentSpeed;
    public bool running;
    public bool objectDetected;
    public long timestamp;
}

[Serializable]
public class TwinWarehouseControlPayload
{
    public string objectId;
    public int remainingInA;
    public int deliveredToB;
    public int remainingInB;
    public string batchStatus;
    public long timestamp;
}

[Serializable]
public class TwinControlEnvelope
{
    public string objectId;
    public string type;
    public string source;
    public long timestamp;
}

[Serializable]
public class TwinConveyorControlMessage
{
    public string objectId;
    public string type;
    public string source;
    public long timestamp;
    public ConveyorControlData control;
}

[Serializable]
public class TwinCarControlMessage
{
    public string objectId;
    public string type;
    public string source;
    public long timestamp;
    public CarControlData control;
}

[Serializable]
public class TwinArmControlMessage
{
    public string objectId;
    public string type;
    public string source;
    public long timestamp;
    public ArmControlData control;
}

[Serializable]
public class TwinWarehouseControlMessage
{
    public string objectId;
    public string type;
    public string source;
    public long timestamp;
    public WarehouseControlData control;
}

[Serializable]
public class ConveyorControlData
{
    public float currentSpeed;
    public bool running;
    public bool objectDetected;
    [NonSerialized] public bool hasCurrentSpeed;
    [NonSerialized] public bool hasRunning;
    [NonSerialized] public bool hasObjectDetected;
}

[Serializable]
public class CarControlData
{
    public float Car1_Target;
    public float Car2_Target;
    public float targetZ;
    public float currentX;
    public float currentZ;
    public string status;
    public bool atPickTarget;
    public bool atDropTarget;
    public bool carrying;
    [NonSerialized] public bool hasTargetZ;
    [NonSerialized] public bool hasCurrentX;
    [NonSerialized] public bool hasCurrentZ;
    [NonSerialized] public bool hasAtPickTarget;
    [NonSerialized] public bool hasAtDropTarget;
    [NonSerialized] public bool hasCarrying;
}

[Serializable]
public class ArmControlData
{
    public float currentJ1;
    public float currentJ2;
    public float currentJ3;
    public float currentJ4;
    public float currentJ5;
    public float currentJ6;
    public bool finger1State;
    public bool finger2State;
    public string status;
    public bool atPickTarget;
    public bool atDropTarget;
}

[Serializable]
public class WarehouseControlData
{
    public int remainingInA;
    public int remainingInB;
    public int deliveredToB;
    public string batchStatus;
    [NonSerialized] public bool hasRemainingInA;
    [NonSerialized] public bool hasRemainingInB;
    [NonSerialized] public bool hasDeliveredToB;
}
