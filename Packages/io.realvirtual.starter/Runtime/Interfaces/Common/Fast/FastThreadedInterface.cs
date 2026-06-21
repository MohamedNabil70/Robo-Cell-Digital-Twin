// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using System;
using System.Threading;
using System.Threading.Tasks;
using NaughtyAttributes;

namespace realvirtual
{
    //! Base class for threaded interfaces providing communication loop and connection management
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/custom-interfaces")]
    public class FastThreadedInterface : FastInterfaceBase
    {
        
        [Button("Connect", EButtonEnableMode.Playmode), ShowIf(nameof(CanConnect))]
        private void ConnectButton() => OpenInterface();
        
        [Button("Disconnect", EButtonEnableMode.Playmode), ShowIf(nameof(CanDisconnect))]
        private void DisconnectButton() => CloseInterface();
        
        private bool CanConnect => !IsConnected && state != InterfaceState.Connecting;
        private bool CanDisconnect => IsConnected || state == InterfaceState.Connecting;
        
        //! Copies Unity properties to thread-safe variables before background thread starts
        protected override void CopyPropertiesToThreadSafe()
        {
            base.CopyPropertiesToThreadSafe();
            threadSafeDebugMode = DebugMode;
        }
        //! Establishes connection to external system
        protected virtual async Task EstablishConnectionThreaded()
        {
            // Override this method to establish connection
            // This is called before communication starts
            // Throw exception if connection cannot be established
            await Task.CompletedTask;
        }
        
        //! Main communication update method called each cycle
        protected virtual void CommunicationThreadUpdate()
        {
            // Override this method in derived classes - maintains compatibility with existing pattern
            // This method is called in each communication cycle after connection is established
        }
        
        //! Cleanup method called when communication thread stops
        protected virtual void CommunicationThreadClose()
        {
            // Override this method for cleanup when communication thread stops
        }
        
        //! Called when communication starts successfully
        protected override void OnCommunicationStarted()
        {
            base.OnCommunicationStarted();
            
            // Automatically initialize high-performance signal management
            InitializeHighPerformanceMode();
            
            ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, $"Communication started - High-Performance Mode active", GetType().Name);
        }
        
        //! Initializes high-performance signal management for background thread use
        private void InitializeHighPerformanceMode()
        {
            try
            {
                ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "Initializing high-performance signal management...", GetType().Name);
                
                // Signal manager already initialized on main thread before background communication starts
                // Background thread can safely assume high-performance mode is active
                
                ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "High-Performance Mode ready for background thread use", GetType().Name);
            }
            catch (System.Exception ex)
            {
                // Store error for main thread to handle
                threadSafeErrorMessage = $"High-Performance Mode initialization error: {ex.Message}";
                ThreadSafeLogger.LogErrorIf(threadSafeDebugMode, $"High-Performance Mode error: {ex.Message}", GetType().Name);
            }
        }
        
        //! Sealed implementation of connection establishment
        protected sealed override async Task EstablishConnection(CancellationToken cancellationToken)
        {
            ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "Establishing connection...", GetType().Name);
                
            await EstablishConnectionThreaded();
            
            ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "Connection established successfully", GetType().Name);
        }
        
        //! Sealed implementation of communication loop with error handling
        protected sealed override async Task CommunicationLoop(CancellationToken cancellationToken)
        {
            if (threadSafeDebugMode && privateCycleCount == 0)
                ThreadSafeLogger.LogInfo("Communication loop started", GetType().Name);
            else if (threadSafeDebugMode && privateCycleCount % 1000 == 0) // Log every 1000 cycles
                ThreadSafeLogger.LogCycle(ThreadSafeLogger.LogLevel.Info, privateCycleCount, $"CycleTime: {privateCommCycleMs}ms", GetType().Name);
            
            try
            {
                // Wrap the traditional synchronous CommunicationThreadUpdate in async context
                await Task.Run(() => CommunicationThreadUpdate(), cancellationToken);
                
                if (threadSafeDebugMode && privateCycleCount % 500 == 0) // Log every 500 cycles
                {
                    ThreadSafeLogger.LogCycle(ThreadSafeLogger.LogLevel.Info, privateCycleCount, $"Communication running smoothly, CycleTime: {privateCommCycleMs}ms", GetType().Name);
                }
            }
            catch (System.Exception ex) when (!(ex is OperationCanceledException))
            {
                // Store error for main thread logging - no Unity API calls from background thread
                threadSafeErrorMessage = $"CommunicationThreadUpdate error in cycle {privateCycleCount}: {ex.Message}";
                ThreadSafeLogger.LogErrorIf(threadSafeDebugMode, $"Communication error: {ex.Message}", GetType().Name);
                throw; // Re-throw to let parent handle
            }
        }
        
        //! Called when communication stops
        protected override void OnCommunicationStopped()
        {
            try
            {
                ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, $"Communication stopped, calling cleanup after {privateCycleCount} cycles", GetType().Name);
                    
                CommunicationThreadClose();
            }
            catch (System.Exception ex)
            {
                // Store error for main thread logging - no Unity API calls from background thread
                threadSafeErrorMessage = $"Error during communication thread cleanup: {ex.Message}";
                ThreadSafeLogger.LogErrorIf(threadSafeDebugMode, $"Cleanup error: {ex.Message}", GetType().Name);
            }
            
            // Automatically cleanup high-performance signal management
            CleanupHighPerformanceMode();
            
            base.OnCommunicationStopped();
        }
        
        //! Cleans up high-performance signal management
        private void CleanupHighPerformanceMode()
        {
            try
            {
                ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "Cleaning up high-performance signal management...", GetType().Name);
                
                // Signal manager cleanup will be handled by main thread in base class
                
                ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "High-Performance Mode cleanup completed", GetType().Name);
            }
            catch (System.Exception ex)
            {
                // Store error for main thread logging - no Unity API calls from background thread  
                threadSafeErrorMessage = $"High-Performance Mode cleanup error: {ex.Message}";
                ThreadSafeLogger.LogErrorIf(threadSafeDebugMode, $"Cleanup error: {ex.Message}", GetType().Name);
            }
        }
    }
}