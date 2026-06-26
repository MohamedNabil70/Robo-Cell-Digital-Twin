using UnityEngine;

public class ArmControlReceiver : MonoBehaviour
{
    [Header("References")]
    public RobotArmController robotArm;
    public Transform joint1;
    public Transform joint2;
    public Transform joint3;
    public Transform joint4;
    public Transform joint5;
    public Transform joint6;
    public Transform leftFingerTransform;
    public Transform rightFingerTransform;

    [Header("Axes")]
    public RobotArmController.RotAxis j1Axis = RobotArmController.RotAxis.Y;
    public RobotArmController.RotAxis j2Axis = RobotArmController.RotAxis.Z;
    public RobotArmController.RotAxis j3Axis = RobotArmController.RotAxis.Z;
    public RobotArmController.RotAxis j4Axis = RobotArmController.RotAxis.X;
    public RobotArmController.RotAxis j5Axis = RobotArmController.RotAxis.Z;
    public RobotArmController.RotAxis j6Axis = RobotArmController.RotAxis.X;

    [Header("Motion")]
    public float jointDegreesPerSecond = 180f;
    public bool smoothJoints = true;

    [Header("Finger Positions")]
    public float leftFingerClosedX = -0.04f;
    public float rightFingerClosedX = 0.04f;
    public float fingerOpenX = 0f;

    [Header("Diagnostics")]
    [SerializeField] bool hasTarget;
    [SerializeField] string lastStatus = "";
    [SerializeField] bool lastAtPickTarget;
    [SerializeField] bool lastAtDropTarget;
    [SerializeField] bool lastFinger1State;
    [SerializeField] bool lastFinger2State;

    readonly float[] targetAngles = new float[6];

    void Awake()
    {
        ResolveFromRobotArm();
    }

    void Update()
    {
        if (!hasTarget)
        {
            return;
        }

        float maxDelta = smoothJoints ? Mathf.Max(0f, jointDegreesPerSecond) * Time.deltaTime : float.PositiveInfinity;
        MoveJoint(joint1, j1Axis, targetAngles[0], maxDelta);
        MoveJoint(joint2, j2Axis, targetAngles[1], maxDelta);
        MoveJoint(joint3, j3Axis, targetAngles[2], maxDelta);
        MoveJoint(joint4, j4Axis, targetAngles[3], maxDelta);
        MoveJoint(joint5, j5Axis, targetAngles[4], maxDelta);
        MoveJoint(joint6, j6Axis, targetAngles[5], maxDelta);
    }

    public void Configure(RobotArmController controller)
    {
        robotArm = controller;
        ResolveFromRobotArm();
    }

    public void ApplyControl(ArmControlData control)
    {
        if (control == null)
        {
            Debug.LogWarning($"[ArmControlReceiver:{name}] Ignored null arm control.");
            return;
        }

        ResolveFromRobotArm();

        targetAngles[0] = control.currentJ1;
        targetAngles[1] = control.currentJ2;
        targetAngles[2] = control.currentJ3;
        targetAngles[3] = control.currentJ4;
        targetAngles[4] = control.currentJ5;
        targetAngles[5] = control.currentJ6;
        hasTarget = true;

        ApplyFingerState(leftFingerTransform, control.finger1State ? leftFingerClosedX : fingerOpenX);
        ApplyFingerState(rightFingerTransform, control.finger2State ? rightFingerClosedX : fingerOpenX);

        lastFinger1State = control.finger1State;
        lastFinger2State = control.finger2State;
        lastStatus = control.status ?? string.Empty;
        lastAtPickTarget = control.atPickTarget;
        lastAtDropTarget = control.atDropTarget;
    }

    void ResolveFromRobotArm()
    {
        if (robotArm == null)
        {
            robotArm = GetComponent<RobotArmController>();
        }

        if (robotArm == null)
        {
            return;
        }

        if (joint1 == null) joint1 = robotArm.joint1;
        if (joint2 == null) joint2 = robotArm.joint2;
        if (joint3 == null) joint3 = robotArm.joint3;
        if (joint4 == null) joint4 = robotArm.joint4;
        if (joint5 == null) joint5 = robotArm.joint5;
        if (joint6 == null) joint6 = robotArm.joint6;

        j1Axis = robotArm.j1Axis;
        j2Axis = robotArm.j2Axis;
        j3Axis = robotArm.j3Axis;
        j4Axis = robotArm.j4Axis;
        j5Axis = robotArm.j5Axis;
        j6Axis = robotArm.j6Axis;
    }

    static void MoveJoint(Transform joint, RobotArmController.RotAxis axis, float targetAngle, float maxDelta)
    {
        if (joint == null)
        {
            return;
        }

        Vector3 euler = joint.localEulerAngles;
        switch (axis)
        {
            case RobotArmController.RotAxis.X:
                euler.x = Mathf.MoveTowardsAngle(euler.x, targetAngle, maxDelta);
                break;
            case RobotArmController.RotAxis.Y:
                euler.y = Mathf.MoveTowardsAngle(euler.y, targetAngle, maxDelta);
                break;
            default:
                euler.z = Mathf.MoveTowardsAngle(euler.z, targetAngle, maxDelta);
                break;
        }

        joint.localEulerAngles = euler;
    }

    static void ApplyFingerState(Transform finger, float localX)
    {
        if (finger == null)
        {
            return;
        }

        Vector3 position = finger.localPosition;
        position.x = localX;
        finger.localPosition = position;
    }
}
