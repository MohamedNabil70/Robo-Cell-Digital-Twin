// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Linq;
using NaughtyAttributes;
using UnityEngine.Serialization;

namespace realvirtual
{
    //! Attribute to mark string properties that represent connection states for custom drawing
    public class ConnectionStateAttribute : PropertyAttribute
    {
    }
    
    //! Attribute to mark string properties that should display error messages with red background
    public class ErrorMessageAttribute : PropertyAttribute
    {
    }
    
}

namespace realvirtual
{
    //! Base class for fast, thread-safe interface communication providing automatic signal management,
    //! connection handling, and high-performance data exchange between Unity and external systems.
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/custom-interfaces")]
    public class FastInterfaceBase : InterfaceBaseClass, IOnInterfaceEnable, IPreFixedUpdate, IPostFixedUpdate
    {
        [BoxGroup("State"), ReadOnly, ConnectionStateAttribute] public string State = "⚫ Disconnected"; //!< Current connection status with visual indicator
        [FormerlySerializedAs("ConnectionState")] [HideInInspector] public InterfaceState state = InterfaceState.Disconnected; //!< Internal connection state enum
        [BoxGroup("State"), ShowIf(nameof(HasError)), ErrorMessageAttribute]
        public new string ErrorMessage = ""; //!< Last error message if connection failed
        [BoxGroup("State"), ReadOnly, ShowIf(nameof(IsReconnecting))] public int ReconnectAttemptCount = 0; //!< Number of reconnection attempts made
        [HideInInspector] public bool IsReconnecting = false; //!< Whether interface is currently attempting to reconnect
        
        [BoxGroup("State"), ReadOnly] public int InputSignals = 0; //!< Total number of input signals found
        [BoxGroup("State"), ReadOnly] public int OutputSignals = 0; //!< Total number of output signals found
        [BoxGroup("State"), ReadOnly] public int CommCycleMs = 0; //!< Actual communication cycle time in milliseconds
        [BoxGroup("State"), ReadOnly] public int CycleCount = 0; //!< Total number of communication cycles completed
        
        [BoxGroup("Configuration")] public int UpdateCycleMs = 10; //!< Communication thread update interval in milliseconds
        [BoxGroup("Configuration")] public bool OnlyTransmitChangedInputs = false; //!< Only send input signals that have changed since last transmission (performance optimization)
        [BoxGroup("Configuration")] public bool AutoReconnect = true; //!< Automatically attempt to reconnect on connection loss
        [BoxGroup("Configuration"), ShowIf(nameof(AutoReconnect))] public float ReconnectIntervalSeconds = 10.0f; //!< Time to wait between reconnection attempts
        [BoxGroup("Configuration"), ShowIf(nameof(AutoReconnect))] public int MaxReconnectAttempts = -1; //!< Maximum reconnection attempts (-1 = unlimited)
        [BoxGroup("Configuration")] public bool DebugMode = false; 
        private CancellationTokenSource cancellationTokenSource;
        private Task communicationTask;
        private bool isRunning = false;
        private DateTime lastCycleStart;
   
        // Reconnection state
        private float lastReconnectTime = 0f;
        private bool shouldAttemptReconnect = false;
        private float errorStateStartTime = 0f;
        private const float ERROR_DISPLAY_DURATION = 2.0f; // Show error for 2 seconds before reconnecting
        private const float RECONNECTING_MIN_DISPLAY_DURATION = 2.0f; // Minimum seconds to display reconnecting state
        private float reconnectingStateStartTime = 0f;
        
        // Main thread initialization flag
        private bool needsSignalManagerInit = false;
        
        // State tracking for main thread logging
        private InterfaceState lastLoggedState = InterfaceState.Disconnected;
        private string lastLoggedError = "";
        private int lastLoggedReconnectAttempt = 0;
        
        // Signal status management flags
        private bool lastSignalConnectionStatus = false;
        
        // Base class notification flags
        private bool needsOnConnectedCall = false;
        private bool needsOnDisconnectedCall = false;
        
        // State synchronization
        private InterfaceState lastStateUpdate = InterfaceState.Disconnected;
        private string lastErrorMessage = "";
        protected string threadSafeErrorMessage = ""; // Thread-safe error message storage
        
        // Thread-safe cycle tracking (protected for derived classes, not MonoBehaviour serialized)
        protected int privateCycleCount = 0;
        protected int privateCommCycleMs = 0;
        
        // Thread-safe signal data - COMPLETELY isolated from Unity GameObjects
        private readonly Dictionary<string, object> threadSafeInputs = new Dictionary<string, object>();
        private readonly Dictionary<string, object> threadSafeOutputs = new Dictionary<string, object>();
        private readonly object signalDataLock = new object();
        
        // Change detection for input signals (for performance optimization)
        private readonly Dictionary<string, object> lastInputValues = new Dictionary<string, object>();
        private readonly Dictionary<string, object> changedInputs = new Dictionary<string, object>();
        
        // Thread-safe property copies - NEVER access MonoBehaviour properties from background threads
        protected bool threadSafeDebugMode;
        protected int threadSafeUpdateCycleMs;

        // Flag to track if interface has been enabled via IOnInterfaceEnable
        private bool interfaceInitialized = false;

        // Static flag: set to true once the first OnInterfaceEnable call arrives from realvirtualController.
        // After this point, any newly created FastInterfaceBase (e.g. via AddComponent at runtime)
        // knows the init phase is complete and self-initializes in OnEnable instead of waiting forever.
        private static bool _initPhaseComplete = false;

        // Cached signal lists for SyncOutputsToUnity/SyncInputsFromUnity (avoid GetComponentsInChildren every tick)
        private Signal[] cachedOutputSignals;
        private Signal[] cachedInputSignals;

        // Manual physics control - static fields shared across all interfaces
        private static bool manualPhysicsEnabled = false;
        private static SimulationMode originalPhysicsSimulationMode = SimulationMode.FixedUpdate;
        private static int manualPhysicsRefCount = 0;
        
