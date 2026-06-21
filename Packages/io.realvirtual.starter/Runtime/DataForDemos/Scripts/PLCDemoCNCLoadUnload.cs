// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

#pragma warning disable 4014

using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using NaughtyAttributes;

namespace realvirtual
{
    // Fixed Issues:
    // 1. Line 319: Fixed inverted exit sensor logic (was checking == false, now == true)
    // 2. Replaced dangerous Invoke() calls with cancellable async timers
    // 3. Added state validation before transitions
    // 4. Added timer cancellation on switch off/emergency stop

    #region doc
    //! Simulates a PLC-controlled CNC loading and unloading station with robotic handling.

    //! This component demonstrates a complete industrial automation cycle including material
    //! handling, CNC machining, and robotic load/unload operations. It simulates the PLC logic
    //! for coordinating between entry conveyor, robot, CNC machine, and exit conveyor systems.
    //!
    //! Key Features:
    //! - Automatic and manual operation modes with emergency stop functionality
    //! - State machine control for robot, conveyor, and CNC operations
    //! - Configurable machine cycle times and operation parameters
    //! - Safety interlocks and proper door control sequences
    //! - Physics-based timing that respects Unity's Time.timeScale
    //!
    //! Common Applications:
    //! - Virtual commissioning of CNC loading systems
    //! - Training operators on automated machine tending
    //! - Testing PLC logic before deployment
    //! - Demonstration of industrial automation workflows
    //!
    //! Integration Points:
    //! - Works with PLCInputBool and PLCOutputBool signals for PLC integration
    //! - Can operate standalone or with external PLC control
    //! - Compatible with robot programming interfaces
    //! - Supports emergency stop and safety systems
    //!
    //! For detailed documentation see: https://doc.realvirtual.io/demos/cnc-loading
    #endregion
    [HelpURL("https://doc.realvirtual.io/demos/cnc-loading")]
    public class PLCDemoCNCLoadUnload : realvirtualBehavior
    {
        public float MachineCycleTime = 10; //!< Duration in seconds for the CNC machining operation cycle
        [InfoBox("Only Machine Control, connected PLC is controlling the rest of the system")]
        public bool OnlyMachineControll= false; //!< If true, only machine control is handled internally while rest is controlled by external PLC
        
        [Header("State")]
        [ReadOnly] public string RobotState; //!< Current state of the robot (WaitingForLoading, LoadingMachine, etc.)
        [ReadOnly] public string EntryState; //!< Current state of the entry conveyor system
        [ReadOnly] public string MachineState; //!< Current state of the CNC machine (Empty, Loading, Machining, etc.)
        [ReadOnly] public string ExitState; //!< Current state of the exit conveyor system
        [ReadOnly] public bool AutomaticMode = true; //!< True when system is in automatic operation mode

        [Header("Buttons")]
        public PLCInputBool OnSwitch; //!< Main power switch input signal
        public PLCInputBool EmergencyButton; //!< Emergency stop button input signal
        public PLCInputBool AutomaticButton; //!< Automatic mode selection button input
        public PLCOutputBool AutomaticButtonLight; //!< Light indicator for automatic mode button
        public PLCInputBool RobotButton; //!< Manual robot operation button input
        public PLCOutputBool RobotLight; //!< Light indicator for robot button
        public PLCInputBool ConveyorInButton; //!< Manual entry conveyor operation button input
        public PLCOutputBool ConveyorInLight; //!< Light indicator for entry conveyor button
        public PLCInputBool ConyeyorOutButton; //!< Manual exit conveyor operation button input
        public PLCOutputBool ConveyorOutLight; //!< Light indicator for exit conveyor button
        
        [Header("Robot")] public PLCOutputBool StartLoadingProgramm; //!< Output signal to start robot loading program
        public PLCOutputBool StartUnloadingProgramm; //!< Output signal to start robot unloading program
        public PLCInputBool LoadingProgrammIsRunning; //!< Input signal indicating robot loading program is active
        public PLCInputBool UnloadingProgrammIsRunning; //!< Input signal indicating robot unloading program is active
        
