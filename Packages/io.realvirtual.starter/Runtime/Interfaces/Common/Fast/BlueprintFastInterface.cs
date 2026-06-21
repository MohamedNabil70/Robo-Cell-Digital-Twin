// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using NaughtyAttributes;

namespace realvirtual
{
    //! Blueprint/example interface demonstrating signal handling without external communication
    //! 
    //! This interface serves as a template for creating custom interfaces. It demonstrates:
    //! - Signal discovery and metadata handling
    //! - Thread-safe communication patterns
    //! - Proper initialization and cleanup
    //! - Value simulation for testing
    //!
    //! To create your own interface:
    //! 1. Copy this file and rename the class
    //! 2. Replace the simulated connection in EstablishConnection() with your real connection logic
    //! 3. Implement your protocol in ProcessCommunication()
    //! 4. Add any cleanup code in CloseConnection()
    //!
    //! Key helper methods available from FastInterface base class:
    //! - this.GetAllSignals() - Get all child signals efficiently
    //! - this.GetSignalComponent("name") - Find a specific signal by name with O(1) lookup
    //! - GetSignalValue<T>("name") - Get signal value directly with fast access
    //! - SetSignalValue<T>("name", value) - Set signal value directly with fast access
    //! Note: Additional extension methods may be available from FastSignalManagerHelper for
    //! specialized operations like signal creation and batch operations
    //!
    //! Thread-safe data exchange (use in ProcessCommunication):
    //! - GetInputsForPLC() - Read all input values safely from background thread
    //! - SetOutputsFromPLC() - Write output values safely from background thread
    //!
    //! Signal metadata methods:
    //! - signal.SetMetadata("key", value) - Store custom data with signals
    //! - signal.GetMetadata<T>("key") - Retrieve metadata
    //! - signal.HasMetadata("key") - Check if metadata exists
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/custom-interfaces")]
    public class BlueprintFastInterface : FastInterfaceBase
    {
        [Header("Data Exchange Settings")]
        [Tooltip("Log signal values every N cycles (only when DebugMode is enabled)")]
        public int LogIntervalCycles = 100; //!< Log signal values every N cycles when DebugMode is enabled
        public bool SimulateValueChanges = true; //!< Simulate changing values for output signals
        public float ValueChangeSpeed = 1.0f; //!< Speed of simulated value changes
        
        [Header("Status")]
        [ReadOnly] public int TotalSignalsFound = 0; //!< Total number of signals found
        [ReadOnly] public int InputSignalCount = 0; //!< Number of input signals
        [ReadOnly] public int OutputSignalCount = 0; //!< Number of output signals
        [ReadOnly] public int DataExchangeCycles = 0; //!< Number of communication cycles completed
        [ReadOnly] public float SimulationTime = 0f; //!< Total simulation time in seconds
        
        // Private fields
        private List<Signal> allSignals = new List<Signal>();
        private DateTime simulationStartTime;
        private Dictionary<string, float> signalPhase = new Dictionary<string, float>();
        
        #region Demo Methods for Signal Import
        
        //! Demo method showing how to use CreateOrUpdateSignal for dynamic signal import
        //! This demonstrates the standard pattern for importing signals from external sources
        //! 
        //! Signal lookup logic:
        //! 1. First checks if a signal exists with Signal.Name property matching the import name
        //! 2. If Name is empty/null, checks if GameObject.name matches the import name
        //! 3. If found, updates the existing signal (keeping GameObject name unchanged)
        //! 4. If not found, creates new signal with both GameObject.name and Signal.Name set to import name
        [Button("Demo Import Signals")]
        private void DemoImportSignals()
        {
            Debug.Log("[Demo] Starting signal import demonstration...");
            
            // Example 1: Create a new signal with metadata
            // If no existing signal found by Name property or GameObject name, creates new
            var metadata1 = new Dictionary<string, object> 
            { 
                ["Address"] = "DB100.DBX0.0",
                ["DataType"] = "BOOL",
                ["Source"] = "PLC" 
            };
            var signal1 = this.CreateOrUpdateSignal("Motor1_Running", SignalType.Bool, SignalDirection.Output, metadata1);
            Debug.Log($"Created/Updated: {signal1.name} (GameObject: {signal1.gameObject.name})");
            
            // Example 2: Update existing signal's metadata (same direction)
            var metadata2 = new Dictionary<string, object> 
            { 
                ["Address"] = "DB100.DBX0.1",  // Changed address
                ["DataType"] = "BOOL",
                ["Source"] = "PLC",
                ["Updated"] = DateTime.Now.ToString()
            };
            var signal2 = this.CreateOrUpdateSignal("Motor1_Running", SignalType.Bool, SignalDirection.Output, metadata2);
            Debug.Log($"Updated metadata: {signal2.name}");
            
            // Example 3: Change signal direction (Input to Output)
            // This will replace the signal component but keep the GameObject
            var metadata3 = new Dictionary<string, object> 
            { 
                ["Address"] = "DB100.DBW2",
                ["DataType"] = "INT",
                ["Source"] = "PLC" 
            };
            var signal3 = this.CreateOrUpdateSignal("Motor1_Speed", SignalType.Int, SignalDirection.Input, metadata3);
            Debug.Log($"Created/Updated: {signal3.name} as Input");
            
            // Now change it to Output - component will be replaced
            var signal3Updated = this.CreateOrUpdateSignal("Motor1_Speed", SignalType.Int, SignalDirection.Output, metadata3);
            Debug.Log($"Direction changed: {signal3Updated.name} now as Output");
            
            // Refresh signal manager to ensure all changes are reflected
            this.RefreshSignalManager();
            
            Debug.Log("[Demo] Signal import demonstration completed");
        }
        
