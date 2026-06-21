// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using NaughtyAttributes;

namespace realvirtual
{
    #region doc
    //! Generates test signal values for the PLCInput signal on this GameObject.

    //! Place this component on a signal GameObject (PLCInputBool, PLCInputFloat, PLCInputInt)
    //! to generate cyclic test patterns without a PLC connection.
    //! Float signals follow a sine wave, Bool signals toggle on/off, and Int signals increment.
    //!
    //! For detailed documentation see: https://doc.realvirtual.io/components-and-scripts/interfaces/signal-test-generator
    #endregion
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/interfaces/signal-test-generator")]
    [AddComponentMenu("realvirtual/Interfaces/Signal Test Generator")]
    public class SignalTestGenerator : BehaviorInterface
    {
        [Foldout("Test Settings")]
        public bool EnableTesting = true; //!< Enable or disable test signal generation

        [Foldout("Test Settings")]
        public float UpdateInterval = 0.5f; //!< Time in seconds between signal updates

        [Foldout("Test Settings")]
        [Header("Float Settings")]
        public float SineAmplitude = 100f; //!< Amplitude of the sine wave for float signals in signal units

        [Foldout("Test Settings")]
        public float SineOffset = 100f; //!< Center offset of the sine wave for float signals

        [Foldout("Test Settings")]
        public float SineFrequency = 0.5f; //!< Frequency of the sine wave in Hz

        [Foldout("Test Settings")]
        [Header("Bool Settings")]
        public float ToggleInterval = 2.0f; //!< Time in seconds between bool toggles

        [Foldout("Test Settings")]
        [Header("Int Settings")]
        public int IntIncrement = 1; //!< Increment value per update cycle for int signals

        [Foldout("Test Settings")]
        public int IntMax = 100; //!< Maximum value before int counter resets to zero

        [Foldout("Status")]
        [ReadOnly]
        public int CycleCount = 0; //!< Number of update cycles completed

        [Foldout("Status")]
        [ReadOnly]
        public string DetectedSignalType = "None"; //!< Type of signal detected on this GameObject

        // Cached signal references (only one will be non-null)
        private PLCInputBool boolSignal;
        private PLCInputFloat floatSignal;
        private PLCInputInt intSignal;

        private float updateTimer;
        private float toggleTimer;
        private bool toggleState;
        private int intCounter;
        private bool initialized;

        private void Start()
        {
            RefreshSignal();
        }

        //! Discovers the PLCInput signal on this GameObject.
        [Button("Refresh Signal")]
        public void RefreshSignal()
        {
            boolSignal = GetComponent<PLCInputBool>();
            floatSignal = GetComponent<PLCInputFloat>();
            intSignal = GetComponent<PLCInputInt>();
            initialized = true;

            if (boolSignal != null)
                DetectedSignalType = "PLCInputBool";
            else if (floatSignal != null)
                DetectedSignalType = "PLCInputFloat";
            else if (intSignal != null)
                DetectedSignalType = "PLCInputInt";
            else
                DetectedSignalType = "None";

            if (DetectedSignalType == "None")
                Logger.Warning("No PLCInput signal found on this GameObject", this);
            else
                Logger.Message($"Found {DetectedSignalType} signal", this);
        }

        private void FixedUpdate()
        {
            if (!EnableTesting || !initialized)
                return;

            updateTimer += Time.deltaTime;
            toggleTimer += Time.deltaTime;

            if (updateTimer >= UpdateInterval)
            {
                updateTimer = 0f;
                CycleCount++;

                UpdateFloatSignal();
                UpdateIntSignal();
            }

            if (toggleTimer >= ToggleInterval)
            {
                toggleTimer = 0f;
                toggleState = !toggleState;
                UpdateBoolSignal();
            }
        }

        private void UpdateFloatSignal()
        {
            if (floatSignal == null) return;
            float value = SineOffset + SineAmplitude * Mathf.Sin(2f * Mathf.PI * SineFrequency * Time.time);
            floatSignal.Value = value;
        }

        private void UpdateBoolSignal()
        {
            if (boolSignal == null) return;
            boolSignal.Value = toggleState;
        }

        private void UpdateIntSignal()
        {
            if (intSignal == null) return;
            intCounter += IntIncrement;
            if (intCounter > IntMax)
                intCounter = 0;
            intSignal.Value = intCounter;
        }
    }
}