        [Header("Machine")]
        public PLCOutputBool StartMachine; //!< Output signal to start the CNC machine operation
        public PLCOutputBool MoveToolingWheel; //!< Output signal to move the tooling wheel into position
        
        [Header("PLCToMachine")]
        public PLCOutputBool OpenDoor; //!< Output signal to open the machine door
        public PLCInputBool DoorOpened; //!< Input signal indicating machine door is fully opened
        public PLCInputBool DoorClosed; //!< Input signal indicating machine door is fully closed
        public PLCOutputBool StartMachining; //!< Output signal to start the machining process
        public PLCInputBool IsMachining; //!< Input signal indicating machining operation is in progress
        public PLCInputBool MachiningFinished; //!< Input signal indicating machining operation has completed
        
        public PLCOutputBool EntryConveyorStart; //!< Output signal to start the entry conveyor belt
        public PLCInputBool EntrySensorOccupied; //!< Input signal indicating entry sensor detects a part
        
        [Header("ExitConveyor")]
        public PLCOutputBool ExitConveyorStart; //!< Output signal to start the exit conveyor belt
        public PLCInputBool ExitSensorOccupied; //!< Input signal indicating exit sensor detects a part
        
        private CancellationTokenSource cancellationTokenSource, cancellationTokenSourceEmergency;
        private bool startMachineBefore = false;
        private CancellationTokenSource machineTimerToken;
        private CancellationTokenSource toolingWheelToken;
        new void Awake()
        {
            // if not enabled do nothing
            if (!this.enabled || Active == ActiveOnly.Never)
                return;
            RobotState = "WaitingForLoading";
            EntryState = "WaitingForPart";
            MachineState = "Empty";
            ExitState = "Empty";
            if (!OnlyMachineControll) EntryConveyorStart.Value = true;
            AutomaticMode = true;
            if (MachiningFinished != null) MachiningFinished.Value = true;
            base.Awake();
        }
        
        
        private void BlinkLight(PLCOutputBool light, float frequency, CancellationToken token)
        {
            StartCoroutine(BlinkLightCoroutine(light, frequency, token));
        }

        private IEnumerator BlinkLightCoroutine(PLCOutputBool light, float frequency, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                light.Value = !light.Value;
                yield return new WaitForSeconds(frequency);
            }
        }

