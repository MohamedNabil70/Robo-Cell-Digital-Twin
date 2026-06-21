// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz


#region

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CRI_Client;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;

#endregion

namespace realvirtual
{
    [AddComponentMenu("realvirtual/Interfaces/igus iRC")]
    public class igusREBELInterface : InterfaceThreadedBaseClass
    {
        public string Adress = "127.0.0.1";
        public int Port = 3921;
        public List<Drive> Drives = new();
        public int NumOutputs;
        public int NumInputs;
        [OnValueChanged("SetSpeed")][Range(0, 100)]public float Speed = 100 ; // as slider

        [ReadOnly] public bool IsMovingToTarget = false;
        [ReadOnly] public RebelTarget Target = null;
        [ReadOnly] public bool KinematicError;
        public bool ActiveCommandOnStart = true;
        public bool DebugMode;
        private HardwareProtocolClient robotClient;
        private readonly ConcurrentQueue<Signal> SignalsChanged = new();
        private readonly List<Signal> OutputSignals = new();
        private volatile bool _moveFinishedPending;
        private int targetmessageindex = 0;
        // a Unity Event for Command is finished - like move to position
        public UnityEvent OnMoveFinished;


        #region RobotCommands
        //< Move to a position
        public void MoveToPosition(RebelTarget target, Vector3 offset, float speed, bool ptp)
        {
            // already moving to target
            if (IsMovingToTarget)
            {
                Logger.Warning("igus iRC Interface - Already moving to target", this);
                return;
            }
            var isopened = false;
            if (!IsConnected)
            {
                OpenInterface();
                isopened = true;
            }

            var pose = CalculateRobotPose(target,offset);

            IsMovingToTarget = true;
            Target = target;
            Target.IsMovingToTarget = true;

            if (!ptp)
                MoveToCartesianPositionCommand(pose.position, speed, pose.rotation.eulerAngles);
            else
                MoveToJointPositionCommand(target.Joints, speed);

            if (isopened) CloseInterface();
        }
        //< Move to a position as async function
        public async Task MoveToPositionAsync(RebelTarget target)
        {
            await MoveToPositionAsync(target, new Vector3(0, 0, 0),target.Speed,target.MotionTypePTP);
        }
        //< Move to a position as async function with offset and motion type
        public async Task MoveToPositionAsync(RebelTarget target, Vector3 offset, float speed, bool ptp)
        {
            // If we are already moving, bail out
            if (IsMovingToTarget)
            {
                Logger.Warning("igus iRC Interface - Already moving to target", this);
                return;
            }

            // Create a TaskCompletionSource to await the OnCommandFinished event
            var tcs = new TaskCompletionSource<bool>();

            // Local handler to set the TaskCompletionSource when the move finishes
            UnityAction onFinishedAction = () =>
            {
                if (tcs.Task.Status == TaskStatus.WaitingForActivation)
                    tcs.SetResult(true);
            };

            // Add listener before we call MoveToPosition
            OnMoveFinished.AddListener(onFinishedAction);

            try
            {
                // Create array with dynamic size based on Drives.Count
                float[] joints = new float[Drives.Count];

                if (ptp)
                    joints = await GetJointAngles(target,offset);
                if (joints != null)
                {
                    target.Joints = joints;
                    MoveToPosition(target, offset, speed, ptp); // Kick off the motion command
                    await tcs.Task;
                }
                else
                {
                    KinematicError = true;
                    if (DebugMode) Logger.Error("igus iRC Interface - Error calculating joint angles", this);
                }
            }
            catch (Exception e)
            {
                Logger.Error("igus iRC Interface - Error moving to target " + e.Message, this);
            }
            finally
            {
                // Always remove the listener, even if there's an error
                OnMoveFinished.RemoveListener(onFinishedAction);
            }
        }

        public void SetSpeed(float Speed)
        {
            this.Speed = Speed;
            SetSpeed();
        }

        //< Sets a digital output on the robot
        public void SetRobotOutput(int output, bool value)
        {
            var val = "false";
            if (value) val = "true";
            var cmd = $"CMD DOUT {output-1} {val}";
            robotClient.SendCommand(cmd);
        }

        //< Sets the speed of the robot
        public void SetSpeed()
        {
            var cmd = $"CMD Override {Speed}";
            robotClient.SendCommand(cmd);
        }
        #endregion

        #region Tools
        private async Task<float[]> GetJointAngles(RebelTarget target, Vector3 offset)
        {
            var pose =CalculateRobotPose(target,offset);
            var rot = pose.rotation.eulerAngles;

            int messageIdIK = SendCommand($"KINEMATIC TranslateToJoint {pose.position.x} {pose.position.y} {pose.position.z} {rot.x} {rot.y} {rot.z}");
            var angles = await CalculateAngles(messageIdIK);
            return angles;

        }

