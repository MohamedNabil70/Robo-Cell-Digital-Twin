// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using System;
using System.Linq;
using NaughtyAttributes;

namespace realvirtual
{
    /// <summary>
    /// Generic signal testing component for interface simulation
    /// Generates dynamic input signal changes for testing interface behavior
    /// Works with any interface that has compatible signal names
    /// </summary>
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/custom-interfaces")]
    public class SignalTester : MonoBehaviour
    {
        #region Configuration
        
        [BoxGroup("Test Configuration")] public bool EnableTesting = true; //!< Enable automatic test data generation
        [BoxGroup("Test Configuration")] public float TestDataUpdateRate = 1.0f; //!< Rate of test data updates in seconds
        [BoxGroup("Test Configuration")] public bool DebugMode = false; //!< Enable debug logging for signal changes
        
        #endregion
        
        #region Status
        
        [BoxGroup("Status"), ReadOnly] public int TestCycleCount = 0; //!< Number of test cycles completed
        [BoxGroup("Status"), ReadOnly] public string CurrentTestPhase = "Stopped"; //!< Current phase of the test cycle
        
        #endregion
        
        #region Private Fields
        
        private InterfaceBaseClass targetInterface;
        private int testCounter = 0;
        private float testTimer = 0f;
        private bool isInitialized = false;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Start()
        {
            InitializeTester();
        }
        
