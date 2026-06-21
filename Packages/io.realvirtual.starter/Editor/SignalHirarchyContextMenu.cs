// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;


namespace realvirtual
{
    [InitializeOnLoad]
    public static class SignalHierarchyContextMenu
    {
        private static void ChangeDirection(GameObject gameobjec)
        {
            var signal = gameobjec.GetComponent<Signal>();
            if (signal == null)
                return;
            var newsignal = signal;

            // Store metadata before changing
            var metadataKeys = signal.GetMetadataKeys().ToList();
            var metadataValues = new Dictionary<string, object>();
            foreach (var key in metadataKeys)
            {
                metadataValues[key] = signal.GetMetadata<object>(key);
            }

            var type = signal.GetType();
            if (signal.IsInput())
            {
                if (type == typeof(PLCInputBool)) newsignal = gameobjec.AddComponent<PLCOutputBool>();

                if (type == typeof(PLCInputInt)) newsignal = gameobjec.AddComponent<PLCOutputInt>();

                if (type == typeof(PLCInputFloat)) newsignal = gameobjec.AddComponent<PLCOutputFloat>();

                if (type == typeof(PLCInputTransform)) newsignal = gameobjec.AddComponent<PLCOutputTransform>();
                
                if (type == typeof(PLCInputText)) newsignal = gameobjec.AddComponent<PLCOutputText>();
            }
            else
            {
                if (type == typeof(PLCOutputBool)) newsignal = gameobjec.AddComponent<PLCInputBool>();

                if (type == typeof(PLCOutputInt)) newsignal = gameobjec.AddComponent<PLCInputInt>();

                if (type == typeof(PLCOutputFloat)) newsignal = gameobjec.AddComponent<PLCInputFloat>();

                if (type == typeof(PLCOutputTransform)) newsignal = gameobjec.AddComponent<PLCInputTransform>();
                
                if (type == typeof(PLCOutputText)) newsignal = gameobjec.AddComponent<PLCInputText>();
            }

            newsignal.Name = signal.Name;
            newsignal.Comment = signal.Comment;
            newsignal.OriginDataType = signal.OriginDataType;
            
            // Ensure metadata is initialized
            if (newsignal.Metadata == null)
            {
                newsignal.Metadata = new SignalMetadata();
            }
            
            // Restore metadata
            foreach (var kvp in metadataValues)
            {
                newsignal.SetMetadata(kvp.Key, kvp.Value);
            }
            
            Object.DestroyImmediate(signal);
        }


        [MenuItem("GameObject/realvirtual/Change Signal Direction", false, 0)]
        public static void HierarchyChangeSignalDirection()
        {
            foreach (var obj in Selection.objects)
            {
                var gameobject = (GameObject)obj;
                ChangeDirection(gameobject);
            }
        }

        [MenuItem("CONTEXT/Component/realvirtual/Change Signal Direction")]
        public static void ComtextChangeSignalDirection(MenuCommand command)
        {
            var gameobject = command.context;
            var obj = (Component)gameobject;
            ChangeDirection(obj.gameObject);
        }
    }
}