        private Pose CalculateRobotPose(RebelTarget target, Vector3 offset)
        {
            // Get the position and rotation in world space
            var unityPos = target.transform.position;
            var unityRot = target.transform.rotation;

            // Convert position and rotation into the local frame
            unityPos = transform.InverseTransformPoint(unityPos);
            unityPos += offset;
            unityRot = Quaternion.Inverse(transform.rotation) * unityRot;

            // Convert to robot coordinates and apply scaling
            var scaledPos = ConvertToRobotCoordinates(unityPos) * Global.realvirtualcontroller.Scale;
            var robotEuler = ConvertToRobotRotation(unityRot).eulerAngles;

            // Return as a Unity Pose
            return new Pose(scaledPos, Quaternion.Euler(robotEuler));
        }

        public async Task<float[]> CalculateAngles(int messageIdIK)
        {
            var tcs = new TaskCompletionSource<float[]>();
            int numJoints = Drives.Count;

            void OnMessageReceived(string msg)
            {
                var parts = msg.Split(' ');
                if (parts.Length > 5
                    && int.TryParse(parts[1], out int currentId)
                    && currentId == messageIdIK)
                {
                    if (parts[2] == "KINEMATIC" && parts[3] == "Translate" && parts[4] == "Result")
                    {
                        var values = new List<float>();
                        // Generalized: parse based on Drives.Count instead of hardcoded 6
                        for (int i = 11; i < 11 + numJoints && i < parts.Length; i++)
                        {
                            if (float.TryParse(parts[i], out float parsedValue))
                                values.Add(parsedValue);
                        }

                        tcs.TrySetResult(values.ToArray());
                    }
                    else if (parts[2] == "KINEMATIC" && parts[3] == "Translate" && parts[4] == "Error")
                    {
                        var errorMessage = string.Join(" ", parts, 5, parts.Length - 5);
                        tcs.TrySetResult(null);
                    }
                }
            }

            robotClient.OnMessageReceived += OnMessageReceived;
            try
            {
                return await tcs.Task;
            }
            finally
            {
                robotClient.OnMessageReceived -= OnMessageReceived;
            }
        }

        private Vector3 ConvertToRobotCoordinates(Vector3 unityPosition)
        {
            return new Vector3(unityPosition.x, -unityPosition.y, unityPosition.z);
        }

        private Quaternion ConvertToRobotRotation(Quaternion unityRotation)
        {
            return new Quaternion(unityRotation.x, -unityRotation.y, unityRotation.z, -unityRotation.w);
        }

        private void MoveToCartesianPositionCommand(Vector3 position, float speed, Vector3 orientationDeg,
            float velocityMmPerSec = 50f)
        {
            var scale = Global.realvirtualcontroller.Scale;
            var moveCommand = "CMD Move Cart " +
                              $"{position.x } {position.y } {position.z} " + // X Y Z
                              $"{orientationDeg.x} {orientationDeg.y} {orientationDeg.z} " + // A B C
                              "0 0 0 " + // E1 E2 E3 (unused)
                              $"{speed*Speed/100} #base"; // velocity + reference frame

            // This "SendCommand" method will wrap your command with "CMD " prefix plus CRISTART...CRIEND automatically
            targetmessageindex  = SendCommand(moveCommand);
            if (DebugMode) Logger.Message("igus iRC Interface - Move to target command sent " + targetmessageindex, this);
        }

        private void MoveToJointPositionCommand(
            float[] joints,
            float speedPercent = 50f)
        {
            // Generalized: build joint string dynamically based on array length
            string jointStr = string.Join(" ", joints.Select(j => j.ToString())) + " ";
            // Pad to 9 values for CRI protocol (expects 9 joints)
            for (int i = joints.Length; i < 9; i++) jointStr += "0 ";

            var moveCommand =
                "CMD Move Joint " +
                jointStr +                                               // joint angles (dynamic count, padded to 9)
                $"{speedPercent*Speed/100}";                             // velocity (percent, 1..100)

            // Wraps our command with CRISTART...CRIEND automatically
            targetmessageindex = SendCommand(moveCommand);

            if (DebugMode)
            {
                Logger.Message("igus iRC Interface - Move to joint target command sent: " + targetmessageindex, this);
            }
        }


        public void SetActive(bool active)
        {
            var sactive = "false";
            if (active)
                sactive = "true";
            robotClient.SendCommand("CMD SetActive " + sactive);
        }

        public int SendCommand(string command)
        {
            return robotClient.SendCommand(command);
        }
        #endregion