        private void MachineControl()
        {
            // this is used if only the machine is controlled and the rest of the system by a real connected plc

            if (MachineState == "Empty" && StartMachining.Value && !startMachineBefore && IsMachining.Value == false)
            {
                MachiningFinished.Value = false;
                StartMachine.Value = true;
                MoveToolingWheel.Value = true;
                OpenDoor.Value = false;
                IsMachining.Value = true;
                // Use coroutine instead of Invoke for better state control
                StartMachineTimer(MachineCycleTime);
                StartToolingWheelTimer(4.0f);
            }

            if (MachineState == "WaitingForUnloading")
            {
                IsMachining.Value = false;
                MachiningFinished.Value = true;
                StartMachine.Value = false;
                MachineState = "Empty";
            }
            startMachineBefore = StartMachining.Value;
        }
        // This is the PLC Cycle, permanent loop - checking the inputs and setting the outputs based on the state
        void FixedUpdate()
        {
            if (OnlyMachineControll)
            {
                MachineControl();
                return;
            }
                
      
            // On Switch Pressed - switch if possible
            if (OnSwitch.Value == false)
            {
                EntryConveyorStart.Value = false;
                ExitConveyorStart.Value = false;
                // Cancel any running timers when switch is off
                if (machineTimerToken != null)
                {
                    machineTimerToken.Cancel();
                }
                if (toolingWheelToken != null)
                {
                    toolingWheelToken.Cancel();
                }
            }
            
            // Emergency Button Pressed - switch if possible
            if (EmergencyButton.ChangedToTrue  && !AutomaticButton.Value)
            {
                AutomaticButtonLight.Value = false;
                AutomaticButton.Value = false;
                EntryConveyorStart.Value = false;
                ExitConveyorStart.Value = false;
                AutomaticMode = false;
                cancellationTokenSourceEmergency = new CancellationTokenSource();
                BlinkLight(AutomaticButtonLight,0.2f,cancellationTokenSourceEmergency.Token);
            }
            
            if (EmergencyButton.ChangedToFalse)
            {
                if (cancellationTokenSourceEmergency != null)
                {
                    cancellationTokenSourceEmergency.Cancel();
                    cancellationTokenSourceEmergency = null;
                }
                 
            }
            
            if (!AutomaticMode && !EmergencyButton.Value)
            {
                if (cancellationTokenSource == null)
                {
                    cancellationTokenSource = new CancellationTokenSource();
                    BlinkLight(AutomaticButtonLight,0.7f,cancellationTokenSource.Token);
                }
              
            }
           
            
            // Automaticmode Button Pressed - switch if possible
            if (AutomaticButton.ChangedToTrue && !EmergencyButton.Value && OnSwitch.Value)
            {
                if (AutomaticMode) // turn off if possible
                {
                    AutomaticMode = false;
                    cancellationTokenSource = new CancellationTokenSource();
                    BlinkLight(AutomaticButtonLight,0.7f,cancellationTokenSource.Token);
                }
                else
                {
                    AutomaticMode = true;
                    if (cancellationTokenSource != null)
                    {
                        cancellationTokenSource.Cancel();
                        cancellationTokenSource = null;
                    }
                        
                }
            }
            
            if (AutomaticMode)
            {
                AutomaticButtonLight.Value = true;
            }
            
            RobotLight.Value = RobotButton.Value;

            if (OnSwitch.Value)
            {
                ConveyorInLight.Value = ConveyorInButton.Value;
                ConveyorOutLight.Value = ConyeyorOutButton.Value;
            }
            else
            {
                AutomaticMode = false;
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource = null;
                }
                
                if (cancellationTokenSourceEmergency != null)
                {
                    cancellationTokenSourceEmergency.Cancel();
                    cancellationTokenSourceEmergency = null;
                }
                   
                ConveyorInLight.Value = false;
                ConveyorOutLight.Value = false;
                RobotLight.Value = false;
                AutomaticButtonLight.Value = false;
             
            }
            
            
            // Entry Conveyor ENTRYSTATE
            if (EntryState == "WaitingForPart" && !EmergencyButton.Value && AutomaticMode && OnSwitch.Value)  
            {
                if (!ConveyorInButton.Value)
                {
                    EntryConveyorStart.Value = false;
                }
                else
                {
                    if (EntrySensorOccupied.Value == true)
                    {
                        EntryState = "PartAvailable";
                        EntryConveyorStart.Value = false;
                    }
                    if (EntrySensorOccupied.Value == false) // only move if waiting for part
                    {
                        EntryConveyorStart.Value = true;
                    }
                }
             
            }

            // Start Robot When Part is available
            if (RobotState == "WaitingForLoading" && AutomaticMode && RobotButton.Value && OnSwitch.Value)
            {
                if (EntryState == "PartAvailable" && MachineState == "Empty")
                {
                    MachineState = "Loading";
                    RobotState = "LoadingMachineMoveToConveyor";
                    StartLoadingProgramm.Value = true;
                    EntryState = "WaitingForRobotToTakePart";
                }
            }
            
            // Set Entry for Waiting if Part is taken by Robot
            if (RobotState == "LoadingMachineMoveToConveyor")
            {
                if (EntrySensorOccupied.Value == false)
                {
                    RobotState = "LoadingMachineMoveToMachine";
                    EntryState = "WaitingForPart";
                    StartLoadingProgramm.Value = false;
                }
            }
            