        #endregion
        
        //! Called when interface starts - performs initialization on main thread
        //! This method is called from OpenInterface() BEFORE the background communication thread starts.
        //! The signal manager is already initialized by the base class before this method is called.
        //! Use this to:
        //! - Discover and configure signals
        //! - Set up any data structures needed for communication
        //! - Add metadata to signals
        //! - Initialize hardware or libraries (main thread only operations)
        //! 
        //! IMPORTANT: This runs on the Unity main thread, so you can safely access GameObjects
        //! IMPORTANT: Signal manager is pre-initialized - you can immediately use all helper methods
        //! 
        //! Available methods from FastInterface base class:
        //! - this.GetAllSignals() - Get all child signals as IEnumerable<Signal>
        //! - this.GetSignalComponent("signalName") - Find specific signal by name (O(1) lookup)
        //! - GetSignalValue<T>("signalName") - Get signal value directly
        //! - SetSignalValue<T>("signalName", value) - Set signal value directly
        //! 
        //! Available extension methods from FastSignalManagerHelper (for Editor operations):
        //! - this.CreateOrUpdateSignal("name", SignalType.Bool, SignalDirection.Input, metadata)
        //! - signal.SetMetadata("key", value) - Add metadata to existing signals
        //! - signal.GetMetadata<T>("key", defaultValue) - Read signal metadata
        //! - this.GetAllSignals().Where(s => s.HasMetadata("key")) - Find signals by metadata
        //! - this.HasSignal("signalName") - Check if signal exists
        //! - this.GetInputSignals() / this.GetOutputSignals() - Get signals by direction
        //! - this.CountSignals(direction, signalType) - Count signals by criteria
        //! 
        //! NEW: Comprehensive Signal Management with CreateOrUpdateSignal:
        //! - this.CreateOrUpdateSignal("name", SignalType.Bool, SignalDirection.Input, metadata)
        //!   → Creates new signal if doesn't exist
        //!   → Updates metadata if signal exists with same direction
        //!   → Replaces signal component if direction changed (keeps GameObject)
        //! 
        //! Example usage for dynamic signal import:
        //! var metadata = new Dictionary<string, object> { ["Source"] = "PLC", ["Address"] = "DB100.DBX0.0" };
        //! var signal = this.CreateOrUpdateSignal("Motor1_Running", SignalType.Bool, SignalDirection.Output, metadata);
        //! 
        //! Signal direction change handling:
        //! // If Motor1_Running was previously an Input, it will be replaced with Output component
        //! // The GameObject is preserved, only the component changes
        //! signal = this.CreateOrUpdateSignal("Motor1_Running", SignalType.Bool, SignalDirection.Output, metadata, allowDirectionChange: true);
        protected virtual void OnInterfaceStarting()
        {
            Debug.Log($"[BlueprintInterface] Starting interface initialization...");
            
            // Signal manager is already initialized by base class before this method is called
            // Get all signals using FastSignalManager for better performance
            allSignals = this.GetAllSignals().ToList();
            TotalSignalsFound = allSignals.Count;
            InputSignalCount = this.GetInputSignals().Count();
            OutputSignalCount = this.GetOutputSignals().Count();
            
            // Add simple metadata (just a counter) to each signal
            int counter = 0;
            foreach (var signal in allSignals)
            {
                signal.SetMetadata("Index", counter++);
                
                // Initialize random phase for output signals
                if (!signal.IsInput())
                {
                    signalPhase[signal.GetSignalName()] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                }
            }
            
            // Initialize simulation
            simulationStartTime = DateTime.Now;
            DataExchangeCycles = 0;
            
            // Log discovered signals
            Debug.Log($"[BlueprintInterface] Found {TotalSignalsFound} signals ({InputSignalCount} inputs, {OutputSignalCount} outputs)");
            foreach (var signal in allSignals)
            {
                var direction = signal.IsInput() ? "INPUT" : "OUTPUT";
                var index = signal.GetMetadata<int>("Index", -1);
                Debug.Log($"  [{index}] {signal.GetSignalName()} ({signal.GetType().Name}, {direction})");
            }
        }
        
