// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor;

namespace realvirtual
{
    //! Context class that caches all component and hierarchy analysis for QuickEdit
    public class QuickEditContext
    {
        public GameObject SelectedObject { get; }
        public bool HasSelection => SelectedObject != null;
        public bool IsSingleSelection { get; }
        public bool IsSceneRoot { get; }

        // Component presence flags
        public bool HasSignal { get; }
        public bool HasDrive { get; }
        public bool HasKinematic { get; }
        public bool HasTransportSurface { get; }
        public bool HasLogicStep { get; }
        public bool HasGrip { get; }
        public bool HasFixer { get; }
        public bool HasAxis { get; }
        public bool HasSensor { get; }
        public bool HasTransportGuided { get; }
        public bool HasSimpleJoint { get; }
        
        // Hierarchy context
        public bool IsUnderInterface { get; }
        public bool IsUnderTransportSurface { get; }
        public bool IsUnderSerialContainer { get; }
        
        // Cached components (only frequently used ones)
        public Drive Drive { get; }
        public realvirtualController Controller { get; }
        public Signal Signal { get; }
        
        public QuickEditContext(GameObject obj)
        {
            SelectedObject = obj;
            IsSingleSelection = Selection.objects.Length == 1;
            IsSceneRoot = obj != null && obj.transform.parent == null;

            if (obj != null)
            {
                // Cache component checks
                Drive = SelectedObject.GetComponent<Drive>();
                HasDrive = Drive != null;
                
                Signal = SelectedObject.GetComponent<Signal>();
                HasSignal = Signal != null;
                
                Controller = SelectedObject.GetComponent<realvirtualController>();
                
                HasKinematic = SelectedObject.GetComponent<Kinematic>() != null;
                HasSensor = SelectedObject.GetComponent<Sensor>() != null;
                HasGrip = SelectedObject.GetComponent<Grip>() != null;
                HasFixer = SelectedObject.GetComponent<Fixer>() != null;
                HasAxis = SelectedObject.GetComponent<Axis>() != null;
                HasTransportGuided = SelectedObject.GetComponent<TransportGuided>() != null;
                HasSimpleJoint = SelectedObject.GetComponent<SimpleJoint>() != null;
                
                // Hierarchy checks
                HasTransportSurface = SelectedObject.GetComponentInParent<TransportSurface>() != null;
                IsUnderTransportSurface = HasTransportSurface;
                IsUnderInterface = SelectedObject.GetComponentInParent<InterfaceBaseClass>() != null;

                // LogicStep checks
                HasLogicStep = SelectedObject.GetComponent<LogicStep>() != null;
                IsUnderSerialContainer = SelectedObject.GetComponentInParent<LogicStep_SerialContainer>() != null;
            }
        }
    }
}
#endif