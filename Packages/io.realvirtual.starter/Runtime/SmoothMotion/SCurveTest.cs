// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEditor;

namespace realvirtual
{


    public class SCurveTest : MonoBehaviour
    {
        public float maxVelocity = 5f;
        public float maxAcceleration = 2f;
        public float jerk = 5f;

        [Space] public MotionState initialState;
        [Space] public MotionState finalState;
        [Space] public SCurve curve;


        public void ComputeSegments()
        {

            curve = SCurve.Generate(initialState, finalState, maxVelocity, maxAcceleration, jerk);

        }
    }
    #if UNITY_EDITOR
    [CustomEditor(typeof(SCurveTest))]
    public class SCurveTestEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SCurveTest myScript = (SCurveTest)target;
            if (GUILayout.Button("Compute Curve"))
            {
                myScript.ComputeSegments();
            }
        }
    }
    #endif

}