        //! Called when interface stops - performs cleanup on main thread
        //! This method is called from CloseInterface() AFTER the background communication thread has stopped.
        //! Use this to:
        //! - Log final statistics
        //! - Clean up any resources created in OnInterfaceStarting
        //! - Save any persistent data
        //! 
        //! IMPORTANT: This runs on the Unity main thread after communication has fully stopped
        //! 
        //! Available methods:
        //! - this.GetAllSignals() - Access all signals for final state logging
        //! - this.GetSignalValue<T>("signalName") - Read final signal values
        //! - Debug.Log() - Safe to use Unity logging
        protected virtual void OnInterfaceStopping()
        {
            Debug.Log($"[BlueprintInterface] Stopping interface after {DataExchangeCycles} cycles and {SimulationTime:F1} seconds");
            Debug.Log($"  Average cycle rate: {DataExchangeCycles / SimulationTime:F1} Hz");
        }
        
        //! Establishes connection on background thread
        //! This method is called ONCE when starting communication.
        //! Use this to:
        //! - Open network connections (TCP, UDP, WebSocket, etc.)
        //! - Authenticate with external systems
        //! - Perform handshaking protocols
        //! - Initialize communication libraries
        //! 
        //! IMPORTANT: This runs on a background thread - do NOT access Unity GameObjects!
        //! If connection fails, throw an exception. The base class will handle reconnection automatically.
        //! 
        //! Available methods:
        //! - ThreadSafeLogger.LogInfo/LogError/LogWarning() - Thread-safe logging
        //! - Task.Delay(milliseconds) - Async delays
        //! - Standard .NET networking classes (TcpClient, UdpClient, HttpClient, ClientWebSocket)
        //! - throw new Exception("message") - To indicate connection failure
        //! 
        //! Example TCP implementation:
        //! tcpClient = new TcpClient();
        //! await tcpClient.ConnectAsync(ip, port);
        //! stream = tcpClient.GetStream();
        protected override async Task EstablishConnection(CancellationToken cancellationToken)
        {
            ThreadSafeLogger.LogInfo($"Establishing simulated connection...", GetType().Name);
            
            // Simulate connection delay
            await Task.Delay(500, cancellationToken);
            
            ThreadSafeLogger.LogInfo("Simulated connection established", GetType().Name);
        }
        