        //! Establishes connection to the external system
        protected virtual async Task EstablishConnection(CancellationToken cancellationToken)
        {
            // Override this method in derived classes to establish connection
            // Return when connection is established or throw exception on failure
            await Task.CompletedTask;
        }
        
        //! Main communication loop executed each cycle after connection is established
        protected virtual async Task CommunicationLoop(CancellationToken cancellationToken)
        {
            // Override this method in derived classes for communication logic
            // This is only called when connection is established
            await Task.CompletedTask;
        }
        
        //! Closes the connection to the external system
        protected virtual void CloseConnection()
        {
            // Override this method in derived classes to close connection
        }
        
        //! Handles communication errors and manages reconnection logic
        protected virtual void OnCommunicationError(Exception exception)
        {
            // Set error state and immediately stop running flag
            state = InterfaceState.Error;
            isRunning = false; // Critical: Set this immediately to allow reconnection
            threadSafeErrorMessage = exception.Message; // Thread-safe storage
            errorStateStartTime = (float)DateTime.Now.Subtract(DateTime.Today).TotalSeconds; // Record when error state started
            
            // Thread-safe logging for debugging reconnection
            ThreadSafeLogger.LogErrorIf(threadSafeDebugMode, $"Communication error: {exception.Message}", GetType().Name);
            
            // Signal status update moved to FixedUpdate (main thread)
            // SetAllSignalStatus(false); // Moved - accesses Unity components
            
            // Reset change detection after communication errors to ensure full sync on reconnection
            ResetInputChangeDetection();
            
            // Mark for reconnection if enabled and within attempt limits (but don't transition state yet)
            if (AutoReconnect && (MaxReconnectAttempts < 0 || ReconnectAttemptCount < MaxReconnectAttempts))
            {
                shouldAttemptReconnect = true;
                lastReconnectTime = errorStateStartTime; // Set reconnect time to error start time
                // ConnectionState remains Error - will transition to Reconnecting after delay in FixedUpdate
                IsReconnecting = false; // Will be set to true when state transitions to Reconnecting
                
                ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, $"Error state set, reconnection will be attempted after {ERROR_DISPLAY_DURATION} seconds. AutoReconnect={AutoReconnect}, MaxAttempts={MaxReconnectAttempts}, CurrentAttempts={ReconnectAttemptCount}", GetType().Name);
            }
            else if (AutoReconnect && MaxReconnectAttempts > 0 && ReconnectAttemptCount >= MaxReconnectAttempts)
            {
                // Max reconnection attempts reached - logging moved to FixedUpdate (main thread)
                threadSafeErrorMessage = $"Maximum reconnection attempts ({MaxReconnectAttempts}) reached, giving up";
            }
        }
        
        //! Called when communication thread starts successfully
        protected virtual void OnCommunicationStarted()
        {
            state = InterfaceState.Connected;
            // State = GetConnectionStatusIcon(); // Removed - MonoBehaviour field access from background thread
            threadSafeErrorMessage = ""; // Clear any previous errors
            errorStateStartTime = 0f; // Clear error state timing
            
            // Reset reconnection state on successful connection
            shouldAttemptReconnect = false;
            IsReconnecting = false;
            ReconnectAttemptCount = 0;
            
            // Reset change detection to ensure all current values are transmitted on new connection
            ResetInputChangeDetection();
            
            // Signal manager already initialized on main thread before communication started
            // Background thread can now safely use high-performance signal methods
            
            // NOTE: base.IsConnected will be set to true when OnConnected() is called in FixedUpdate
            // OnConnected() call moved to FixedUpdate (main thread) - may access Unity components
            // OnConnected(); // Moved - potential threading issues
            needsOnConnectedCall = true;
            
            // Connection successful - logging moved to FixedUpdate (main thread)
        }
        
        //! Called when communication thread stops
        protected virtual void OnCommunicationStopped()
        {
            // Only set to Disconnected if we're not in error state or attempting reconnection
            // This preserves the Error state for UI display before transitioning to Reconnecting
            if (state != InterfaceState.Error && !shouldAttemptReconnect)
            {
                state = InterfaceState.Disconnected;
            }
            
            // Only reset reconnection state if we're not trying to reconnect
            // This prevents race condition where OnCommunicationError schedules reconnection
            // but OnCommunicationStopped immediately cancels it
            if (!AutoReconnect || (MaxReconnectAttempts > 0 && ReconnectAttemptCount >= MaxReconnectAttempts))
            {
                shouldAttemptReconnect = false;
                state = InterfaceState.Disconnected; // Only now set to disconnected when giving up
            }
            
            // Don't reset IsReconnecting flag if we're attempting reconnection
            if (!shouldAttemptReconnect)
            {
                IsReconnecting = false;
            }
            
            // Reset main thread initialization flag
            needsSignalManagerInit = false;
            
            // Signal status update moved to FixedUpdate (main thread)
            // SetAllSignalStatus(false); // Moved - accesses Unity components
            
            // Clean up high-performance signal management
            this.ClearSignalManager();
            cachedOutputSignals = null;
            cachedInputSignals = null;
            
            // OnDisconnected() call moved to FixedUpdate (main thread) - may access Unity components
            // OnDisconnected(); // Moved - potential threading issues
            needsOnDisconnectedCall = true;
            
            // Disconnection - logging moved to FixedUpdate (main thread)
        }
        
