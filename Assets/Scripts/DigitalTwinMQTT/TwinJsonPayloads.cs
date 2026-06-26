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
    public string batchStatus;
    public long timestamp;
}