        //! Main communication processing on background thread
        //! This method is called REPEATEDLY in a loop after connection is established.
        //! Use this to:
        //! - Read data from external system
        //! - Process received data and update output signals
        //! - Read input signals and send to external system
        //! - Handle protocol-specific communication
        //! 
        //! IMPORTANT: This runs on a background thread - do NOT access Unity GameObjects directly!
        //! The base class handles the loop timing based on UpdateCycleMs setting.
        //! This method should complete quickly to maintain consistent cycle times.
        //! 
        //! Available methods for signal data exchange:
        //! - GetInputsForPLC(OnlyTransmitChangedInputs) - Get input signal values as Dictionary<string, object>
        //! - SetOutputsFromPLC(Dictionary<string, object>) - Update output signal values
        //! 
        //! Available helper properties/fields:
        //! - threadSafeDebugMode - Thread-safe copy of DebugMode property
        //! - threadSafeUpdateCycleMs - Thread-safe copy of UpdateCycleMs
        //! - threadSafeOnlyTransmitChangedInputs - Thread-safe copy of OnlyTransmitChangedInputs
        //! - privateCycleCount - Current cycle number (use for statistics)
        //! 
        //! Available methods for logging and communication:
        //! - ThreadSafeLogger.LogInfo/LogError/LogWarning() - Thread-safe logging
        //! - ThreadSafeLogger.LogInfoIf(condition, message, source) - Conditional logging
        //! - Network read/write methods on your connection objects
        //! - Protocol-specific encoding/decoding methods
        //! 
        //! Example TCP pattern:
        //! byte[] buffer = new byte[1024];
        //! int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
        //! await stream.WriteAsync(data, 0, data.Length);
        protected override async Task CommunicationLoop(CancellationToken cancellationToken)
        {
            DataExchangeCycles++;
            SimulationTime = (float)(DateTime.Now - simulationStartTime).TotalSeconds;
            
            // Read all input signals
            var inputs = OnlyTransmitChangedInputs ? GetChangedInputsForPLC() : GetInputsForPLC();
            
            // Log signal values periodically (only when DebugMode is enabled)
            if (threadSafeDebugMode && DataExchangeCycles % LogIntervalCycles == 0)
            {
                ThreadSafeLogger.LogInfo($"=== Cycle {DataExchangeCycles} ===", GetType().Name);

                // Log inputs
                foreach (var kvp in inputs.OrderBy(x => x.Key))
                {
                    ThreadSafeLogger.LogInfo($"  IN:  {kvp.Key} = {kvp.Value}", GetType().Name);
                }

                // Log outputs
                var outputs = allSignals.Where(s => !s.IsInput()).OrderBy(s => s.GetSignalName());
                foreach (var signal in outputs)
                {
                    ThreadSafeLogger.LogInfo($"  OUT: {signal.GetSignalName()} = {signal.GetValue()}", GetType().Name);
                }
            }
            
            // Simulate value changes for outputs
            if (SimulateValueChanges)
            {
                var outputValues = new Dictionary<string, object>();
                var time = SimulationTime * ValueChangeSpeed;
                
                foreach (var signal in allSignals.Where(s => !s.IsInput()))
                {
                    var name = signal.GetSignalName();
                    var phase = signalPhase.GetValueOrDefault(name, 0f);
                    
                    if (signal is PLCOutputBool)
                    {
                        outputValues[name] = Mathf.Sin(time + phase) > 0;
                    }
                    else if (signal is PLCOutputInt)
                    {
                        outputValues[name] = Mathf.RoundToInt(50 + 50 * Mathf.Sin(time + phase));
                    }
                    else if (signal is PLCOutputFloat)
                    {
                        outputValues[name] = Mathf.Sin(time + phase);
                    }
                    else if (signal is PLCOutputText)
                    {
                        var messages = new[] { "Running", "Idle", "Standby", "Active" };
                        outputValues[name] = messages[Mathf.FloorToInt(time / 2) % messages.Length];
                    }
                }
                
                // Apply output values
                if (outputValues.Count > 0)
                {
                    SetOutputsFromPLC(outputValues);
                }
            }
            
            // Simulate processing time
            await Task.Delay(1, cancellationToken);
        }
        
        //! Cleanup when closing connection on background thread
        //! This method is called when stopping communication or before reconnection.
        //! Use this to:
        //! - Close network connections gracefully
        //! - Send disconnect messages to external system
        //! - Dispose of communication resources
        //! - Clean up any background thread resources
        //! 
        //! IMPORTANT: This runs on a background thread - do NOT access Unity GameObjects!
        //! This is called both during normal shutdown and before reconnection attempts.
        //! 
        //! Available methods:
        //! - ThreadSafeLogger.LogInfo/LogError/LogWarning() - Thread-safe logging
        //! - privateCycleCount - Get total cycles completed for logging
        //! - Connection cleanup methods (Close(), Dispose(), Disconnect())
        //! 
        //! Example TCP cleanup:
        //! stream?.Close();
        //! stream?.Dispose();
        //! tcpClient?.Close();
        protected override void CloseConnection()
        {
            ThreadSafeLogger.LogInfo($"Closing simulated connection after {DataExchangeCycles} cycles", GetType().Name);
            // Note: OnInterfaceStopping is called on main thread by base class, not here
        }
        
        protected override void OnCommunicationStarted()
        {
            base.OnCommunicationStarted();
            // OnInterfaceStarting should be called from main thread context
        }
        
        //! Opens the interface - called on main thread
        public override void OpenInterface()
        {
            // Call initialization before starting communication
            OnInterfaceStarting();
            base.OpenInterface();
        }
        
        //! Closes the interface - called on main thread
        public override void CloseInterface()
        {
            base.CloseInterface();
            // Call cleanup after stopping communication
            OnInterfaceStopping();
        }
    }
}