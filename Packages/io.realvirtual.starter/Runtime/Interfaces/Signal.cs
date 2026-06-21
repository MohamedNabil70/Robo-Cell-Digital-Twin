// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace realvirtual
{
    // Force recompilation - metadata null safety fixes applied
    [Serializable]
    //! Struct for Settings of Signals
    public struct SettingsSignal
    {
        [Tooltip("Only implemented for some special interfaces - please check the doc")]
        public bool Active;
        public bool DetectBoolFlanks;
        public bool Override;
    }

    [Serializable]
    //! Struct for current status of a bool signal
    public struct StatusBool
    {
        public bool Connected;
        public bool ValueOverride;
        public bool Value;
        [HideInInspector] public bool OldValue;
    }

    [Serializable]
    //! Struct for current status of a float signal
    public struct StatusFloat
    {
        public bool Connected;
        public float ValueOverride;
        public float Value;
        [HideInInspector] public float OldValue;
    }

    [Serializable]
    //! Struct for current status of a omt signal
    public struct StatusInt
    {
        public bool Connected;
        public int ValueOverride;
        public int Value;
        [HideInInspector] public int OldValue;
    }

    [Serializable]
    //! Struct for current status of a text signal
    public struct StatusText
    {
        public bool Connected;
        public string ValueOverride;
        public string Value;
        [HideInInspector] public string OldValue;
    }

    [Serializable]
    //! Struct for current status of a bool signal
    public struct StatusTransform
    {
        public bool Connected;

        [InfoBox("Value is taken from Tanform postion if not overwritten!")]
        public Pose Value;

        public Pose ValueOverride;

        [HideInInspector] public Pose OldValue;
    }


    [Serializable]
    //! Class for saving connection information for signal - Behavior where signal is connected tp and property  where signal is connected to
    public class Connection
    {
        public GameObject Behavior;
        public string ConnectionName;
    }

    [Serializable]
    //! Custom metadata entry for signals with key-value storage
    public class SignalMetadataEntry
    {
        public string Key; //!< Metadata key name
        public string Value; //!< Metadata value as string
    }

    [Serializable]
    //! Generic metadata container for signals providing key-value storage for interface configuration
    public class SignalMetadata
    {
        [FormerlySerializedAs("Properties")] public List<SignalMetadataEntry> Metadata = new List<SignalMetadataEntry>(); //!< List of metadata entries visible in Unity Inspector
        
        private Dictionary<string, object> metadataDict;
        private bool dictBuilt = false;
        
        public SignalMetadata()
        {
            if (Metadata == null)
                Metadata = new List<SignalMetadataEntry>();
        }
        
        //! Get metadata value with automatic type conversion
        public T Get<T>(string key, T defaultValue = default(T))
        {
            if (Metadata == null)
                return defaultValue;
                
            if (!dictBuilt)
                BuildDictionary();
                
            if (metadataDict.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T directValue)
                        return directValue;
                    
                    // Try to convert string values to requested type
                    if (value is string stringValue && typeof(T) != typeof(string))
                    {
                        return (T)Convert.ChangeType(stringValue, typeof(T));
                    }
                }
                catch
                {
                    // Conversion failed, return default
                }
            }
            
            return defaultValue;
        }
        
        //! Set metadata value for the specified key
        public void Set(string key, object value)
        {
            // Ensure Metadata list is initialized
            if (Metadata == null)
                Metadata = new List<SignalMetadataEntry>();
                
            if (!dictBuilt)
                BuildDictionary();
                
            metadataDict[key] = value;
            
            // Update serialized list
            var existing = Metadata.FirstOrDefault(x => x.Key == key);
            if (existing != null)
            {
                existing.Value = value?.ToString() ?? "";
            }
            else
            {
                Metadata.Add(new SignalMetadataEntry { Key = key, Value = value?.ToString() ?? "" });
            }
        }
        
        //! Check if metadata key exists in the container
        public bool Has(string key)
        {
            if (Metadata == null)
                return false;
                
            if (!dictBuilt)
                BuildDictionary();
            return metadataDict.ContainsKey(key);
        }
        
        //! Remove metadata key and its value from the container
        public void Remove(string key)
        {
            if (Metadata == null)
                return;
                
            if (!dictBuilt)
                BuildDictionary();
                
            metadataDict.Remove(key);
            Metadata.RemoveAll(x => x.Key == key);
        }
        
        //! Get all available metadata keys
        public IEnumerable<string> GetKeys()
        {
            if (Metadata == null)
                return new string[0];
                
            if (!dictBuilt)
                BuildDictionary();
            return metadataDict.Keys;
        }
        
        private void BuildDictionary()
        {
            metadataDict = new Dictionary<string, object>();
            if (Metadata != null)
            {
                foreach (var entry in Metadata)
                {
                    metadataDict[entry.Key] = entry.Value;
                }
            }
            dictBuilt = true;
        }
    }

    //! The base class for all Signals
    public class Signal : realvirtualBehavior, IInspector
    {
        public delegate void OnSignalChangedDelegate(Signal obj);

        [rvPlanner] public string Comment;
        [rvPlanner] public string OriginDataType;
        public SettingsSignal Settings;
        public SignalEvent EventSignalChanged;
        [HideInInspector] public bool Autoconnected;

        [HideInInspector]
        public bool UpdateEnable; // Turns on the Update function - for some signals (Transforms) needed

        [HideInInspector] public List<Connection> ConnectionInfo = new();

        [HideInInspector]
        public InterfaceSignal interfacesignal;
        protected string Visutext;
        
        [FormerlySerializedAs("metadata")] [SerializeField] public SignalMetadata Metadata = new SignalMetadata(); //!< Metadata for PLC communication and interface configuration

        private void Start()
        {
            SignalChangedEvent(this);
            if (EventSignalChanged != null)
            {
                if (EventSignalChanged.GetPersistentEventCount() == 0)
                    enabled = false;
                else
                    enabled = true;
            }
            else
            {
                enabled = false;
            }
            if (Settings.DetectBoolFlanks)
                enabled = true;

            if (UpdateEnable)
                enabled = true;
        }
        #if REALVIRTUAL_PLANNER
                public void ChangeSignalType(Signal newComponent)
                {
                    newComponent.Name = Name;
                    newComponent.Comment = Comment;
                    newComponent.OriginDataType = OriginDataType;
                    
                    // Copy metadata to preserve interface mappings
                    if (Metadata != null && Metadata.Metadata != null && Metadata.Metadata.Count > 0)
                    {
                        if (newComponent.Metadata == null)
                            newComponent.Metadata = new SignalMetadata();
                            
                        foreach (var metadataEntry in Metadata.Metadata)
                        {
                            newComponent.Metadata.Set(metadataEntry.Key, metadataEntry.Value);
                        }
                    }
                    
                    newComponent.StartDelayedInspectorRoutine();
                    DestroyImmediate(this);
            
                }

                public void StartDelayedInspectorRoutine()
                {
                    StartCoroutine(OpenDelayedInspector());
                }

                IEnumerator OpenDelayedInspector()
                {
                    yield return null;
                    rvUIInspectorWindow.Open(gameObject);
                    PlannerSignalBrowser.Open();
                    yield return null;
                }

      
          [rvInspectorButton(ButtonLabel = "To Bool")] public void ToBool()
                {
                    if (IsInput())
                    {
                        if (GetType() == typeof(PLCInputBool))
                            return;
                
                        ChangeSignalType(gameObject.AddComponent<PLCInputBool>());
                    }
                    else
                    {
                        if (GetType() == typeof(PLCOutputBool))
                            return;
                
                        ChangeSignalType(gameObject.AddComponent<PLCOutputBool>());
                    }
            
                }
        
                [rvInspectorButton(ButtonLabel = "To Int")] public void ToInt()
                {
                    if (IsInput())
                    {
                        if (GetType() == typeof(PLCInputInt))
                            return;
                        ChangeSignalType(gameObject.AddComponent<PLCInputInt>());
                    }
                    else
                    {
                        if (GetType() == typeof(PLCOutputInt))
                            return;
                        ChangeSignalType(gameObject.AddComponent<PLCOutputInt>());
                    }
                }
        
                [rvInspectorButton(ButtonLabel = "To Float")] public void ToFloat()
                {
                    if (IsInput())
                    {
                        if (GetType() == typeof(PLCInputFloat))
                            return;
                        ChangeSignalType(gameObject.AddComponent<PLCInputFloat>());
                    }
                    else
                    {
                        if (GetType() == typeof(PLCOutputFloat))
                            return;
                        ChangeSignalType(gameObject.AddComponent<PLCOutputFloat>());
                    }
                }
                
                [rvInspectorButton(ButtonLabel = "To Text")] public void ToText()
                {
                    if (IsInput())
                    {
                        if (GetType() == typeof(PLCInputText))
                            return;
                        ChangeSignalType(gameObject.AddComponent<PLCInputText>());
                    }
                    else
                    {
                        if (GetType() == typeof(PLCOutputText))
                            return;
                        ChangeSignalType(gameObject.AddComponent<PLCOutputText>());
                    }
                }
                
                [rvInspectorButton(ButtonLabel = "To Transform")] public void ToTransform()
                {
                    if (IsInput())
                    {
                        if (GetType() == typeof(PLCInputTransform))
                            return;
                        ChangeSignalType(gameObject.AddComponent<PLCInputTransform>());
                    }
                    else
                    {
                        if (GetType() == typeof(PLCOutputTransform))
                            return;
                        ChangeSignalType(gameObject.AddComponent<PLCOutputTransform>());
                    }
                }

                [rvInspectorButton(ButtonLabel = "Change Direction")]
                public void ChangeDirection()
                { 
                    var currenttype = GetType();
            
                    switch (currenttype.ToString())
                    {
                        case "realvirtual.PLCInputBool":
                            ChangeSignalType(gameObject.AddComponent<PLCOutputBool>());
                            break;
                        case "realvirtual.PLCOutputBool":
                            ChangeSignalType(gameObject.AddComponent<PLCInputBool>());
                            break;
                        case "realvirtual.PLCInputFloat":
                            ChangeSignalType(gameObject.AddComponent<PLCOutputFloat>());
                            break;
                        case "realvirtual.PLCOutputFloat":
                            ChangeSignalType(gameObject.AddComponent<PLCInputFloat>());
                            break;
                        case "realvirtual.PLCInputInt":
                            ChangeSignalType(gameObject.AddComponent<PLCOutputInt>());
                            break;
                        case "realvirtual.PLCOutputInt":
                            ChangeSignalType(gameObject.AddComponent<PLCInputInt>());
                            break;
                        case "realvirtual.PLCInputText":
                            ChangeSignalType(gameObject.AddComponent<PLCOutputText>());
                            break;
                        case "realvirtual.PLCOutputText":
                            ChangeSignalType(gameObject.AddComponent<PLCInputText>());
                            break;
                        case "realvirtual.PLCInputTransform":
                            ChangeSignalType(gameObject.AddComponent<PLCOutputTransform>());
                            break;
                        case "realvirtual.PLCOutputTransform":
                            ChangeSignalType(gameObject.AddComponent<PLCInputTransform>());
                            break;
                    }
                }
        
                [rvInspectorButton(ButtonLabel = "Delete")]
                public void DeleteSignal()
                {
                    DestroyImmediate(gameObject);
                    rvUIInspectorWindow.Close();
                    PlannerSignalBrowser.Open();
                }
        #endif
                public void OnInspectValueChanged()
                {
        #if REALVIRTUAL_PLANNER
                    gameObject.name = _name;
                    base.Name = _name;
                    PlannerSignalBrowser.Open();
                    rvUIInspectorWindow.Open(gameObject);
        #endif
                }

        public bool OnObjectDrop(Object reference)
        {
            return true;
        }

        public void OnInspectedToggleChanged(bool arg0)
        {
        }

        public event OnSignalChangedDelegate SignalChanged;
        protected void SignalChangedEvent(Signal signal)
        {
            if (SignalChanged != null)
                SignalChanged(signal);
        }
        
        protected new bool hidename()
        {
            return false;
        }

        public string GetSignalName()
        {
            // Name property is the signal ID, if empty use GameObject name
            if (string.IsNullOrEmpty(Name))
                return gameObject != null ? gameObject.name : "UnnamedSignal";
            return Name;
        }

        //!  Virtual for getting the text for the Hierarchy View
        public virtual string GetVisuText()
        {
            return "not implemented";
        }

        public virtual byte[] GetByteValue()
        {
            return null;
        }

        public virtual int GetByteSize()
        {
            return -1;
        }


        //! Virtual for getting information if the signal is an Input
        public virtual bool IsInput()
        {
            return false;
        }

        //! Virtual for setting the value
        public virtual void SetValue(string value)
        {
        }

        public virtual void SetValue(byte[] value)
        {
        }


        //! Virtual for toogle in hierarhy view
        public virtual void OnToggleHierarchy()
        {
        }

        //! Virtual for setting the Status to connected
        public virtual void SetStatusConnected(bool status)
        {
        }

        //! Sets the value of the signal
        public virtual void SetValue(object value)
        {
        }
        //! Unforces the signal
        public void Unforce()
        {
            Settings.Override = false;
            EventSignalChanged.Invoke(this);
            SignalChangedEvent(this);
        }

        //! Gets the value of the signal
        public virtual object GetValue()
        {
            return null;
        }

        //! Virtual for getting the connected Status
        public virtual bool GetStatusConnected()
        {
            return false;
        }

        public void DeleteSignalConnectionInfos()
        {
            ConnectionInfo.Clear();
        }

        public void AddSignalConnectionInfo(GameObject behavior, string connectionname)
        {
            var element = new Connection();
            element.Behavior = behavior;
            element.ConnectionName = connectionname;

            var item = ConnectionInfo.FirstOrDefault(o => o.Behavior == behavior);
            if (item == null)
                ConnectionInfo.Add(element);
            if (IsInput())
                if (ConnectionInfo.Count > 1)
                {
                    //   Error("PLCInput Signal is connected to more than one behavior model, this is not allowed", this);
                }
        }

        //! Returns true if InterfaceSignal is connected to any Behavior Script
        public bool IsConnectedToBehavior()
        {
            if (ConnectionInfo.Count > 0)
                return true;
            return false;
        }

        //! Returns the type of the Signal as a String
        public string GetTypeString()
        {
            // returns a string with the type of the signal
            var type = GetType().ToString();
            switch (type)
            {
                case "realvirtual.PLCInputText":
                    return "TEXT";
                case "realvirtual.PLCInputBool":
                    return "BOOL";
                case "realvirtual.PLCOutputBool":
                    return "BOOL";
                case "realvirtual.PLCInputFloat":
                    return "FLOAT";
                case "realvirtual.PLCOutputFloat":
                    return "FLOAT";
                case "realvirtual.PLCInputInt":
                    return "INT";
                case "realvirtual.PLCOutputInt":
                    return "INT";
                case "realvirtual.PLCOutputText":
                    return "TEXT";
                case "realvirtual.PLCInputTransform":
                    return "TRANSFORM";
                case "realvirtual.PLCOutputTransform":
                    return "TRANSFORM";
            }

            return "";
        }

        //! Returns an InterfaceSignal Object based on the Signal Component
        public InterfaceSignal GetInterfaceSignal()
        {
            var newsignal = new InterfaceSignal();
            newsignal.OriginDataType = OriginDataType;
            newsignal.Name = name;
            newsignal.SymbolName = Name;
            newsignal.Signal = this;
            var type = GetType().ToString();
            switch (type)
            {
                case "realvirtual.PLCInputText":
                    newsignal.Type = InterfaceSignal.TYPE.TEXT;
                    newsignal.Direction = InterfaceSignal.DIRECTION.INPUT;
                    break;
                case "realvirtual.PLCInputBool":
                    newsignal.Type = InterfaceSignal.TYPE.BOOL;
                    newsignal.Direction = InterfaceSignal.DIRECTION.INPUT;
                    break;
                case "realvirtual.PLCOutputBool":
                    newsignal.Type = InterfaceSignal.TYPE.BOOL;
                    newsignal.Direction = InterfaceSignal.DIRECTION.OUTPUT;
                    break;
                case "realvirtual.PLCInputFloat":
                    newsignal.Type = InterfaceSignal.TYPE.REAL;
                    newsignal.Direction = InterfaceSignal.DIRECTION.INPUT;
                    break;
                case "realvirtual.PLCOutputFloat":
                    newsignal.Type = InterfaceSignal.TYPE.REAL;
                    newsignal.Direction = InterfaceSignal.DIRECTION.OUTPUT;
                    break;
                case "realvirtual.PLCInputInt":
                    newsignal.Type = InterfaceSignal.TYPE.INT;
                    newsignal.Direction = InterfaceSignal.DIRECTION.INPUT;
                    break;
                case "realvirtual.PLCOutputInt":
                    newsignal.Type = InterfaceSignal.TYPE.INT;
                    newsignal.Direction = InterfaceSignal.DIRECTION.OUTPUT;
                    break;
                case "realvirtual.PLCOutputText":
                    newsignal.Type = InterfaceSignal.TYPE.TEXT;
                    newsignal.Direction = InterfaceSignal.DIRECTION.OUTPUT;
                    break;
                case "realvirtual.PLCInputTransform":
                    newsignal.Type = InterfaceSignal.TYPE.TRANSFORM;
                    newsignal.Direction = InterfaceSignal.DIRECTION.INPUT;
                    break;
                case "realvirtual.PLCOutputTransform":
                    newsignal.Type = InterfaceSignal.TYPE.TRANSFORM;
                    newsignal.Direction = InterfaceSignal.DIRECTION.OUTPUT;
                    break;
            }

            return newsignal;
        }
        
        #region Metadata API
        
        //! Set metadata value for this signal
        public void SetMetadata(string key, object value)
        {
            if (Metadata == null)
                Metadata = new SignalMetadata();
            Metadata.Set(key, value);
        }
        
        //! Get metadata value with automatic type conversion
        public T GetMetadata<T>(string key, T defaultValue = default(T))
        {
            if (Metadata == null)
                return defaultValue;
            return Metadata.Get(key, defaultValue);
        }
        
        //! Check if metadata key exists for this signal
        public bool HasMetadata(string key)
        {
            if (Metadata == null)
                return false;
            return Metadata.Has(key);
        }
        
        //! Remove metadata key from this signal
        public void RemoveMetadata(string key)
        {
            if (Metadata == null)
                return;
            Metadata.Remove(key);
        }
        
        //! Get all metadata keys for this signal
        public IEnumerable<string> GetMetadataKeys()
        {
            if (Metadata == null)
                return new string[0];
            return Metadata.GetKeys();
        }
        
        #endregion
    }
}