        private void Update()
        {
            if (!EnableTesting || !isInitialized)
                return;
                
            GenerateRealisticTestData();
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeTester()
        {
            // Find any interface component
            targetInterface = GetComponentInParent<InterfaceBaseClass>();
            if (targetInterface == null)
                targetInterface = FindFirstObjectByType<InterfaceBaseClass>();
                
            if (targetInterface == null)
            {
                Debug.LogError("SignalTester: No InterfaceBaseClass found! Please ensure this component is near an interface component.");
                enabled = false;
                return;
            }
            
            isInitialized = true;
            CurrentTestPhase = "Initialized";
            
            Debug.Log($"SignalTester: Initialized and connected to {targetInterface.name}");
        }
        
        #endregion
        
        #region Test Data Generation
        
        private void GenerateRealisticTestData()
        {
            // Use time-based updates since this runs on main thread
            testTimer += Time.deltaTime;
            
            if (testTimer >= TestDataUpdateRate)
            {
                // Generate realistic changing input signal data that matches the actual signal objects
                // Simulate a cyclic manufacturing process
                
                GenerateMotorControlSignals();
                GenerateProcessControlSignals();
                GenerateSafetySignals();
                
                // Update status
                TestCycleCount = testCounter;
                UpdateTestPhase();
                
                
                testCounter++;
                testTimer = 0f;
            }
        }
        
        private void GenerateMotorControlSignals()
        {
            // Motor control cycle: Start → Run → Stop → Pause
            int motorCycle = testCounter % 20;
            bool motorStart = motorCycle == 0; // Start command at beginning of cycle
            bool motorStop = motorCycle == 15;  // Stop command near end of cycle
            
            // Set motor control signals through the interface
            SetInputSignal("Motor1_Start", motorStart);
            SetInputSignal("Motor1_Stop", motorStop);
            
        }
        
        private void GenerateProcessControlSignals()
        {
            // Varying speed setpoint (realistic industrial pattern)
            float speedSetpoint = 50.0f + 30.0f * Mathf.Sin(testCounter * 0.1f);
            SetInputSignal("Speed_Setpoint", speedSetpoint);
            
            // Varying position target (back and forth movement)
            float positionTarget = 100.0f + 50.0f * Mathf.Cos(testCounter * 0.05f);
            SetInputSignal("Position_Target", positionTarget);
            
            // Production mode cycles through different modes
            int productionMode = testCounter % 3;
            SetInputSignal("Production_Mode", productionMode);
            
            // Recipe name changes periodically
            string[] recipes = { "Recipe_A", "Recipe_B", "Recipe_C", "Recipe_D" };
            string recipeName = recipes[testCounter % recipes.Length];
            SetInputSignal("Recipe_Name", recipeName);
        }
        
        private void GenerateSafetySignals()
        {
            // Emergency stop briefly every 100 cycles to test safety response
            bool emergencyStop = (testCounter % 100) == 50;
            SetInputSignal("Emergency_Stop", emergencyStop);
            
        }
        
        private void UpdateTestPhase()
        {
            int phase = (testCounter % 20);
            CurrentTestPhase = phase switch
            {
                0 => "Motor Starting",
                < 10 => "Motor Running",
                15 => "Motor Stopping", 
                > 15 => "Motor Idle",
                _ => "Processing"
            };
            
            // Add special phases
            if ((testCounter % 100) == 50)
                CurrentTestPhase = "Emergency Test";
            else if ((testCounter % 3) == 0)
                CurrentTestPhase += " - Recipe Change";
        }
        
        #endregion
        
        #region Helper Methods
        
        private void SetInputSignal(string signalName, object value)
        {
            if (targetInterface == null) return;
            
            try
            {
                // Find signal by name using standard Unity hierarchy search
                var signal = targetInterface.GetComponentsInChildren<Signal>()
                    .FirstOrDefault(s => s.name == signalName || s.Name == signalName);
                    
                if (signal != null)
                {
                    // Get the old value for comparison
                    var oldValue = signal.GetValue();
                    
                    // Set the signal value using the standard Signal API
                    signal.SetValue(value);
                    
                    // Verify the value was actually set
                    var newValue = signal.GetValue();
                    
                }
            }
            catch (Exception ex)
            {
                if (DebugMode)
                    Debug.LogWarning($"SignalTester: Could not set signal '{signalName}' to '{value}': {ex.Message}");
            }
        }
        
        private bool AreValuesEqual(object value1, object value2)
        {
            if (value1 == null && value2 == null) return true;
            if (value1 == null || value2 == null) return false;
            return value1.Equals(value2);
        }
        
        #endregion
        
        #region Unity Editor Tools
        
        [Button("Create Test Signal Objects", EButtonEnableMode.Editor)]
        private void CreateTestSignalObjects()
        {
            #if UNITY_EDITOR
            CreateSignalGameObjects();
            #endif
        }
        
        [Button("Delete Test Signal Objects", EButtonEnableMode.Editor)]
        private void DeleteTestSignalObjects()
        {
            #if UNITY_EDITOR
            DeleteSignalGameObjects();
            #endif
        }
        
        [Button("Reset Test Counter", EButtonEnableMode.Always)]
        private void ResetTestCounter()
        {
            testCounter = 0;
            TestCycleCount = 0;
            testTimer = 0f;
            CurrentTestPhase = "Reset";
            
            Debug.Log("SignalTester: Test counter reset");
        }
        
        [Button("Generate Single Test Cycle", EButtonEnableMode.Always)]
        private void GenerateSingleTestCycle()
        {
            if (!isInitialized)
                InitializeTester();
                
            if (isInitialized)
            {
                GenerateRealisticTestData();
                Debug.Log($"SignalTester: Generated single test cycle #{testCounter}");
            }
        }
        
        
        #if UNITY_EDITOR
        private void CreateSignalGameObjects()
        {
            if (targetInterface == null)
                InitializeTester();
                
            if (targetInterface == null)
            {
                Debug.LogError("SignalTester: No interface found to create signals for!");
                return;
            }
            
            // Create input signals
            CreateSignalGameObject("Motor1_Start", typeof(PLCInputBool));
            CreateSignalGameObject("Motor1_Stop", typeof(PLCInputBool));
            CreateSignalGameObject("Speed_Setpoint", typeof(PLCInputFloat));
            CreateSignalGameObject("Position_Target", typeof(PLCInputFloat));
            CreateSignalGameObject("Production_Mode", typeof(PLCInputInt));
            CreateSignalGameObject("Recipe_Name", typeof(PLCInputText));
            CreateSignalGameObject("Emergency_Stop", typeof(PLCInputBool));
            
            // Create output signals
            CreateSignalGameObject("Motor1_Running", typeof(PLCOutputBool));
            CreateSignalGameObject("Motor1_Error", typeof(PLCOutputBool));
            CreateSignalGameObject("Speed_Actual", typeof(PLCOutputFloat));
            CreateSignalGameObject("Position_Current", typeof(PLCOutputFloat));
            CreateSignalGameObject("Product_Count", typeof(PLCOutputInt));
            CreateSignalGameObject("System_Status", typeof(PLCOutputText));
            CreateSignalGameObject("System_Ready", typeof(PLCOutputBool));
            
            UnityEditor.EditorUtility.SetDirty(targetInterface);
            Debug.Log($"SignalTester: Created test signal objects for interface: {targetInterface.name}");
        }
        
        private void DeleteSignalGameObjects()
        {
            if (targetInterface == null)
                InitializeTester();
                
            if (targetInterface == null)
            {
                Debug.LogError("SignalTester: No interface found to delete signals from!");
                return;
            }
            
            // List of test signal names to delete
            string[] testSignalNames = {
                "Motor1_Start", "Motor1_Stop", "Speed_Setpoint", "Position_Target", 
                "Production_Mode", "Recipe_Name", "Emergency_Stop",
                "Motor1_Running", "Motor1_Error", "Speed_Actual", "Position_Current",
                "Product_Count", "System_Status", "System_Ready"
            };
            
            int deletedCount = 0;
            foreach (string signalName in testSignalNames)
            {
                Transform signalTransform = targetInterface.transform.Find(signalName);
                if (signalTransform != null)
                {
                    UnityEditor.Undo.DestroyObjectImmediate(signalTransform.gameObject);
                    deletedCount++;
                }
            }
            
            UnityEditor.EditorUtility.SetDirty(targetInterface);
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(targetInterface.gameObject.scene);
                
            Debug.Log($"SignalTester: Deleted {deletedCount} test signal objects from interface: {targetInterface.name}");
        }
        
        private void CreateSignalGameObject(string signalName, System.Type signalType)
        {
            // Check if signal already exists
            Transform existingSignal = targetInterface.transform.Find(signalName);
            if (existingSignal != null)
            {
                Debug.LogWarning($"SignalTester: Signal '{signalName}' already exists, skipping creation");
                return;
            }
            
            // Create new GameObject for the signal using Undo system for proper editor integration
            GameObject signalObj = new GameObject(signalName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(signalObj, $"Create signal {signalName}");
            
            // Set parent using Undo system
            UnityEditor.Undo.SetTransformParent(signalObj.transform, targetInterface.transform, $"Parent signal {signalName}");
            
            // Add the appropriate signal component
            var signalComponent = UnityEditor.Undo.AddComponent(signalObj, signalType) as Signal;
            if (signalComponent != null)
            {
                UnityEditor.Undo.RecordObject(signalComponent, $"Configure signal {signalName}");
                signalComponent.Name = signalName;
                
                // Set initial values based on signal type
                SetInitialSignalValues(signalComponent);
            }
            
            // Mark the scene as dirty
            UnityEditor.EditorUtility.SetDirty(targetInterface);
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(targetInterface.gameObject.scene);
        }
        
        private void SetInitialSignalValues(Signal signalComponent)
        {
            switch (signalComponent)
            {
                case PLCInputBool inputBool:
                    inputBool.Value = false;
                    break;
                case PLCOutputBool outputBool:
                    outputBool.Value = false;
                    break;
                case PLCInputFloat inputFloat:
                    inputFloat.Value = 0.0f;
                    break;
                case PLCOutputFloat outputFloat:
                    outputFloat.Value = 0.0f;
                    break;
                case PLCInputInt inputInt:
                    inputInt.Value = 0;
                    break;
                case PLCOutputInt outputInt:
                    outputInt.Value = 0;
                    break;
                case PLCInputText inputText:
                    inputText.Value = "";
                    break;
                case PLCOutputText outputText:
                    outputText.Value = "";
                    break;
            }
        }
        #endif
        
        #endregion
    }
}