        //! Opens the interface and starts communication thread
        public override void OpenInterface()
        {
            // Init the signal manager if not already initialized
            if (needsSignalManagerInit)
            {
                this.RefreshSignalManager();
                needsSignalManagerInit = false; // Only initialize once
            }
            // Check if realvirtualController is null
            if (realvirtualController == null)
            {
                Logger.Error($"[{GetType().Name}] OpenInterface failed - realvirtualController is null", this, true);
                return;
            }
            
            if (realvirtualController.DebugMode)
                DebugMode = true;

            // CRITICAL: Prevent interface opening in Edit mode to avoid threading issues
            if (!Application.isPlaying)
            {
                if (DebugMode== true)
                    Logger.Log($"Interface opening blocked - not in Play mode",this,true);
                return;
            }

            if (isRunning)
            {
                if (DebugMode) Logger.Log("Interface already running - skipping duplicate open request", this,true);
                return;
            }

            if (DebugMode) Logger.Log("Opening interface", this,true);
                
            isRunning = true;
            cancellationTokenSource = new CancellationTokenSource();
            
            // CRITICAL: Copy properties to thread-safe variables BEFORE background thread starts
            threadSafeDebugMode = realvirtualController?.DebugMode == true;
            threadSafeUpdateCycleMs = UpdateCycleMs;
            
            // Call derived class property copying (this may override threadSafeDebugMode if derived class has its own DebugMode)
            CopyPropertiesToThreadSafe();
            
            // CRITICAL: Initialize signal manager on main thread BEFORE background communication starts
            try
            {
                this.RefreshSignalManager(); // Must initialize before background thread uses signals
                SetAllSignalStatus(true);

                // Cache signal arrays for SyncOutputsToUnity/SyncInputsFromUnity (avoids GetComponentsInChildren every tick)
                RebuildCachedSignalArrays();

                // Update signal counts immediately after initialization
                UpdateSignalCounts();
                
                // Allow derived classes to prepare Unity data for background thread access
                PrepareForBackgroundThread();
                
                if (threadSafeDebugMode)
                    Logger.Log($"Signal manager pre-initialized for thread safety. Signals found: {InputSignals} inputs, {OutputSignals} outputs",this,true);
            }
            catch (Exception ex)
            {
                Logger.Error($"Signal manager pre-initialization failed: {ex.Message}",this,true);
                CloseInterface(); // Abort if signal manager can't initialize
                return;
            }
            
            if (DebugMode== true)
                Logger.Log($"Starting communication thread",this,true);
            
            communicationTask = Task.Run(async () =>
            {
                try
                {
                    // First establish connection
                    state = InterfaceState.Connecting;
                    ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "EstablishConnection starting...", GetType().Name);
                    await EstablishConnection(cancellationTokenSource.Token);
                    ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "EstablishConnection completed successfully", GetType().Name);
                    
                    // Only start communication loop if connection was successful
                    OnCommunicationStarted();
                    
                    while (!cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        lastCycleStart = DateTime.Now;
                        
                        try
                        {
                            await CommunicationLoop(cancellationTokenSource.Token);
                            privateCycleCount++; // Thread-safe private field
                            
                            var cycleEnd = DateTime.Now;
                            var workTime = (int)(cycleEnd - lastCycleStart).TotalMilliseconds;
                            
                            // Ensure minimum cycle time using high-resolution timer
                            // Stopwatch + Thread.Sleep(1) achieves ~1ms resolution vs Task.Delay's 15-30ms on Windows
                            var remainingTime = threadSafeUpdateCycleMs - workTime;
                            if (remainingTime > 0)
                            {
                                var sw = Stopwatch.StartNew();
                                while (sw.ElapsedMilliseconds < remainingTime && !cancellationTokenSource.IsCancellationRequested)
                                {
                                    if (remainingTime - sw.ElapsedMilliseconds > 2)
                                        Thread.Sleep(1);
                                    else
                                        Thread.Yield();
                                }
                            }
                            
                            // Calculate total cycle time (work + sleep)
                            var totalCycleEnd = DateTime.Now;
                            privateCommCycleMs = (int)(totalCycleEnd - lastCycleStart).TotalMilliseconds;
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            ThreadSafeLogger.LogErrorIf(threadSafeDebugMode, $"CommunicationLoop exception: {ex.Message}", GetType().Name);
                            OnCommunicationError(ex);
                            
                            if (!AutoReconnect)
                            {
                                ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "AutoReconnect disabled - stopping communication", GetType().Name);
                                break;
                            }
                                
                            // Wait before retry - reconnection will be handled by FixedUpdate
                            await Task.Delay(1000, cancellationTokenSource.Token);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Expected when closing - only if we actually requested cancellation
                }
                catch (Exception ex)
                {
                    // Connection establishment failed (including timeout exceptions)
                    OnCommunicationError(ex);
                }
                finally
                {
                    ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "Communication thread stopping", GetType().Name);
                    try
                    {
                        CloseConnection();
                        ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "CloseConnection completed", GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        // Error closing connection - no logging from background thread
                        threadSafeErrorMessage = $"Error closing connection: {ex.Message}";
                        ThreadSafeLogger.LogErrorIf(threadSafeDebugMode, $"CloseConnection error: {ex.Message}", GetType().Name);
                    }
                    
                    OnCommunicationStopped();
                    
                    // Always reset isRunning when communication task completes
                    // This allows reconnection logic to trigger properly
                    isRunning = false;
                    ThreadSafeLogger.LogInfoIf(threadSafeDebugMode, "Communication thread stopped", GetType().Name);
                }
            });
        }
        
        //! Closes the interface and stops communication thread
        public override void CloseInterface()
        {
            if (DebugMode == true)
                Logger.Log($"Closing interface",this,true); // Removed name - causes threading issues
                
            if (!isRunning)
                return;
                
            isRunning = false;
            state = InterfaceState.Closing;
            
            // Immediately update visual State field since FixedUpdate may not run (Edit Mode or Play Mode exit)
            State = GetConnectionStatusIcon(InterfaceState.Closing);
            
            // CRITICAL: Also update the base class IsConnected field to ensure it's synchronized
            // The base class has IsConnected as a field, not a property, and external code might be accessing it
            base.IsConnected = false;
            
            // Debug the state of both IsConnected values
            if (DebugMode == true)
            {
                Logger.Log($"CloseInterface: isRunning={isRunning}, state={state}, " +
                          $"base.IsConnected={base.IsConnected}, this.IsConnected={this.IsConnected}", this, true);
            }
            
            cancellationTokenSource?.Cancel();
            
            try
            {
                communicationTask?.Wait(5000); // Wait up to 5 seconds for clean shutdown
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions during shutdown
            }
            
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            communicationTask = null;
            
            // Reset private cycle tracking
            privateCycleCount = 0;
            privateCommCycleMs = 0;
            
            // Set final disconnected state immediately since FixedUpdate may not run
            state = InterfaceState.Disconnected;
            State = GetConnectionStatusIcon(InterfaceState.Disconnected);
            
            // Ensure base class field is also set to false
            base.IsConnected = false;
            
            // Call OnDisconnected to ensure signals are grayed out (SetAllSignalStatus(false))
            // This is safe to call from main thread (unlike background thread calls)
            OnDisconnected();
            
            // Process any remaining log entries and clear queue for cleanup
            ThreadSafeLogger.ProcessLogQueue();
        }
        
        //! Unity Start - block base class behavior
        protected void Start()
        {
            // Do NOT call base.Start()
            // FastInterfaceBase manages its own initialization
        }
        
        //! Unity OnEnable - opens interface when component is enabled
        protected new void OnEnable()
        {
            // Do NOT call base.OnEnable() - we manage our own initialization

            // Register for FixedUpdate callbacks (safe to call multiple times - contains duplicate check)
            realvirtualController.RegisterPreFixedUpdateHandler(this);
            realvirtualController.RegisterPostFixedUpdateHandler(this);

            // Only open if in play mode and after late initialization
            if (Application.isPlaying)
            {
                if (!interfaceInitialized)
                {
                    if (_initPhaseComplete)
                    {
                        // Init phase already complete → this interface was added dynamically
                        // (e.g. via AddComponent at runtime). Self-initialize instead of waiting.
                        if (realvirtualController?.DebugMode == true)
                            Logger.Log($"OnEnable: late-added interface detected, self-initializing", this, true);
                        OnInterfaceEnable();
                        return;
                    }

                    // Still in init phase → wait for OnInterfaceEnable from realvirtualController
                    if (realvirtualController?.DebugMode == true)
                        Logger.Log($"OnEnable blocked - waiting for OnInterfaceEnable initialization", this, true);
                    return;
                }

                if (realvirtualController?.DebugMode == true)
                    Logger.Log($"OnEnable executing - opening interface", this, true);

                OpenInterface();
            }
        }
        
        protected new void OnDisable()
        {
            // Do NOT call base.OnDisable() - we handle our own cleanup

            // Unregister from FixedUpdate callbacks
            realvirtualController.UnregisterPreFixedUpdateHandler(this);
            realvirtualController.UnregisterPostFixedUpdateHandler(this);

            if (realvirtualController?.DebugMode == true)
            {
                Logger.Log($"OnDisable - closing interface. Current IsConnected values: " +
                          $"base.IsConnected={base.IsConnected}, this.IsConnected={this.IsConnected}", this, true);
            }
            CloseInterface();
            
            // Final verification after close
            if (realvirtualController?.DebugMode == true)
            {
                Logger.Log($"OnDisable complete - Final IsConnected values: " +
                          $"base.IsConnected={base.IsConnected}, this.IsConnected={this.IsConnected}", this, true);
            }
        }
        
        //! Handles application pause events
        protected virtual void OnApplicationPause(bool pauseStatus)
        {
            // Close interface when application is paused (Play mode exit)
            if (pauseStatus)
                CloseInterface();
        }
        
        //! Unity FixedUpdate for main thread operations (signal sync moved to PrePost FixedUpdate)
        protected void  FixedUpdate()
        {
            // Only run in Play mode - Edit mode doesn't need/support threading operations
            if (!Application.isPlaying)
                return;

            // Process thread-safe log queue from background threads (MUST BE FIRST for debugging)
            ThreadSafeLogger.ProcessLogQueue();


            // Handle thread-safe logging on main thread (Unity Debug methods safe here)
            if (realvirtualController?.DebugMode == true)
            {
                HandleMainThreadLogging();
            }

            // Handle signal status updates on main thread
            HandleSignalStatusUpdates();

            // Handle base class notifications on main thread
            HandleBaseClassNotifications();

            // Update State field on main thread (thread-safe)
            UpdateStateDisplay();

            // NOTE: Signal synchronization moved to PrePost FixedUpdate system for precise timing
            
            
            // Handle error state to reconnecting state transition with delay
            if (state == InterfaceState.Error && shouldAttemptReconnect)
            {
                float currentTime = (float)DateTime.Now.Subtract(DateTime.Today).TotalSeconds;
                float timeSinceError = currentTime - errorStateStartTime;
                
                // Show error state for ERROR_DISPLAY_DURATION seconds before transitioning to reconnecting
                if (timeSinceError >= ERROR_DISPLAY_DURATION)
                {
                    state = InterfaceState.Reconnecting;
                    IsReconnecting = true;
                    reconnectingStateStartTime = currentTime; // Track when we started showing reconnecting state
                }
            }
            
            // Handle automatic reconnection using standard realvirtual pattern
            if (shouldAttemptReconnect && state == InterfaceState.Reconnecting)
            {
                float currentTime = (float)DateTime.Now.Subtract(DateTime.Today).TotalSeconds;
                float timeSinceError = currentTime - errorStateStartTime;
                float totalWaitTime = Math.Max(ERROR_DISPLAY_DURATION, ReconnectIntervalSeconds);
             
                if (!isRunning && timeSinceError >= totalWaitTime)
                {
                    if (realvirtualController?.DebugMode == true)
                        Logger.Log($"Initiating reconnection attempt #{ReconnectAttemptCount + 1} (max: {(MaxReconnectAttempts < 0 ? "unlimited" : MaxReconnectAttempts.ToString())})", this, true);
                    
                    ReconnectAttemptCount++;
                    shouldAttemptReconnect = false; // Will be set again if connection fails
                    
                    // Attempt to reconnect
                    OpenInterface();
                }
            }
            
            // Update signal counts periodically and during initialization
            bool shouldUpdateCounts = false;
            
            // Always update counts during initialization (first few seconds)
            if (Time.fixedTime % 1.0f < Time.fixedDeltaTime) // Every second
            {
                shouldUpdateCounts = true;
            }
            // Update more frequently when connected and in first 10 seconds
            else if (isRunning && state == InterfaceState.Connected && CycleCount < 500)
            {
                shouldUpdateCounts = Time.fixedTime % 2.0f < Time.fixedDeltaTime; // Every 2 seconds
            }
            // Update less frequently when stable
            else if (isRunning && state == InterfaceState.Connected)
            {
                shouldUpdateCounts = Time.fixedTime % 10.0f < Time.fixedDeltaTime; // Every 10 seconds
            }
            
            if (shouldUpdateCounts)
            {
                UpdateSignalCounts();
            }
        }
        
        private void HandleMainThreadLogging()
        {
            // Log state changes (thread-safe on main thread)
            if (lastLoggedState != state)
            {
                switch (state)
                {
                    case InterfaceState.Connected:
                        Log($"Interface connected successfully", this);
                        break;
                    case InterfaceState.Disconnected:
                        Log($"Interface disconnected", this);
                        break;
                    case InterfaceState.Error when !string.IsNullOrEmpty(ErrorMessage):
                        Error($"Communication error: {ErrorMessage}", this);
                        break;
                    case InterfaceState.Reconnecting when ReconnectAttemptCount != lastLoggedReconnectAttempt:
                        Log($"Attempting reconnection #{ReconnectAttemptCount} in {ReconnectIntervalSeconds} seconds", this);
                        lastLoggedReconnectAttempt = ReconnectAttemptCount;
                        break;
                }
                lastLoggedState = state;
            }
            
            // Log errors that have changed
            if (!string.IsNullOrEmpty(ErrorMessage) && lastLoggedError != ErrorMessage && state == InterfaceState.Error)
            {
                Error($"Interface error: {ErrorMessage}", this);
                lastLoggedError = ErrorMessage;
            }
        }
        
        private void HandleSignalStatusUpdates()
        {
            // Determine what signal status should be based on connection state
            bool shouldSignalsBeConnected = (state == InterfaceState.Connected);
            
            // Update signal status if it changed
            if (lastSignalConnectionStatus != shouldSignalsBeConnected)
            {
                try
                {
                    SetAllSignalStatus(shouldSignalsBeConnected);
                    lastSignalConnectionStatus = shouldSignalsBeConnected;
                    
                    if (realvirtualController?.DebugMode == true)
                        Log($"Signal status updated: {(shouldSignalsBeConnected ? "Connected" : "Disconnected")}", this);
                }
                catch (Exception ex)
                {
                    Error($"Signal status update error: {ex.Message}", this);
                }
            }
        }
        
        private void HandleBaseClassNotifications()
        {
            // Handle OnConnected notification on main thread
            if (needsOnConnectedCall)
            {
                needsOnConnectedCall = false;
                try
                {
                    OnConnected(); // This sets base.IsConnected = true
                    
                    // Debug to verify both IsConnected values
                    if (realvirtualController?.DebugMode == true)
                    {
                        Log($"OnConnected() called - base.IsConnected={base.IsConnected}, " +
                            $"this.IsConnected={this.IsConnected} (should both be true)", this);
                    }
                }
                catch (Exception ex)
                {
                    Error($"OnConnected() error: {ex.Message}", this);
                }
            }
            
            // Handle OnDisconnected notification on main thread
            if (needsOnDisconnectedCall)
            {
                needsOnDisconnectedCall = false;
                try
                {
                    // Call cleanup for derived classes first
                    CleanupAfterBackgroundThread();
                    
                    // Then call base class OnDisconnected
                    OnDisconnected(); // This sets base.IsConnected = false
                    
                    // Debug to verify both IsConnected values
                    if (realvirtualController?.DebugMode == true)
                    {
                        Log($"OnDisconnected() called - base.IsConnected={base.IsConnected}, " +
                            $"this.IsConnected={this.IsConnected} (should both be false)", this);
                    }
                }
                catch (Exception ex)
                {
                    Error($"OnDisconnected() error: {ex.Message}", this);
                }
            }
        }
        
        private void UpdateStateDisplay()
        {
            // Sync thread-safe error message to main thread
            ErrorMessage = threadSafeErrorMessage;
            
            // Log error messages to console for visibility using Logger
            if (!string.IsNullOrEmpty(ErrorMessage) && lastErrorMessage != ErrorMessage)
            {
                Logger.Error($"ERROR: {ErrorMessage}", this);
            }
            
            // Determine display state with minimum duration for Reconnecting state
            InterfaceState displayState = state;
            
            // If we're in Reconnecting state, ensure minimum display duration
            if (lastStateUpdate == InterfaceState.Reconnecting && state == InterfaceState.Error)
            {
                float currentTime = (float)DateTime.Now.Subtract(DateTime.Today).TotalSeconds;
                float timeSinceReconnecting = currentTime - reconnectingStateStartTime;
                
                // Keep showing Reconnecting state for minimum duration even if error occurred
                if (timeSinceReconnecting < RECONNECTING_MIN_DISPLAY_DURATION)
                {
                    displayState = InterfaceState.Reconnecting;
                }
            }
            
            // Update State field only when display state changes (thread-safe on main thread)
            if (lastStateUpdate != displayState || lastErrorMessage != ErrorMessage)
            {
                State = GetConnectionStatusIcon(displayState);
                lastStateUpdate = displayState;
                lastErrorMessage = ErrorMessage;
            }
            
            // Sync private thread-safe values to public MonoBehaviour fields (main thread only)
            CycleCount = privateCycleCount;
            CommCycleMs = privateCommCycleMs;
            
        }
        
        #region Thread-Safe Signal Methods (Background Thread Safe)
        
        //! Gets all input signal values to send to PLC from background threads
        protected Dictionary<string, object> GetInputsForPLC()
        {
            lock (signalDataLock)
            {
                return new Dictionary<string, object>(threadSafeInputs);
            }
        }
        
        //! Gets only changed input signal values to send to PLC from background threads
        protected Dictionary<string, object> GetChangedInputsForPLC()
        {
            lock (signalDataLock)
            {
                changedInputs.Clear();
                
                foreach (var kvp in threadSafeInputs)
                {
                    string signalName = kvp.Key;
                    object currentValue = kvp.Value;
                    
                    // Check if this signal has changed or is new
                    if (!lastInputValues.TryGetValue(signalName, out var lastValue) || 
                        !AreValuesEqual(currentValue, lastValue))
                    {
                        changedInputs[signalName] = currentValue;
                        lastInputValues[signalName] = CloneValue(currentValue);
                    }
                }
                
                // Remove signals that no longer exist
                var signalsToRemove = lastInputValues.Keys.Where(k => !threadSafeInputs.ContainsKey(k)).ToList();
                foreach (var signalName in signalsToRemove)
                {
                    lastInputValues.Remove(signalName);
                }
                
                return new Dictionary<string, object>(changedInputs);
            }
        }
        
        //! Resets change detection forcing all inputs to be considered changed on next call
        protected void ResetInputChangeDetection()
        {
            lock (signalDataLock)
            {
                lastInputValues.Clear();
            }
        }
        
        //! Sets output signal values from PLC data in background threads
        protected void SetOutputsFromPLC(Dictionary<string, object> outputs)
        {
            lock (signalDataLock)
            {
                foreach (var kvp in outputs)
                {
                    threadSafeOutputs[kvp.Key] = kvp.Value;
                }
            }
        }
        
        //! Sets single output signal value from PLC data in background threads
        protected void SetOutputFromPLC(string signalName, object value)
        {
            lock (signalDataLock)
            {
                threadSafeOutputs[signalName] = value;
            }
        }

        //! Rebuilds the cached signal arrays used by SyncOutputsToUnity/SyncInputsFromUnity.
        //! Call this after dynamically creating or removing signals (e.g., after import).
        //! Must be called on the main thread.
        protected void RebuildCachedSignalArrays()
        {
            var allSignals = GetComponentsInChildren<Signal>();
            cachedOutputSignals = allSignals.Where(s => !s.IsInput()).ToArray();
            cachedInputSignals = allSignals.Where(s => s.IsInput()).ToArray();
        }

        #region PrePost FixedUpdate Interface Implementation

        //! Processes PLC outputs before FixedUpdate - applies data FROM PLC TO Unity objects
        //! IMPLEMENTS IPreFixedUpdate::PreFixedUpdate
        public virtual void PreFixedUpdate()
        {
            if (!isRunning || state != InterfaceState.Connected)
                return;

            try
            {
                // Sync PLC outputs TO Unity objects BEFORE physics calculations
                SyncOutputsToUnity();
            }
            catch (Exception ex)
            {
                if (DebugMode)
                    Logger.Error($"Error in PreFixedUpdate: {ex.Message}", this);
            }
        }

        //! Processes PLC inputs after FixedUpdate - reads data FROM Unity objects TO send to PLC
        //! IMPLEMENTS IPostFixedUpdate::PostFixedUpdate
        public virtual void PostFixedUpdate()
        {
            if (!isRunning || state != InterfaceState.Connected)
                return;

            try
            {
                // Sync PLC inputs FROM Unity objects AFTER physics calculations
                SyncInputsFromUnity();
            }
            catch (Exception ex)
            {
                if (DebugMode)
                    Logger.Error($"Error in PostFixedUpdate: {ex.Message}", this);
            }
        }


        //! Synchronizes PLC outputs FROM thread-safe storage TO Unity Signal GameObjects
        private void SyncOutputsToUnity()
        {
            var signals = cachedOutputSignals; // Local copy for thread safety
            if (signals == null || signals.Length == 0) return;

            lock (signalDataLock)
            {
                if (threadSafeOutputs.Count == 0) return;

                foreach (var signal in signals)
                {
                    if (signal == null) continue; // Signal was destroyed at runtime
                    try
                    {
                        string signalName = signal.GetSignalName();
                        if (threadSafeOutputs.TryGetValue(signalName, out var value) && value != null)
                        {
                            // Type-safe conversions (JSON deserializes int as long, float as double)
                            if (signal is PLCOutputInt)
                                signal.SetValue(Convert.ToInt32(value));
                            else if (signal is PLCOutputFloat)
                                signal.SetValue(Convert.ToSingle(value));
                            else if (signal is PLCOutputBool)
                                signal.SetValue(Convert.ToBoolean(value));
                            else
                                signal.SetValue(value);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (DebugMode)
                            Logger.Error($"Error setting signal '{signal.GetSignalName()}': {ex.Message}", this);
                    }
                }
            }
        }

        //! Synchronizes PLC inputs FROM Unity Signal GameObjects TO thread-safe storage
        private void SyncInputsFromUnity()
        {
            var signals = cachedInputSignals; // Local copy for thread safety
            if (signals == null || signals.Length == 0) return;

            lock (signalDataLock)
            {
                foreach (var signal in signals)
                {
                    if (signal == null) continue; // Signal was destroyed at runtime
                    try
                    {
                        string signalName = signal.GetSignalName();
                        var currentValue = signal.GetValue();

                        // Store current value for transmission to PLC
                        threadSafeInputs[signalName] = currentValue;

                        // Handle change detection for performance optimization
                        if (OnlyTransmitChangedInputs)
                        {
                            if (!lastInputValues.TryGetValue(signalName, out var lastValue) ||
                                !Equals(lastValue, currentValue))
                            {
                                changedInputs[signalName] = currentValue;
                                lastInputValues[signalName] = currentValue;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (DebugMode)
                            Logger.Error($"Error reading signal '{signal.GetSignalName()}': {ex.Message}", this);
                    }
                }
            }
        }

        #endregion

        #region Manual Physics Control

        //! Enables or disables manual physics control mode.
        //! When enabled, disables Physics.autoSimulation and requires manual stepping via StepPhysics().
        //! Interface signal synchronization continues automatically in PreFixedUpdate and PostFixedUpdate.
        //! Uses reference counting to coordinate multiple interfaces - physics is restored only when all interfaces disable manual mode.
        public void SetManualPhysicsMode(bool enabled)
        {
            if (enabled && !manualPhysicsEnabled)
            {
                // Enable manual physics
                manualPhysicsRefCount++;
                if (manualPhysicsRefCount == 1)
                {
                    // First interface to enable - disable auto physics
                    originalPhysicsSimulationMode = Physics.simulationMode;
                    Physics.simulationMode = SimulationMode.Script;
                    manualPhysicsEnabled = true;

                    if (DebugMode)
                        Logger.Message("Manual physics mode ENABLED - Physics.simulationMode set to Script", this);
                }
            }
            else if (!enabled && manualPhysicsEnabled)
            {
                // Disable manual physics
                manualPhysicsRefCount--;
                if (manualPhysicsRefCount <= 0)
                {
                    // Last interface to disable - restore auto physics
                    Physics.simulationMode = originalPhysicsSimulationMode;
                    manualPhysicsEnabled = false;
                    manualPhysicsRefCount = 0;

                    if (DebugMode)
                        Logger.Message("Manual physics mode DISABLED - Physics.simulationMode restored", this);
                }
            }
        }

        //! Steps physics simulation forward by a specified time duration.
        //! Only affects physics simulation - interface signal synchronization continues automatically.
        //! Must be called when manual physics mode is enabled via SetManualPhysicsMode(true).
        public void StepPhysics(float timestep = 0f)
        {
            if (!manualPhysicsEnabled)
            {
                if (DebugMode)
                    Logger.Warning("StepPhysics called but manual physics mode not enabled", this);
                return;
            }

            // Default to fixedDeltaTime if not specified
            if (timestep <= 0f)
                timestep = Time.fixedDeltaTime;

            // Execute physics simulation with specified timestep
            Physics.Simulate(timestep);

            if (DebugMode)
                Logger.Message($"Stepped physics with timestep {timestep:F4}s", this);
        }

        //! Gets whether manual physics mode is currently enabled globally.
        //! Returns true if any FastInterfaceBase instance has enabled manual physics mode.
        public static bool IsManualPhysicsMode()
        {
            return manualPhysicsEnabled;
        }

        #endregion

        //! Legacy method - kept for backward compatibility but signal sync moved to PrePost FixedUpdate
        private void SyncSignalData()
        {
            try
            {
                // Use safe signal access methods - this runs on main thread so Unity API is safe
                // Read from Unity Signal GameObjects to thread-safe inputs using standard realvirtual methods
                var inputSignals = GetComponentsInChildren<Signal>().Where(s => s.IsInput());
                
                // Debug: Log signal synchronization activity
                bool shouldDebugSync = realvirtualController?.DebugMode == true && CycleCount % 200 == 0;
                if (shouldDebugSync)
                    Log($"Signal sync: Found {inputSignals.Count()} input signals", this);
                
                lock (signalDataLock)
                {
                    threadSafeInputs.Clear();
                    int syncedInputs = 0;
                    foreach (var signal in inputSignals)
                    {
                        var signalName = signal.GetSignalName();
                        var value = signal.GetValue();
                        if (value != null)
                        {
                            threadSafeInputs[signalName] = value;
                            syncedInputs++;
                            
                            if (shouldDebugSync && syncedInputs <= 3) // Log first few signals
                                Log($"Synced input: '{signalName}' = '{value}' (Type: {value.GetType().Name})", this);
                        }
                        else if (shouldDebugSync && syncedInputs <= 3)
                        {
                            Log($"Skipped input signal '{signalName}': null value", this);
                        }
                    }
                    
                    if (shouldDebugSync)
                        Log($"Signal sync: Synchronized {syncedInputs}/{inputSignals.Count()} input signals to thread-safe storage", this);
                }
                
                // Write from thread-safe outputs to Unity Signal GameObjects using standard realvirtual methods
                Dictionary<string, object> outputsToWrite;
                lock (signalDataLock)
                {
                    outputsToWrite = new Dictionary<string, object>(threadSafeOutputs);
                }
                
                if (outputsToWrite.Count > 0)
                {
                    var outputSignals = GetComponentsInChildren<Signal>().Where(s => !s.IsInput());
                    int updatedCount = 0;
                    int errorCount = 0;
                    
                    foreach (var signal in outputSignals)
                    {
                        var signalName = signal.GetSignalName();
                        if (outputsToWrite.TryGetValue(signalName, out var value))
                        {
                            try
                            {
                                // Handle type conversions for common mismatches
                                if (signal is PLCOutputInt && value != null)
                                {
                                    // Convert to int for integer signals
                                    signal.SetValue(Convert.ToInt32(value));
                                }
                                else if (signal is PLCOutputFloat && value != null)
                                {
                                    // Convert to float for float signals
                                    signal.SetValue(Convert.ToSingle(value));
                                }
                                else if (signal is PLCOutputBool && value != null)
                                {
                                    // Convert to bool for boolean signals
                                    signal.SetValue(Convert.ToBoolean(value));
                                }
                                else
                                {
                                    // For text signals or exact type matches
                                    signal.SetValue(value);
                                }
                                updatedCount++;
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                if (errorCount <= 3) // Limit error spam
                                {
                                    Error($"Failed to set signal '{signalName}' (type: {signal.GetType().Name}) with value '{value}' (type: {value?.GetType().Name ?? "null"}): {ex.Message}", this);
                                }
                            }
                        }
                    }
                    
                    // Debug: Log sync activity
                    if (realvirtualController?.DebugMode == true && updatedCount > 0 && CycleCount % 100 == 0)
                        Log($"Signal sync: Updated {updatedCount} output signals from {outputsToWrite.Count} thread-safe values" + 
                            (errorCount > 0 ? $" ({errorCount} errors)" : ""), this);
                }
            }
            catch (Exception ex)
            {
                Error($"Signal sync error: {ex.Message}", this);
            }
        }
        
        //! Compares values for change detection
        private bool AreValuesEqual(object value1, object value2)
        {
            if (value1 == null && value2 == null) return true;
            if (value1 == null || value2 == null) return false;
            
            // Handle different numeric types that should be considered equal
            if (IsNumericType(value1) && IsNumericType(value2))
            {
                return Convert.ToDouble(value1).Equals(Convert.ToDouble(value2));
            }
            
            return value1.Equals(value2);
        }
        
        //! Clones values for change detection
        private object CloneValue(object value)
        {
            if (value == null) return null;
            
            // For value types and strings, just return the value (they're immutable)
            if (value.GetType().IsValueType || value is string)
                return value;
            
            // For other types, convert to string as a safe fallback
            return value.ToString();
        }
        
        //! Checks if a type is numeric
        private bool IsNumericType(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
        }
        
        #endregion
        
        #region Backward Compatibility (Deprecated Methods)
        
        //! [DEPRECATED] Use GetInputsForPLC() instead for clearer data flow direction
        [System.Obsolete("Use GetInputsForPLC() instead for clearer data flow direction", false)]
        protected Dictionary<string, object> ReadInputsThreadSafe()
        {
            return GetInputsForPLC();
        }
        
        //! [DEPRECATED] Use GetChangedInputsForPLC() instead for clearer data flow direction
        [System.Obsolete("Use GetChangedInputsForPLC() instead for clearer data flow direction", false)]
        protected Dictionary<string, object> ReadChangedInputsThreadSafe()
        {
            return GetChangedInputsForPLC();
        }
        
        //! [DEPRECATED] Use SetOutputsFromPLC() instead for clearer data flow direction
        [System.Obsolete("Use SetOutputsFromPLC() instead for clearer data flow direction", false)]
        protected void WriteOutputsThreadSafe(Dictionary<string, object> outputs)
        {
            SetOutputsFromPLC(outputs);
        }
        
        //! [DEPRECATED] Use SetOutputFromPLC() instead for clearer data flow direction
        [System.Obsolete("Use SetOutputFromPLC() instead for clearer data flow direction", false)]
        protected void WriteOutputThreadSafe(string signalName, object value)
        {
            SetOutputFromPLC(signalName, value);
        }
        
        #endregion
        
        //! Copies MonoBehaviour properties to thread-safe variables before background thread starts
        protected virtual void CopyPropertiesToThreadSafe()
        {
            // Base class has no additional properties to copy
            // Override in derived classes to copy interface-specific properties
        }
        
        //! Called on main thread to prepare any Unity GameObject data before background thread starts
        //! Override this to cache signal names, GameObject references, or other Unity-specific data
        //! that the background thread will need to access
        protected virtual void PrepareForBackgroundThread()
        {
            // Base implementation does nothing
            // Override in derived classes to cache Unity data for background thread access
            // Example: Cache signal names, output topics, GameObject paths, etc.
        }
        
        //! Called on main thread after background thread stops to clean up Unity-specific resources
        //! Override this to clean up cached data, reset Unity components, or perform other main-thread cleanup
        protected virtual void CleanupAfterBackgroundThread()
        {
            // Base implementation does nothing
            // Override in derived classes to clean up Unity resources after communication stops
            // Example: Clear cached data, reset visual indicators, cleanup temporary GameObjects, etc.
        }
        
        // Override the base class IsConnected field with a property
        // NOTE: The base class has IsConnected as a field, not a property, so we use 'new' to hide it
        // IMPORTANT: External code might still access the base field directly, so we keep them synchronized
        protected new bool IsConnected => isRunning && state == InterfaceState.Connected;
        
        //! Controls when ErrorMessage field is shown in Inspector
        private bool HasError => (state == InterfaceState.Error || state == InterfaceState.Reconnecting) && !string.IsNullOrEmpty(ErrorMessage);
        
        //! Used by [ShowIf] to hide IsConnected field for FastInterface classes - always returns false
        protected override bool ShowIsConnectedField => false;
        
        private string GetConnectionStatusIcon(InterfaceState? state = null)
        {
            var stateToDisplay = state ?? this.state;
            return stateToDisplay switch
            {
                InterfaceState.Disconnected => "⚫ Disconnected",
                InterfaceState.Connecting => "🟡 Connecting...",
                InterfaceState.Connected => "🟢 Connected", 
                InterfaceState.Reconnecting => ReconnectAttemptCount > 0 ? $"🔄 Reconnecting... (#{ReconnectAttemptCount})" : "🔄 Reconnecting...",
                InterfaceState.Error => "🔴 Error",
                InterfaceState.Closing => "⚫ Closing...",
                _ => "❓ Unknown"
            };
        }
        
        //! Updates input and output signal counts
        protected virtual void UpdateSignalCounts()
        {
            try
            {
                // Use simple Unity component methods instead of SignalManagerHelper (main thread safe)
                var allSignals = GetComponentsInChildren<Signal>();
                var inputs = allSignals.Where(s => s.IsInput());
                var outputs = allSignals.Where(s => !s.IsInput());
                
                InputSignals = inputs.Count();
                OutputSignals = outputs.Count();
            }
            catch (System.Exception ex)
            {
                if (realvirtualController?.DebugMode == true)
                    Log($"Error updating signal counts: {ex.Message}", this);
            }
        }
        
        //! Override PostAllScenesLoaded to prevent base class from opening interface too early
        public override void PostAllScenesLoaded()
        {
            // FastInterfaceBase intentionally ignores PostAllScenesLoaded
            // It waits for OnInterfaceEnable instead
        }
        
        #region IOnInterfaceEnable Implementation
        
        //! Called by realvirtualController after all scenes are loaded to enable the interface.
        //! Also called by OnEnable for late-added (dynamically created) interfaces.
        public void OnInterfaceEnable()
        {
            _initPhaseComplete = true;
            interfaceInitialized = true;
            
            // If the GameObject is currently enabled, start the interface now
            if (enabled && gameObject.activeSelf && Application.isPlaying)
            {
                if (realvirtualController?.DebugMode == true)
                    Logger.Log($"FastInterface {name} enabled via OnInterfaceEnable", this, true);
                    
                OpenInterface();
            }
        }
        
        //! Returns true if the interface has been initialized via OnInterfaceEnable
        public bool IsInterfaceReady => interfaceInitialized;

        #endregion
    }
}