        #region interface
        public override void OpenInterface()
        {
            // Check if at least 1 drive, if not Error Message and quit
            if (!Global.CheckDrives(Drives.ToArray(), "igus iRC Interface", 1))
            {
                return;
            }

            if (Global.realvirtualcontroller.DebugMode) DebugMode = true;
            log.DebugEnabled = DebugMode;
            try
            {
                // Create an instance of the robot interface
                robotClient = new HardwareProtocolClient();
                robotClient.IPAddress = Adress;
                robotClient.Port = Port;
                robotClient.Connect();
                var connected = robotClient.GetConnectionStatus();
                // check if connected
                if (connected)
                {
                    Logger.Message("igus iRC Interface - connected to robot at IP Adress " + Adress, this);
                    OnConnected();
                }
                else
                {
                    Logger.Error("igus iRC Interface - Connection to robot at IP Adress " + Adress + " failed", this);
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Error("igus iRC Interface - Connection to robot at IP Adress " + Adress + " failed", this);
                Logger.Error(e.ToString(), this);
            }

            while (SignalsChanged.TryDequeue(out _)) { }
            OutputSignals.Clear();
            // get all signals - after this call all signals under this gameobject are in the list of InterfaceSignals
            UpdateInterfaceSignals(ref NumInputs, ref NumOutputs);

            var numoutput = 19;
            var numinput = -1;
            // subscribe to all plcinputs to only send when changed
            foreach (var interfaceSignal in InterfaceSignals)
                if (interfaceSignal.Type == InterfaceSignal.TYPE.BOOL) // only bools for rebel available
                {
                    if (interfaceSignal.Direction == InterfaceSignal.DIRECTION.OUTPUT)
                    {
                        numoutput++;
                        if (!int.TryParse(interfaceSignal.SymbolName, out int mempos))
                            mempos = numoutput;
                        interfaceSignal.Mempos = mempos;
                        interfaceSignal.Signal.Comment = "DOut " + interfaceSignal.Mempos;
                        OutputSignals.Add(interfaceSignal.Signal);
                    }
                    else
                    {
                        numinput++;
                        if (!int.TryParse(interfaceSignal.SymbolName, out int mempos))
                            mempos = numinput;
                        interfaceSignal.Mempos = mempos;
                        interfaceSignal.Signal.SignalChanged += SignalOnSignalChanged;
                        interfaceSignal.Signal.Comment = "GSig " + interfaceSignal.Mempos;
                    }

                    interfaceSignal.Signal.interfacesignal = interfaceSignal;
                }

            // subscribe to messages
            robotClient.OnMessageReceived += RobotClientOnOnMessageReceived;

            // Thread starten NACHDEM alle Collections vollstaendig befuellt sind
            base.OpenInterface();

            // Befehle die den laufenden Thread benoetigen
            SetSpeed();
            if (ActiveCommandOnStart) SetActive(true);
        }

        private void RobotClientOnOnMessageReceived(string msg)
        {
            try
            {

                var parts = msg.Split(' ');
                if (parts.Length < 3) return; // Check for the correct start token

                // The start token is followed by the command number and the message type
                var msgType = parts[2];


                // ignore the messages RUNSTATE, STATUS, CYCLESTAT
                if (msgType == "RUNSTATE" || msgType == "STATUS" || msgType == "CYCLESTAT"|| msgType == "VARIABLES" || msgType == "GRIPPERSTATE" || msgType == "OPINFO") return;

                if (DebugMode) Logger.Message("igus iRC Interface - Message received: " + msg, this);

                if (msgType == "EXECEND")
                {
                    if (IsMovingToTarget)
                    {
                        _moveFinishedPending = true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("igus iRC Interface - Error parsing message: " + msg, this);
                Logger.Error(e.ToString(), this);
            }
        }

        private void SignalOnSignalChanged(Signal obj)
        {
            SignalsChanged.Enqueue(obj);
        }

        private void Update()
        {
            if (_moveFinishedPending)
            {
                _moveFinishedPending = false;
                if (DebugMode) Logger.Message("igus iRC Interface - Move to target finished", this);
                IsMovingToTarget = false;
                if (Target != null) Target.IsMovingToTarget = false;
                Target = null;
                OnMoveFinished?.Invoke();
            }
        }

        [Button("Disconnect")]
        public override void CloseInterface()
        {
            if (robotClient != null)
            {
                if (robotClient.GetConnectionStatus())
                    robotClient.SendCommand("QUIT");
                robotClient.OnMessageReceived -= RobotClientOnOnMessageReceived;
                Logger.Message("igus iRC Interface - Disconnecting from robot at IP Adress " + Adress, this);
                robotClient.Disconnect();
            }
            OnDisconnected();
            base.CloseInterface();
        }

        public new void OnEnable()
        {
            IsConnected = false;
            base.OnEnable();
        }

        public void OnDestroy()
        {
            IsConnected = false;
        }

        protected override void CommunicationThreadUpdate()
        {
            if (IsConnected == false)
                return;
            // get joint values - generalized for variable axis count
            int numAxes = Math.Min(Drives.Count, 9); // CRI supports max 9 axes
            for (var i = 0; i < numAxes; i++)
                Drives[i].CurrentPosition = (float)robotClient.posJointsCurrent[i];

            // get Inputs and Outputs
            var outputs = robotClient.digialOutputs;

            foreach (var signal in OutputSignals)
            {
                // number is in signal.Mempos
                var value = (outputs & (1UL << signal.interfacesignal.Mempos)) != 0;
                signal.SetValue(value);
            }


            // loop through all inputs that have changed in SignalsChanged
            while (SignalsChanged.TryDequeue(out var signal))
            {
                var value = ((PLCInputBool)signal).Value ? "true" : "false";
                var command = "CMD GSIG " + signal.interfacesignal.Mempos + " " + value;
                SendCommand(command);
            }
        }
#endregion
    }
}
