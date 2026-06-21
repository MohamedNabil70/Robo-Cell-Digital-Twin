// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace realvirtual
{
    public static class FastSignalManagerHelper
    {
        private static readonly Dictionary<InterfaceBaseClass, FastSignalManager> _managers 
            = new Dictionary<InterfaceBaseClass, FastSignalManager>();
        
        // Track the main thread ID for thread safety checks
        private static int _mainThreadId = -1;
        
        // Static constructor to capture main thread ID
        static FastSignalManagerHelper()
        {
            // This runs when the class is first accessed, which should be on the main thread
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }
        
        #region High-Performance Signal Value Operations
        
        public static T GetSignalValue<T>(this InterfaceBaseClass interfaceBase, string signalName)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].GetValueFast<T>(signalName);
        }
        
        public static bool TryGetSignalValue<T>(this InterfaceBaseClass interfaceBase, string signalName, out T value)
        {
            EnsureInitialized(interfaceBase);
            
            if (_managers[interfaceBase].HasSignal(signalName))
            {
                value = _managers[interfaceBase].GetValueFast<T>(signalName);
                return true;
            }
            
            value = default(T);
            return false;
        }
        
        public static void SetSignalValue<T>(this InterfaceBaseClass interfaceBase, string signalName, T value)
        {
            EnsureInitialized(interfaceBase);
            _managers[interfaceBase].SetValueFast(signalName, value);
        }
        
        public static bool TrySetSignalValue<T>(this InterfaceBaseClass interfaceBase, string signalName, T value)
        {
            EnsureInitialized(interfaceBase);
            if (_managers[interfaceBase].HasSignal(signalName))
            {
                _managers[interfaceBase].SetValueFast(signalName, value);
                return true;
            }
            return false;
        }
        
        #endregion
        
        #region High-Performance Signal Discovery and Filtering
        
        //! Gets Signal component by name using high-performance lookup
        public static Signal GetSignalComponent(this InterfaceBaseClass interfaceBase, string signalName)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].GetSignalFast(signalName);
        }
        
        public static IEnumerable<Signal> GetAllSignals(this InterfaceBaseClass interfaceBase)
        {
            EnsureInitialized(interfaceBase);
            return interfaceBase.GetComponentsInChildren<Signal>(true); // true = include inactive
        }
        
        public static IEnumerable<T> GetSignals<T>(this InterfaceBaseClass interfaceBase) where T : Signal
        {
            return interfaceBase.GetComponentsInChildren<T>(true); // true = include inactive
        }
        
        public static IEnumerable<Signal> GetInputSignals(this InterfaceBaseClass interfaceBase)
        {
            return GetAllSignals(interfaceBase).Where(s => s.IsInput());
        }
        
        public static IEnumerable<Signal> GetOutputSignals(this InterfaceBaseClass interfaceBase)
        {
            return GetAllSignals(interfaceBase).Where(s => !s.IsInput());
        }
        
        public static bool HasSignal(this InterfaceBaseClass interfaceBase, string signalName)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].HasSignal(signalName);
        }
        
        public static int GetSignalCount(this InterfaceBaseClass interfaceBase)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].SignalCount;
        }
        
        #endregion
        
        #region High-Performance Signal Creation
        
        public static Signal CreateSignalSafe(this InterfaceBaseClass interfaceBase, string name, SignalType signalType, SignalDirection direction)
        {
            try
            {
                var signalGO = new GameObject(name);
                signalGO.transform.parent = interfaceBase.transform;
                
                // Add appropriate signal component
                Signal signal = null;
                switch (signalType)
                {
                    case SignalType.Bool:
                        signal = direction == SignalDirection.Input 
                            ? signalGO.AddComponent<PLCInputBool>() as Signal
                            : signalGO.AddComponent<PLCOutputBool>() as Signal;
                        break;
                    case SignalType.Int:
                        signal = direction == SignalDirection.Input
                            ? signalGO.AddComponent<PLCInputInt>() as Signal
                            : signalGO.AddComponent<PLCOutputInt>() as Signal;
                        break;
                    case SignalType.Float:
                        signal = direction == SignalDirection.Input
                            ? signalGO.AddComponent<PLCInputFloat>() as Signal
                            : signalGO.AddComponent<PLCOutputFloat>() as Signal;
                        break;
                    case SignalType.Text:
                        signal = direction == SignalDirection.Input
                            ? signalGO.AddComponent<PLCInputText>() as Signal
                            : signalGO.AddComponent<PLCOutputText>() as Signal;
                        break;
                    default:
                        Logger.Error($"Unsupported signal type: {signalType}");
                        UnityEngine.Object.DestroyImmediate(signalGO);
                        return null;
                }
                
                // Ensure metadata is initialized for new signals
                if (signal != null)
                {
                    // Always set the Name property to match GameObject name
                    signal.Name = name;
                    
                    if (signal.Metadata == null)
                    {
                        signal.Metadata = new SignalMetadata();
                    }
                }
                
                // Refresh high-performance manager after creation
                RefreshSignalManager(interfaceBase);
                
                return signal;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to create signal '{name}' of type {signalType} {direction}: {ex.Message}");
                return null;
            }
        }
        
        public static T CreateSignal<T>(this InterfaceBaseClass interfaceBase, string name, SignalDirection direction) where T : struct
        {
            var signalType = SignalTypeHelper.GetSignalType<T>();
            var signal = CreateSignalSafe(interfaceBase, name, signalType, direction);
            
            if (signal != null)
            {
                return GetSignalValue<T>(interfaceBase, name);
            }
            
            return default(T);
        }
        
        public static bool CreateSignalIfNotExists(this InterfaceBaseClass interfaceBase, string name, SignalType signalType, SignalDirection direction)
        {
            if (HasSignal(interfaceBase, name))
                return true; // Already exists
                
            return CreateSignalSafe(interfaceBase, name, signalType, direction) != null;
        }
        
        #endregion
        
        #region High-Performance Batch Operations
        
        public static Dictionary<string, object> GetAllSignalValues(this InterfaceBaseClass interfaceBase)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].GetAllValuesFast();
        }
        
        public static void SetMultipleSignalValues(this InterfaceBaseClass interfaceBase, Dictionary<string, object> values)
        {
            EnsureInitialized(interfaceBase);
            _managers[interfaceBase].UpdateBatch(values);
        }
        
        public static Dictionary<string, object> ReadAllInputs(this InterfaceBaseClass interfaceBase)
        {
            EnsureInitialized(interfaceBase);
            var result = new Dictionary<string, object>();
            _managers[interfaceBase].ReadAllInputs(result);
            return result;
        }
        
        public static void WriteAllOutputs(this InterfaceBaseClass interfaceBase, Dictionary<string, object> values)
        {
            EnsureInitialized(interfaceBase);
            _managers[interfaceBase].WriteAllOutputs(values);
        }
        
        public static int CountSignals(this InterfaceBaseClass interfaceBase, SignalDirection? direction = null, SignalType? signalType = null)
        {
            var signals = GetAllSignals(interfaceBase);
            
            if (direction.HasValue)
            {
                bool isInput = direction.Value == SignalDirection.Input;
                signals = signals.Where(s => s.IsInput() == isInput);
            }
            
            if (signalType.HasValue)
            {
                signals = signals.Where(s => 
                {
                    var valueType = s.GetValue()?.GetType();
                    return valueType != null && SignalTypeHelper.GetSignalType(valueType) == signalType.Value;
                });
            }
            
            return signals.Count();
        }
        
        #endregion
        
        #region High-Performance Manager Utilities
        
        private static void EnsureInitialized(InterfaceBaseClass interfaceBase)
        {
            if (!_managers.TryGetValue(interfaceBase, out var manager))
            {
                manager = new FastSignalManager();
                _managers[interfaceBase] = manager;
            }
            
            if (!manager.IsInitialized)
            {
                // Check if we're on the main thread
                if (System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThreadId)
                {
                    // We're on main thread - safe to auto-initialize
                    try
                    {
                        manager.Initialize(interfaceBase);
                        
                        // Log the auto-initialization if in debug mode
                        var fastInterface = interfaceBase as FastInterfaceBase;
                        if (fastInterface != null && fastInterface.DebugMode)
                        {
                            UnityEngine.Debug.Log($"[{interfaceBase.GetType().Name}] Signal manager auto-initialized on demand (main thread)");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        throw new System.InvalidOperationException(
                            $"Failed to auto-initialize signal manager: {ex.Message}", ex);
                    }
                }
                else
                {
                    // We're on a background thread - cannot auto-initialize
                    throw new System.InvalidOperationException(
                        "Signal manager not initialized. Must call RefreshSignalManager() on main thread before using signals from background threads. " + 
                        "This indicates a race condition or missing pre-initialization.");
                }
            }
        }
        
        public static void RefreshSignalManager(this InterfaceBaseClass interfaceBase)
        {
            if (!_managers.TryGetValue(interfaceBase, out var manager))
            {
                // Create new manager if it doesn't exist
                manager = new FastSignalManager();
                _managers[interfaceBase] = manager;
            }
            
            // Initialize or re-initialize the manager
            manager.Initialize(interfaceBase);
        }
        
        public static void ClearSignalManager(this InterfaceBaseClass interfaceBase)
        {
            _managers.Remove(interfaceBase);
        }
        
        public static bool IsHighPerformanceModeActive(this InterfaceBaseClass interfaceBase)
        {
            return _managers.TryGetValue(interfaceBase, out var manager) && manager.IsInitialized;
        }
        
        #endregion
        
        #region Event-Driven Signal Helper Methods
        
        /// <summary>
        /// Create and manage event-driven output buffer for PLC data (PLC → Unity, thread-safe)
        /// </summary>
        public static void EnqueueOutputData(this InterfaceBaseClass interfaceBase, Dictionary<string, object> data, 
            Queue<Dictionary<string, object>> outputBuffer, object lockObject, int maxBufferSize = 100)
        {
            if (data == null || data.Count == 0) return;
            
            lock (lockObject)
            {
                // Prevent buffer overflow
                if (outputBuffer.Count >= maxBufferSize)
                {
                    ThreadSafeLogger.LogWarning($"Output buffer full ({maxBufferSize}), dropping oldest data", interfaceBase.GetType().Name);
                    outputBuffer.Dequeue(); // Remove oldest
                }
                
                outputBuffer.Enqueue(data);
            }
        }
        
        /// <summary>
        /// Process all buffered output data and write to Unity signals (main thread only)
        /// </summary>
        public static int ProcessOutputBuffer(this InterfaceBaseClass interfaceBase, 
            Queue<Dictionary<string, object>> outputBuffer, object lockObject)
        {
            var processedCount = 0;
            lock (lockObject)
            {
                while (outputBuffer.Count > 0)
                {
                    var data = outputBuffer.Dequeue();
                    interfaceBase.SetMultipleSignalValues(data);  // Use existing batch method
                    processedCount++;
                }
            }
            return processedCount;
        }
        
        /// <summary>
        /// Add input data to send queue (Unity → PLC, thread-safe)
        /// </summary>
        public static void EnqueueInputData(this InterfaceBaseClass interfaceBase, Dictionary<string, object> inputs,
            Queue<Dictionary<string, object>> inputBuffer, object lockObject)
        {
            if (inputs == null || inputs.Count == 0) return;
            
            lock (lockObject)
            {
                inputBuffer.Enqueue(inputs);
            }
        }
        
        /// <summary>
        /// Process batched input data for sending to device (communication thread safe)
        /// </summary>
        public static Dictionary<string, object>[] ProcessInputBuffer(this InterfaceBaseClass interfaceBase,
            Queue<Dictionary<string, object>> inputBuffer, object lockObject, int maxBatchSize = 10)
        {
            var results = new List<Dictionary<string, object>>();
            
            lock (lockObject)
            {
                int batchCount = 0;
                while (inputBuffer.Count > 0 && batchCount < maxBatchSize)
                {
                    results.Add(inputBuffer.Dequeue());
                    batchCount++;
                }
            }
            
            return results.ToArray();
        }
        
        /// <summary>
        /// Check if there are pending inputs to send
        /// </summary>
        public static bool HasPendingInputs(this InterfaceBaseClass interfaceBase,
            Queue<Dictionary<string, object>> inputBuffer, object lockObject)
        {
            lock (lockObject)
            {
                return inputBuffer.Count > 0;
            }
        }
        
        /// <summary>
        /// Clear all event-driven buffers (call on disconnect)
        /// </summary>
        public static void ClearEventBuffers(this InterfaceBaseClass interfaceBase,
            Queue<Dictionary<string, object>> outputBuffer, Queue<Dictionary<string, object>> inputBuffer, object lockObject)
        {
            lock (lockObject)
            {
                var outputCount = outputBuffer.Count;
                var inputCount = inputBuffer.Count;
                
                outputBuffer.Clear();
                inputBuffer.Clear();
                
                ThreadSafeLogger.LogInfo($"Cleared buffers: {outputCount} outputs, {inputCount} inputs", interfaceBase.GetType().Name);
            }
        }
        
        #endregion
        
        #region Signal Name Access (Thread-Safe)
        
        /// <summary>
        /// Get all input signal names - thread-safe after initialization
        /// </summary>
        public static List<string> GetInputSignalNames(this InterfaceBaseClass interfaceBase)
        {
            if (_managers.TryGetValue(interfaceBase, out var manager))
            {
                return manager.GetInputSignalNames();
            }
            return new List<string>();
        }
        
        /// <summary>
        /// Get all output signal names - thread-safe after initialization
        /// </summary>
        public static List<string> GetOutputSignalNames(this InterfaceBaseClass interfaceBase)
        {
            if (_managers.TryGetValue(interfaceBase, out var manager))
            {
                return manager.GetOutputSignalNames();
            }
            return new List<string>();
        }
        
        /// <summary>
        /// Get all signal names - thread-safe after initialization
        /// </summary>
        public static List<string> GetAllSignalNames(this InterfaceBaseClass interfaceBase)
        {
            if (_managers.TryGetValue(interfaceBase, out var manager))
            {
                return manager.GetAllSignalNames();
            }
            return new List<string>();
        }
        
        #endregion
        
        #region Thread-Safe Metadata Operations
        
        //! Gets signal metadata in thread-safe manner using cached copy from initialization
        public static T GetSignalMetadataSafe<T>(this InterfaceBaseClass interfaceBase, string signalName, string key, T defaultValue = default(T))
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].GetMetadataSafe(signalName, key, defaultValue);
        }
        
        //! Gets all metadata for a signal in thread-safe manner using cached copy
        public static Dictionary<string, object> GetAllSignalMetadataSafe(this InterfaceBaseClass interfaceBase, string signalName)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].GetAllMetadataSafe(signalName);
        }
        
        //! Checks if signal has metadata key in thread-safe manner
        public static bool HasSignalMetadataSafe(this InterfaceBaseClass interfaceBase, string signalName, string key)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].HasMetadataSafe(signalName, key);
        }
        
        //! Gets input signals with their metadata in thread-safe batch operation
        public static Dictionary<string, SignalWithMetadata> GetInputsWithMetadataSafe(this InterfaceBaseClass interfaceBase)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].GetSignalsWithMetadataSafe(inputsOnly: true);
        }
        
        //! Gets output signals with their metadata in thread-safe batch operation
        public static Dictionary<string, SignalWithMetadata> GetOutputsWithMetadataSafe(this InterfaceBaseClass interfaceBase)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].GetSignalsWithMetadataSafe(outputsOnly: true);
        }
        
        //! Gets all signals with their metadata in thread-safe batch operation
        public static Dictionary<string, SignalWithMetadata> GetAllSignalsWithMetadataSafe(this InterfaceBaseClass interfaceBase)
        {
            EnsureInitialized(interfaceBase);
            return _managers[interfaceBase].GetSignalsWithMetadataSafe();
        }
        
        #endregion
        
        #region Metadata-Aware Signal Operations
        
        //! Sets metadata for a signal by name
        public static void SetSignalMetadata(this InterfaceBaseClass interfaceBase, string signalName, string key, object value)
        {
            var signal = interfaceBase.GetSignalComponent(signalName);
            signal?.SetMetadata(key, value);
        }
        
        //! Gets metadata for a signal by name with type conversion
        public static T GetSignalMetadata<T>(this InterfaceBaseClass interfaceBase, string signalName, string key, T defaultValue = default(T))
        {
            var signal = interfaceBase.GetSignalComponent(signalName);
            return signal != null ? signal.GetMetadata(key, defaultValue) : defaultValue;
        }
        
        //! Gets all input signals with their metadata for interface communication
        public static Dictionary<string, SignalWithMetadata> GetInputsWithMetadata(this InterfaceBaseClass interfaceBase)
        {
            var result = new Dictionary<string, SignalWithMetadata>();
            var inputs = interfaceBase.ReadAllInputs();
            
            foreach (var input in inputs)
            {
                var signal = interfaceBase.GetSignalComponent(input.Key);
                if (signal != null)
                {
                    var signalData = new SignalWithMetadata
                    {
                        Name = input.Key,
                        Value = input.Value,
                        Metadata = new Dictionary<string, object>()
                    };
                    
                    // Copy all metadata
                    foreach (var key in signal.GetMetadataKeys())
                    {
                        signalData.Metadata[key] = signal.GetMetadata<object>(key);
                    }
                    
                    result[input.Key] = signalData;
                }
            }
            
            return result;
        }
        
        //! Creates signal with metadata configuration
        public static Signal CreateSignalWithMetadata(this InterfaceBaseClass interfaceBase, string name, SignalType signalType, 
            SignalDirection direction, Dictionary<string, object> metadata = null)
        {
            var signal = interfaceBase.CreateSignalSafe(name, signalType, direction);
            
            if (signal != null && metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    signal.SetMetadata(kvp.Key, kvp.Value);
                }
            }
            
            return signal;
        }
        
        //! Gets signals filtered by metadata key and optional value
        public static IEnumerable<Signal> GetSignalsByMetadata(this InterfaceBaseClass interfaceBase, string metadataKey, object metadataValue = null)
        {
            var allSignals = interfaceBase.GetAllSignals();
            
            return allSignals.Where(signal => 
            {
                if (!signal.HasMetadata(metadataKey))
                    return false;
                    
                if (metadataValue == null)
                    return true; // Just check if key exists
                    
                var value = signal.GetMetadata<object>(metadataKey);
                return Equals(value, metadataValue);
            });
        }
        
        //! Finds existing signal by name - checks Signal.Name property first, then GameObject name
        //! This matches the logic used by GetSignalName() for consistent lookups
        public static Signal FindExistingSignal(this InterfaceBaseClass interfaceBase, string signalName)
        {
            // This uses GetSignalComponent which internally uses GetSignalName()
            // GetSignalName() checks Name property first, then falls back to GameObject name
            return interfaceBase.GetSignalComponent(signalName);
        }
        
        //! Creates or updates a signal based on requirements - comprehensive signal management
        //! This method handles all signal creation and update scenarios:
        //! - Creates new signals if they don't exist (checks by Name property, then GameObject name)
        //! - Updates metadata for existing signals with same direction
        //! - Replaces signal component when direction changes (keeping GameObject)
        //! - Preserves GameObject hierarchy, transform, and NEVER changes GameObject name
        //! - For existing signals: GameObject name remains unchanged even if import name differs
        public static Signal CreateOrUpdateSignal(this InterfaceBaseClass interfaceBase, 
            string signalName, 
            SignalType signalType, 
            SignalDirection direction, 
            Dictionary<string, object> metadata = null,
            bool allowDirectionChange = true)
        {
            // Delegate to overloaded method with no parent specified
            return CreateOrUpdateSignal(interfaceBase, signalName, signalType, direction, 
                null, metadata, allowDirectionChange);
        }
        
        //! Creates or updates a signal with hierarchy support
        //! - Allows specifying a parent transform for deep hierarchy creation
        //! - Creates intermediate folders if needed when hierarchyPath is provided
        //! - For existing signals: Preserves location and GameObject name
        public static Signal CreateOrUpdateSignal(this InterfaceBaseClass interfaceBase, 
            string signalName, 
            SignalType signalType, 
            SignalDirection direction,
            Transform parentTransform,
            Dictionary<string, object> metadata = null,
            bool allowDirectionChange = true)
        {
            // Check if signal already exists using Name property first, then GameObject name
            var existingSignal = interfaceBase.FindExistingSignal(signalName);
            
            if (existingSignal != null)
            {
                // Signal exists - check what needs updating
                bool existingIsInput = existingSignal.IsInput();
                bool newIsInput = (direction == SignalDirection.Input);
                
                if (existingIsInput != newIsInput)
                {
                    // Direction has changed
                    if (!allowDirectionChange)
                    {
                        Logger.Warning($"Signal '{signalName}' direction change not allowed. Keeping existing direction.");
                        UpdateSignalMetadata(existingSignal, metadata);
                        return existingSignal;
                    }
                    
                    // Replace the signal component while keeping the GameObject
                    return ReplaceSignalComponent(interfaceBase, existingSignal, signalType, direction, metadata);
                }
                else
                {
                    // Same direction - just update metadata
                    UpdateSignalMetadata(existingSignal, metadata);
                    return existingSignal;
                }
            }
            else
            {
                // Create new signal with optional parent
                if (parentTransform != null)
                {
                    // Create signal with hierarchy
                    return CreateSignalInHierarchy(interfaceBase, signalName, signalType, direction, 
                        parentTransform, metadata);
                }
                else
                {
                    // Create signal in default location
                    return interfaceBase.CreateSignalWithMetadata(signalName, signalType, direction, metadata);
                }
            }
        }
        
        //! Creates a signal in a specific hierarchy location
        private static Signal CreateSignalInHierarchy(InterfaceBaseClass interfaceBase,
            string signalName,
            SignalType signalType,
            SignalDirection direction,
            Transform parentTransform,
            Dictionary<string, object> metadata)
        {
            // Create the GameObject at the specified location
            GameObject signalObject = new GameObject(signalName);
            signalObject.transform.parent = parentTransform;
            
            // Add the appropriate signal component
            Signal signal = null;
            switch (signalType)
            {
                case SignalType.Bool:
                    signal = direction == SignalDirection.Input 
                        ? signalObject.AddComponent<PLCInputBool>() as Signal
                        : signalObject.AddComponent<PLCOutputBool>() as Signal;
                    break;
                case SignalType.Int:
                    signal = direction == SignalDirection.Input
                        ? signalObject.AddComponent<PLCInputInt>() as Signal
                        : signalObject.AddComponent<PLCOutputInt>() as Signal;
                    break;
                case SignalType.Float:
                    signal = direction == SignalDirection.Input
                        ? signalObject.AddComponent<PLCInputFloat>() as Signal
                        : signalObject.AddComponent<PLCOutputFloat>() as Signal;
                    break;
                case SignalType.Text:
                    signal = direction == SignalDirection.Input
                        ? signalObject.AddComponent<PLCInputText>() as Signal
                        : signalObject.AddComponent<PLCOutputText>() as Signal;
                    break;
            }
            
            if (signal != null)
            {
                // Configure the signal
                signal.Name = signalName;
                signal.Settings.Active = true;
                signal.SetStatusConnected(true);
                
                // Apply metadata if provided
                UpdateSignalMetadata(signal, metadata);
                
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(signalObject);
                }
                #endif
            }
            
            return signal;
        }
        
        //! Replaces signal component on existing GameObject
        //! IMPORTANT: This preserves the existing GameObject and its name - only the component is replaced
        private static Signal ReplaceSignalComponent(InterfaceBaseClass interfaceBase, 
            Signal existingSignal, 
            SignalType signalType, 
            SignalDirection direction,
            Dictionary<string, object> metadata)
        {
            // Preserve the existing GameObject - its name will NOT be changed
            var gameObject = existingSignal.gameObject;
            var signalName = existingSignal.name;
            
            // Store transform information
            var parent = gameObject.transform.parent;
            var siblingIndex = gameObject.transform.GetSiblingIndex();
            
            // Remove old signal component
            #if UNITY_EDITOR
            UnityEditor.Undo.DestroyObjectImmediate(existingSignal);
            #else
            UnityEngine.Object.DestroyImmediate(existingSignal);
            #endif
            
            // Add new signal component based on type and direction
            Signal newSignal = null;
            
            // Create appropriate signal component
            switch (signalType)
            {
                case SignalType.Bool:
                    newSignal = direction == SignalDirection.Input 
                        ? gameObject.AddComponent<PLCInputBool>() as Signal
                        : gameObject.AddComponent<PLCOutputBool>() as Signal;
                    break;
                case SignalType.Int:
                    newSignal = direction == SignalDirection.Input
                        ? gameObject.AddComponent<PLCInputInt>() as Signal
                        : gameObject.AddComponent<PLCOutputInt>() as Signal;
                    break;
                case SignalType.Float:
                    newSignal = direction == SignalDirection.Input
                        ? gameObject.AddComponent<PLCInputFloat>() as Signal
                        : gameObject.AddComponent<PLCOutputFloat>() as Signal;
                    break;
                case SignalType.Text:
                    newSignal = direction == SignalDirection.Input
                        ? gameObject.AddComponent<PLCInputText>() as Signal
                        : gameObject.AddComponent<PLCOutputText>() as Signal;
                    break;
            }
            
            if (newSignal != null)
            {
                // Set the Name property to match GameObject name
                newSignal.Name = gameObject.name;
                
                // Ensure metadata is initialized
                if (newSignal.Metadata == null)
                {
                    newSignal.Metadata = new SignalMetadata();
                }
                
                // Update metadata
                UpdateSignalMetadata(newSignal, metadata);
                
                // Refresh signal manager
                interfaceBase.RefreshSignalManager();
            }
            
            return newSignal;
        }
        
        //! Updates signal metadata
        private static void UpdateSignalMetadata(Signal signal, Dictionary<string, object> metadata)
        {
            if (signal == null || metadata == null)
                return;
            
            // Ensure signal's metadata is initialized
            if (signal.Metadata == null)
            {
                Logger.Warning($"Signal '{signal.name}' has null metadata, initializing...", signal);
                signal.Metadata = new SignalMetadata();
            }
                
            foreach (var kvp in metadata)
            {
                signal.SetMetadata(kvp.Key, kvp.Value);
            }
        }
        
        //! Copies all metadata from one signal to another signal
        public static void CopySignalMetadata(this InterfaceBaseClass interfaceBase, string fromSignal, string toSignal)
        {
            var source = interfaceBase.GetSignalComponent(fromSignal);
            var target = interfaceBase.GetSignalComponent(toSignal);
            
            if (source != null && target != null)
            {
                foreach (var key in source.GetMetadataKeys())
                {
                    var value = source.GetMetadata<object>(key);
                    target.SetMetadata(key, value);
                }
            }
        }
        
        #endregion
    }
}

namespace realvirtual
{
    //! Signal data container with metadata for interface communication
    public class SignalWithMetadata
    {
        public string Name; //!< Signal name
        public object Value; //!< Current signal value
        public Dictionary<string, object> Metadata = new Dictionary<string, object>(); //!< Signal metadata key-value pairs
    }
}