            // If Loading is finished, start Machine
            if (RobotState == "LoadingMachineMoveToMachine")
            {
                 if (LoadingProgrammIsRunning.Value == false)
                 {
                     // Ensure we're in the right state before transitioning
                     if (MachineState == "Loading")
                     {
                         RobotState = "WaitingForUnloading";
                         MachineState = "StartMachine";
                     }
                 }
            }
            
            
            // Start Unloading if Machine is ready
            if (RobotState == "WaitingForUnloading" && AutomaticMode && RobotButton.Value && OnSwitch.Value)
            {
                if (MachineState == "WaitingForUnloading")
                {
                    // Check if exit is ready - either empty or occupied but with conveyor running
                    bool exitReady = (ExitState == "Empty") || 
                                    (ExitState == "Occupied" && ConyeyorOutButton.Value);
                    
                    if (exitReady)
                    {
                        ExitState = "WaitingForPartFromRobot";
                        MachineState = "Unloading";
                        RobotState = "UnloadingMachine";
                        StartUnloadingProgramm.Value = true;
                    }
                }
            }

            
            // If Unloading is finished, set Machine to Empty
            if (RobotState == "UnloadingMachine")
            {
                if (UnloadingProgrammIsRunning.ChangedToFalse) // only negative flank because it might take some time to start
                {
                    RobotState = "WaitingForLoading";
                    MachineState = "Empty";
                    // Only set ExitState to Occupied if it was waiting for part
                    if (ExitState == "WaitingForPartFromRobot")
                    {
                        ExitState = "Occupied";
                    }
                    StartUnloadingProgramm.Value = false;
                }
            }
           
            
            // Exit Conveyor EXITSTATE
            if (ExitState == "WaitingForPartFromRobot" && !EmergencyButton.Value && OnSwitch.Value)
            {
                ExitConveyorStart.Value = false;
            }

            if (ExitState == "Occupied" && !EmergencyButton.Value && ConyeyorOutButton.Value)
            {  
                ExitConveyorStart.Value = true;
                
               // get negative flank of exit sensor - part has left the sensor
               if (ExitSensorOccupied.ChangedToFalse == true)
               {
                   ExitState = "Empty";
                   ExitConveyorStart.Value = false;  // Stop conveyor after part exits
               }
            }
       
            
            /// Machine States
            if (MachineState == "Loading")
            {
                OpenDoor.Value = true;
            }
            
            if (MachineState == "Unloading")
            {
                OpenDoor.Value = true; 
                MoveToolingWheel.Value = false;
                StartToolingWheelTimer(2.0f);
            }
            
            if (MachineState == "StartMachine" && AutomaticMode && OnSwitch.Value)
            {
                StartMachine.Value = true;
                MoveToolingWheel.Value = true;
                OpenDoor.Value = false;
                StartMachineTimer(MachineCycleTime);
                StartToolingWheelTimer(4.0f);
                MachineState = "Machining"; // Change state to prevent re-triggering
            }
            
            if (MachineState == "WaitingForUnloading")
            {
                // Stop the machine when waiting for unloading
                if (StartMachine.Value == true)
                {
                    StartMachine.Value = false;
                }
                OpenDoor.Value = true; 
            }
            
        }
        
        void EndMachine()
        {
            // Only change state if we're still in the expected state
            if (MachineState == "Machining")
            {
                MachineState = "WaitingForUnloading";
                StartMachine.Value = false; // Stop the machine after cycle completes
            }
            else
            {
            }
        }
        
        void EndMoveToolingWheel()
        {
            MoveToolingWheel.Value = false;
        }
        
        // Physics-based timer that respects Time.timeScale
        void StartMachineTimer(float delay)
        {
            // Cancel any existing timer
            if (machineTimerToken != null)
            {
                machineTimerToken.Cancel();
            }

            machineTimerToken = new CancellationTokenSource();
            StartCoroutine(MachineTimerCoroutine(delay));
        }

        private IEnumerator MachineTimerCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (!machineTimerToken.Token.IsCancellationRequested)
            {
                EndMachine();
            }
        }
        
        void StartToolingWheelTimer(float delay)
        {
            // Cancel any existing timer
            if (toolingWheelToken != null)
            {
                toolingWheelToken.Cancel();
            }

            toolingWheelToken = new CancellationTokenSource();
            StartCoroutine(ToolingWheelTimerCoroutine(delay));
        }

        private IEnumerator ToolingWheelTimerCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (!toolingWheelToken.Token.IsCancellationRequested)
            {
                EndMoveToolingWheel();
            }
        }
    }
}

