// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections.Generic;

namespace realvirtual
{
    //! Signal data types for interface communication
    public enum SignalType
    {
        Bool,     //!< Boolean signal type
        Int,      //!< Integer signal type
        Float,    //!< Floating point signal type
        Text,     //!< Text/string signal type
        Transform //!< Transform/pose signal type
    }
    
    //! Signal direction for data flow
    public enum SignalDirection
    {
        Input,  //!< Input signal (Unity to PLC)
        Output  //!< Output signal (PLC to Unity)
    }
    
    //! Helper class for signal type conversion and management
    public static class SignalTypeHelper
    {
        private static readonly Dictionary<Type, SignalType> TypeMapping = new Dictionary<Type, SignalType>
        {
            { typeof(bool), SignalType.Bool },
            { typeof(int), SignalType.Int },
            { typeof(float), SignalType.Float },
            { typeof(string), SignalType.Text },
            { typeof(UnityEngine.Pose), SignalType.Transform }
        };
        
        private static readonly Dictionary<Type, string> SignalClassNames = new Dictionary<Type, string>
        {
            { typeof(PLCInputBool), "BOOL" },
            { typeof(PLCOutputBool), "BOOL" },
            { typeof(PLCInputInt), "INT" },
            { typeof(PLCOutputInt), "INT" },
            { typeof(PLCInputFloat), "FLOAT" },
            { typeof(PLCOutputFloat), "FLOAT" },
            { typeof(PLCInputText), "TEXT" },
            { typeof(PLCOutputText), "TEXT" },
            { typeof(PLCInputTransform), "TRANSFORM" },
            { typeof(PLCOutputTransform), "TRANSFORM" }
        };
        
        //! Gets SignalType for generic type parameter
        public static SignalType GetSignalType<T>() => GetSignalType(typeof(T));
        
        //! Gets SignalType for specific Type
        public static SignalType GetSignalType(Type type)
        {
            if (TypeMapping.TryGetValue(type, out var signalType))
                return signalType;
            
            throw new ArgumentException($"Unsupported signal type: {type.Name}");
        }
        
        //! Gets .NET Type for SignalType
        public static Type GetValueType(SignalType signalType)
        {
            return signalType switch
            {
                SignalType.Bool => typeof(bool),
                SignalType.Int => typeof(int),
                SignalType.Float => typeof(float),
                SignalType.Text => typeof(string),
                SignalType.Transform => typeof(UnityEngine.Pose),
                _ => throw new ArgumentException($"Unknown signal type: {signalType}")
            };
        }
        
        //! Converts modern SignalType to legacy SIGNALTYPE enum
        public static SIGNALTYPE ToLegacySignalType(SignalType signalType)
        {
            return signalType switch
            {
                SignalType.Bool => SIGNALTYPE.BOOL,
                SignalType.Int => SIGNALTYPE.INT,
                SignalType.Float => SIGNALTYPE.REAL,
                SignalType.Text => SIGNALTYPE.TEXT,
                SignalType.Transform => SIGNALTYPE.TRANSFORM,
                _ => throw new ArgumentException($"Cannot convert signal type: {signalType}")
            };
        }
        
        //! Converts modern SignalDirection to legacy SIGNALDIRECTION enum
        public static SIGNALDIRECTION ToLegacySignalDirection(SignalDirection direction)
        {
            return direction switch
            {
                SignalDirection.Input => SIGNALDIRECTION.INPUT,
                SignalDirection.Output => SIGNALDIRECTION.OUTPUT,
                _ => throw new ArgumentException($"Cannot convert signal direction: {direction}")
            };
        }
        
        //! Converts legacy SIGNALTYPE to modern SignalType
        public static SignalType FromLegacySignalType(SIGNALTYPE legacyType)
        {
            return legacyType switch
            {
                SIGNALTYPE.BOOL => SignalType.Bool,
                SIGNALTYPE.INT or SIGNALTYPE.DINT or SIGNALTYPE.BYTE or SIGNALTYPE.WORD or SIGNALTYPE.DWORD => SignalType.Int,
                SIGNALTYPE.REAL => SignalType.Float,
                SIGNALTYPE.TEXT => SignalType.Text,
                SIGNALTYPE.TRANSFORM => SignalType.Transform,
                _ => throw new ArgumentException($"Cannot convert legacy signal type: {legacyType}")
            };
        }
        
        //! Converts legacy SIGNALDIRECTION to modern SignalDirection
        public static SignalDirection FromLegacySignalDirection(SIGNALDIRECTION legacyDirection)
        {
            return legacyDirection switch
            {
                SIGNALDIRECTION.INPUT => SignalDirection.Input,
                SIGNALDIRECTION.OUTPUT => SignalDirection.Output,
                _ => throw new ArgumentException($"Cannot convert legacy signal direction: {legacyDirection}")
            };
        }
        
        //! Gets human-readable type name for signal
        public static string GetSignalTypeName(Signal signal)
        {
            if (signal == null) return "UNKNOWN";
            
            var signalType = signal.GetType();
            if (SignalClassNames.TryGetValue(signalType, out var typeName))
                return typeName;
                
            return signalType.Name.ToUpper();
        }
        
        //! Checks if signal is an input signal
        public static bool IsInputSignal(Signal signal)
        {
            return signal?.IsInput() ?? false;
        }
        
        //! Checks if signal is an output signal
        public static bool IsOutputSignal(Signal signal)
        {
            return !IsInputSignal(signal);
        }
        
        //! Gets Unity component type for signal type and direction combination
        public static Type GetSignalClassType(SignalType signalType, SignalDirection direction)
        {
            return (signalType, direction) switch
            {
                (SignalType.Bool, SignalDirection.Input) => typeof(PLCInputBool),
                (SignalType.Bool, SignalDirection.Output) => typeof(PLCOutputBool),
                (SignalType.Int, SignalDirection.Input) => typeof(PLCInputInt),
                (SignalType.Int, SignalDirection.Output) => typeof(PLCOutputInt),
                (SignalType.Float, SignalDirection.Input) => typeof(PLCInputFloat),
                (SignalType.Float, SignalDirection.Output) => typeof(PLCOutputFloat),
                (SignalType.Text, SignalDirection.Input) => typeof(PLCInputText),
                (SignalType.Text, SignalDirection.Output) => typeof(PLCOutputText),
                (SignalType.Transform, SignalDirection.Input) => typeof(PLCInputTransform),
                (SignalType.Transform, SignalDirection.Output) => typeof(PLCOutputTransform),
                _ => throw new ArgumentException($"No signal class for type {signalType} and direction {direction}")
            };
        }
        
        //! Attempts to convert value to target type with type safety
        public static bool TryConvertValue<T>(object value, out T result)
        {
            result = default(T);
            
            if (value == null)
                return false;
                
            try
            {
                if (value is T directValue)
                {
                    result = directValue;
                    return true;
                }
                
                // Handle string conversions
                if (typeof(T) == typeof(bool) && value is string strValue)
                {
                    if (strValue == "0" || strValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                    {
                        result = (T)(object)false;
                        return true;
                    }
                    if (strValue == "1" || strValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        result = (T)(object)true;
                        return true;
                    }
                }
                
                result = (T)Convert.ChangeType(value, typeof(T));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}