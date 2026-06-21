// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    //! Fast signal management with O(1) lookups and direct value access
    public class FastSignalManager
    {
        private readonly Dictionary<string, Signal> _signalLookup = new Dictionary<string, Signal>();
        private readonly Dictionary<string, IDirectSignalAccess> _directAccessLookup = new Dictionary<string, IDirectSignalAccess>();
        private readonly List<IDirectSignalAccess> _allSignals = new List<IDirectSignalAccess>();
        
        // Thread-safe metadata cache
        private readonly Dictionary<string, Dictionary<string, object>> _metadataCache = new Dictionary<string, Dictionary<string, object>>();
        
        private bool _initialized = false;
        
        public interface IDirectSignalAccess
        {
            string SignalName { get; }
            bool IsInput { get; }
            Type ValueType { get; }
            object GetValueDirect();
            void SetValueDirect(object value);
            bool IsConnected { get; }
        }
        
        //! Initializes the manager with signals from an interface
        public void Initialize(InterfaceBaseClass interfaceBase)
        {
            _signalLookup.Clear();
            _directAccessLookup.Clear();
            _allSignals.Clear();
            _metadataCache.Clear();
            
            // Get all signals once and cache everything
            var signals = interfaceBase.GetComponentsInChildren<Signal>(true);
            
            foreach (var signal in signals)
            {
                var signalName = signal.GetSignalName();
                if (string.IsNullOrEmpty(signalName))
                {
                    Logger.Warning($"Signal on GameObject '{signal.gameObject.name}' has no name, skipping...", signal.gameObject);
                    continue;
                }
                
                _signalLookup[signalName] = signal;
                
                // Create direct access wrapper
                var directAccess = CreateDirectAccess(signal);
                if (directAccess != null)
                {
                    _directAccessLookup[signalName] = directAccess;
                    _allSignals.Add(directAccess);
                }
                
                // Cache metadata for thread-safe access
                CacheSignalMetadata(signal, signalName);
            }
            
            _initialized = true;
        }
        
        //! Caches signal metadata for thread-safe access
        private void CacheSignalMetadata(Signal signal, string signalName)
        {
            var metadataDict = new Dictionary<string, object>();

            // Cache existing signal metadata
            if (signal.Metadata != null)
            {
                foreach (var key in signal.GetMetadataKeys())
                {
                    try
                    {
                        var value = signal.GetMetadata<object>(key);
                        metadataDict[key] = value;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to cache metadata '{key}' for signal '{signalName}': {ex.Message}");
                    }
                }
            }

            // Add Signal properties to metadata for interface access
            if (!string.IsNullOrEmpty(signal.OriginDataType))
            {
                metadataDict["OriginDataType"] = signal.OriginDataType;
            }

            _metadataCache[signalName] = metadataDict;
        }
        
        //! O(1) signal lookup - much faster than GetSignal
        public Signal GetSignalFast(string name)
        {
            return _signalLookup.TryGetValue(name, out var signal) ? signal : null;
        }
        
        //! O(1) direct value access - bypasses Unity's GetComponent calls
        public T GetValueFast<T>(string signalName)
        {
            if (_directAccessLookup.TryGetValue(signalName, out var accessor))
            {
                var value = accessor.GetValueDirect();
                if (value is T directValue)
                    return directValue;
                    
                // Try conversion
                if (SignalTypeHelper.TryConvertValue<T>(value, out var convertedValue))
                    return convertedValue;
            }
            
            return default(T);
        }
        
        //! O(1) direct value setting
        public void SetValueFast<T>(string signalName, T value)
        {
            if (_directAccessLookup.TryGetValue(signalName, out var accessor))
            {
                accessor.SetValueDirect(value);
            }
        }
        
        //! Batch value updates - very fast for multiple signals
        public void UpdateBatch(Dictionary<string, object> values)
        {
            foreach (var kvp in values)
            {
                if (_directAccessLookup.TryGetValue(kvp.Key, out var accessor))
                {
                    accessor.SetValueDirect(kvp.Value);
                }
            }
        }
        
        //! Gets all values at once - useful for data logging or debugging
        public Dictionary<string, object> GetAllValuesFast()
        {
            var result = new Dictionary<string, object>(_allSignals.Count);
            
            foreach (var signal in _allSignals)
            {
                result[signal.SignalName] = signal.GetValueDirect();
            }
            
            return result;
        }
        
        //! Gets input signals only
        public void ReadAllInputs(Dictionary<string, object> outputDict)
        {
            foreach (var signal in _allSignals)
            {
                if (signal.IsInput)
                {
                    outputDict[signal.SignalName] = signal.GetValueDirect();
                }
            }
        }
        
        //! Sets output signals only
        public void WriteAllOutputs(Dictionary<string, object> inputDict)
        {
            foreach (var signal in _allSignals)
            {
                if (!signal.IsInput && inputDict.TryGetValue(signal.SignalName, out var value))
                {
                    signal.SetValueDirect(value);
                }
            }
        }
        
        //! Checks if signal exists
        public bool HasSignal(string signalName) => _directAccessLookup.ContainsKey(signalName);
        
        //! Gets signal count
        public int SignalCount => _allSignals.Count;
        
        public bool IsInitialized => _initialized;
        
        //! Gets all signals
        public IEnumerable<Signal> GetAllSignals()
        {
            return _signalLookup.Values;
        }
        
        //! Gets signal metadata in thread-safe manner using cached copy
        public T GetMetadataSafe<T>(string signalName, string key, T defaultValue = default(T))
        {
            if (_metadataCache.TryGetValue(signalName, out var metadata) && 
                metadata.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
        
        //! Gets all metadata for a signal in thread-safe manner using cached copy
        public Dictionary<string, object> GetAllMetadataSafe(string signalName)
        {
            if (_metadataCache.TryGetValue(signalName, out var metadata))
            {
                return new Dictionary<string, object>(metadata); // Return copy to avoid modification
            }
            return new Dictionary<string, object>();
        }
        
        //! Checks if signal has metadata key in thread-safe manner
        public bool HasMetadataSafe(string signalName, string key)
        {
            return _metadataCache.TryGetValue(signalName, out var metadata) && metadata.ContainsKey(key);
        }
        
        //! Gets signals with metadata for batch operations in thread-safe manner
        public Dictionary<string, SignalWithMetadata> GetSignalsWithMetadataSafe(bool inputsOnly = false, bool outputsOnly = false)
        {
            var result = new Dictionary<string, SignalWithMetadata>();
            
            foreach (var signal in _allSignals)
            {
                if (inputsOnly && !signal.IsInput) continue;
                if (outputsOnly && signal.IsInput) continue;
                
                var signalData = new SignalWithMetadata
                {
                    Name = signal.SignalName,
                    Value = signal.GetValueDirect(),
                    Metadata = GetAllMetadataSafe(signal.SignalName)
                };
                
                result[signal.SignalName] = signalData;
            }
            
            return result;
        }
        
        //! Clears all cached data
        public void Clear()
        {
            _signalLookup.Clear();
            _directAccessLookup.Clear();
            _allSignals.Clear();
            _metadataCache.Clear();
            _initialized = false;
        }
        
        //! Gets all input signal names in thread-safe manner once initialized
        public List<string> GetInputSignalNames()
        {
            var result = new List<string>();
            foreach (var signal in _allSignals)
            {
                if (signal.IsInput)
                {
                    result.Add(signal.SignalName);
                }
            }
            return result;
        }
        
        //! Gets all output signal names in thread-safe manner once initialized
        public List<string> GetOutputSignalNames()
        {
            var result = new List<string>();
            foreach (var signal in _allSignals)
            {
                if (!signal.IsInput)
                {
                    result.Add(signal.SignalName);
                }
            }
            return result;
        }
        
        //! Gets all signal names in thread-safe manner once initialized
        public List<string> GetAllSignalNames()
        {
            var result = new List<string>(_allSignals.Count);
            foreach (var signal in _allSignals)
            {
                result.Add(signal.SignalName);
            }
            return result;
        }
        
        private static IDirectSignalAccess CreateDirectAccess(Signal signal)
        {
            return signal switch
            {
                PLCInputBool boolInput => new BoolInputAccess(boolInput),
                PLCOutputBool boolOutput => new BoolOutputAccess(boolOutput),
                PLCInputInt intInput => new IntInputAccess(intInput),
                PLCOutputInt intOutput => new IntOutputAccess(intOutput),
                PLCInputFloat floatInput => new FloatInputAccess(floatInput),
                PLCOutputFloat floatOutput => new FloatOutputAccess(floatOutput),
                PLCInputText textInput => new TextInputAccess(textInput),
                PLCOutputText textOutput => new TextOutputAccess(textOutput),
                _ => null
            };
        }
        
        // Direct access implementations for maximum performance
        private class BoolInputAccess : IDirectSignalAccess
        {
            private readonly PLCInputBool _signal;
            public BoolInputAccess(PLCInputBool signal) => _signal = signal;
            public string SignalName => _signal.GetSignalName();
            public bool IsInput => true;
            public Type ValueType => typeof(bool);
            public object GetValueDirect() => _signal.Value;
            public void SetValueDirect(object value) => _signal.Value = (bool)value;
            public bool IsConnected => _signal.Status.Connected;
        }
        
        private class BoolOutputAccess : IDirectSignalAccess
        {
            private readonly PLCOutputBool _signal;
            public BoolOutputAccess(PLCOutputBool signal) => _signal = signal;
            public string SignalName => _signal.GetSignalName();
            public bool IsInput => false;
            public Type ValueType => typeof(bool);
            public object GetValueDirect() => _signal.Value;
            public void SetValueDirect(object value) => _signal.Value = (bool)value;
            public bool IsConnected => _signal.Status.Connected;
        }
        
        private class IntInputAccess : IDirectSignalAccess
        {
            private readonly PLCInputInt _signal;
            public IntInputAccess(PLCInputInt signal) => _signal = signal;
            public string SignalName => _signal.GetSignalName();
            public bool IsInput => true;
            public Type ValueType => typeof(int);
            public object GetValueDirect() => _signal.Value;
            public void SetValueDirect(object value) => _signal.Value = Convert.ToInt32(value);
            public bool IsConnected => _signal.Status.Connected;
        }
        
        private class IntOutputAccess : IDirectSignalAccess
        {
            private readonly PLCOutputInt _signal;
            public IntOutputAccess(PLCOutputInt signal) => _signal = signal;
            public string SignalName => _signal.GetSignalName();
            public bool IsInput => false;
            public Type ValueType => typeof(int);
            public object GetValueDirect() => _signal.Value;
            public void SetValueDirect(object value) => _signal.Value = Convert.ToInt32(value);
            public bool IsConnected => _signal.Status.Connected;
        }
        
        private class FloatInputAccess : IDirectSignalAccess
        {
            private readonly PLCInputFloat _signal;
            public FloatInputAccess(PLCInputFloat signal) => _signal = signal;
            public string SignalName => _signal.GetSignalName();
            public bool IsInput => true;
            public Type ValueType => typeof(float);
            public object GetValueDirect() => _signal.Value;
            public void SetValueDirect(object value) => _signal.Value = Convert.ToSingle(value);
            public bool IsConnected => _signal.Status.Connected;
        }
        
        private class FloatOutputAccess : IDirectSignalAccess
        {
            private readonly PLCOutputFloat _signal;
            public FloatOutputAccess(PLCOutputFloat signal) => _signal = signal;
            public string SignalName => _signal.GetSignalName();
            public bool IsInput => false;
            public Type ValueType => typeof(float);
            public object GetValueDirect() => _signal.Value;
            public void SetValueDirect(object value) => _signal.Value = Convert.ToSingle(value);
            public bool IsConnected => _signal.Status.Connected;
        }
        
        private class TextInputAccess : IDirectSignalAccess
        {
            private readonly PLCInputText _signal;
            public TextInputAccess(PLCInputText signal) => _signal = signal;
            public string SignalName => _signal.GetSignalName();
            public bool IsInput => true;
            public Type ValueType => typeof(string);
            public object GetValueDirect() => _signal.Value;
            public void SetValueDirect(object value) => _signal.Value = value?.ToString() ?? "";
            public bool IsConnected => _signal.Status.Connected;
        }
        
        private class TextOutputAccess : IDirectSignalAccess
        {
            private readonly PLCOutputText _signal;
            public TextOutputAccess(PLCOutputText signal) => _signal = signal;
            public string SignalName => _signal.GetSignalName();
            public bool IsInput => false;
            public Type ValueType => typeof(string);
            public object GetValueDirect() => _signal.Value;
            public void SetValueDirect(object value) => _signal.Value = value?.ToString() ?? "";
            public bool IsConnected => _signal.Status.Connected;
        }
    }
}