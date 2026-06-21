// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using realvirtual;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace realvirtual
{
    //! Controls smooth S-curve motion profiles for precise positioning of automation components with jerk limitation.
    //! This professional motion controller applies advanced trajectory planning to drives and actuators, providing
    //! smooth acceleration and deceleration curves that minimize mechanical stress. Essential for high-speed
    //! pick-and-place operations, precision assembly tasks, and any application requiring smooth, controlled
    //! motion without vibration or overshoot. Supports both linear and angular motion with real-time updates.
    public class SmoothMotionController : MonoBehaviour
    {
        public float targetPosition = 0;
        public float targetVelocity = 0;
        public Vector3 axis = Vector3.up;
        [HideInInspector] public bool angular = false;

        [Space] public SmoothMotion motion = new SmoothMotion();

        private Vector3 initialPosition;
        private Vector3 initialRotation;

        private void Start()
        {
            initialPosition = transform.localPosition;
            initialRotation = transform.localEulerAngles;
            motion.SetInitialPosition(0);
            motion.SetInitialVelocity(0);
        }

        void FixedUpdate()
        {
            motion.Integrate(Time.fixedDeltaTime);

            if (angular)
            {
                float angle = motion.GetPosition();
                transform.localRotation = Quaternion.Euler(initialRotation + axis * angle);
            }
            else
            {
                transform.localPosition = initialPosition + axis * motion.GetPosition();
            }

        }

        public void MoveToTarget()
        {
            motion.SetTarget(targetPosition, targetVelocity);
        }


    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SmoothMotionController))]
    public class SmoothMotionControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            SmoothMotionController motionController = (SmoothMotionController)target;
            if (GUILayout.Button("Move To Target"))
            {
                motionController.MoveToTarget();
            }

            serializedObject.ApplyModifiedProperties();

        }
    }
#endif

}