// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace realvirtual
{
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/custom-interfaces")]
    public class PerformanceTestInterface : FastInterfaceBase
    {
        [Header("Performance Test Settings")]
        public bool RunPerformanceTests = true;
        public int TestIterations = 1000;
        public bool ShowDetailedResults = true;
        
        private Stopwatch _stopwatch = new Stopwatch();
        private Dictionary<string, object> _testData = new Dictionary<string, object>();
        
        protected override void OnCommunicationStarted()
        {
            base.OnCommunicationStarted(); // Automatically initializes high-performance mode
            
            // Create test signals
            CreateTestSignals();
            
            // Run performance comparison
            if (RunPerformanceTests)
            {
                RunPerformanceComparison();
            }
        }
        
        private void CreateTestSignals()
        {
            // Create a variety of signals for testing
            for (int i = 0; i < 20; i++)
            {
                this.CreateSignalIfNotExists($"TestBool{i}", SignalType.Bool, SignalDirection.Input);
                this.CreateSignalIfNotExists($"TestInt{i}", SignalType.Int, SignalDirection.Output);
                this.CreateSignalIfNotExists($"TestFloat{i}", SignalType.Float, SignalDirection.Input);
            }
            
            // Prepare test data
            for (int i = 0; i < 20; i++)
            {
                _testData[$"TestBool{i}"] = i % 2 == 0;
                _testData[$"TestInt{i}"] = i * 100;
                _testData[$"TestFloat{i}"] = i * 3.14f;
            }
        }
        
        private void RunPerformanceComparison()
        {
            if (realvirtualController?.DebugMode != true) return;
            
            UnityEngine.Debug.Log($"=== Performance Test Results ({TestIterations} iterations) ===");
            
            // Test 1: Single signal value reading
            TestSingleValueReading();
            
            // Test 2: Single signal value writing  
            TestSingleValueWriting();
            
            // Test 3: Signal lookup performance
            TestSignalLookup();
            
            // Test 4: Batch operations
            TestBatchOperations();
            
            // Test 5: All values reading
            TestAllValuesReading();
        }
        
        private void TestSingleValueReading()
        {
            // Temporarily disable high-performance mode for comparison
            this.ClearSignalManager();
            
            // Traditional method (without high-performance manager)
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations; i++)
            {
                var signal1 = GetSignal("TestBool0")?.GetComponent<PLCInputBool>();
                bool val1 = signal1?.Value ?? false;
                var signal2 = GetSignal("TestInt5")?.GetComponent<PLCOutputInt>();
                int val2 = signal2?.Value ?? 0;
                var signal3 = GetSignal("TestFloat10")?.GetComponent<PLCInputFloat>();
                float val3 = signal3?.Value ?? 0f;
            }
            _stopwatch.Stop();
            long traditionalTime = _stopwatch.ElapsedMilliseconds;
            
            // Re-enable high-performance mode
            this.RefreshSignalManager();
            
            // High-performance method
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations; i++)
            {
                bool val1 = this.GetSignalValue<bool>("TestBool0");
                int val2 = this.GetSignalValue<int>("TestInt5");
                float val3 = this.GetSignalValue<float>("TestFloat10");
            }
            _stopwatch.Stop();
            long fastTime = _stopwatch.ElapsedMilliseconds;
            
            float improvement = traditionalTime / (float)Mathf.Max(1, fastTime);
            UnityEngine.Debug.Log($"Single Value Reading: Traditional={traditionalTime}ms, High-Performance={fastTime}ms, Improvement={improvement:F1}x");
        }
        
        private void TestSingleValueWriting()
        {
            // Temporarily disable high-performance mode for comparison
            this.ClearSignalManager();
            
            // Traditional method (without high-performance manager)
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations; i++)
            {
                var signal1 = GetSignal("TestInt0")?.GetComponent<PLCOutputInt>();
                if (signal1 != null) signal1.Value = i;
                var signal2 = GetSignal("TestFloat0")?.GetComponent<PLCInputFloat>();
                if (signal2 != null) signal2.Value = i * 1.5f;
                var signal3 = GetSignal("TestBool0")?.GetComponent<PLCInputBool>();
                if (signal3 != null) signal3.Value = i % 2 == 0;
            }
            _stopwatch.Stop();
            long traditionalTime = _stopwatch.ElapsedMilliseconds;
            
            // Re-enable high-performance mode
            this.RefreshSignalManager();
            
            // High-performance method
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations; i++)
            {
                this.SetSignalValue("TestInt0", i);
                this.SetSignalValue("TestFloat0", i * 1.5f);
                this.SetSignalValue("TestBool0", i % 2 == 0);
            }
            _stopwatch.Stop();
            long fastTime = _stopwatch.ElapsedMilliseconds;
            
            float improvement = traditionalTime / (float)Mathf.Max(1, fastTime);
            UnityEngine.Debug.Log($"Single Value Writing: Traditional={traditionalTime}ms, High-Performance={fastTime}ms, Improvement={improvement:F1}x");
        }
        
        private void TestSignalLookup()
        {
            // Clear high-performance mode to test original GetSignal performance
            this.ClearSignalManager();
            
            // Traditional method (slow GetSignal from InterfaceBaseClass)
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations; i++)
            {
                var signal1 = GetSignal("TestBool5"); // Uses slow GetComponentsInChildren approach
                var signal2 = GetSignal("TestInt10");
                var signal3 = GetSignal("TestFloat15");
            }
            _stopwatch.Stop();
            long traditionalTime = _stopwatch.ElapsedMilliseconds;
            
            // Re-enable high-performance mode
            this.RefreshSignalManager();
            
            // High-performance method
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations; i++)
            {
                var signal1 = this.GetSignal("TestBool5"); // Uses fast O(1) lookup
                var signal2 = this.GetSignal("TestInt10");
                var signal3 = this.GetSignal("TestFloat15");
            }
            _stopwatch.Stop();
            long fastTime = _stopwatch.ElapsedMilliseconds;
            
            float improvement = traditionalTime / (float)Mathf.Max(1, fastTime);
            UnityEngine.Debug.Log($"Signal Lookup: Traditional={traditionalTime}ms, High-Performance={fastTime}ms, Improvement={improvement:F1}x");
        }
        
        private void TestBatchOperations()
        {
            // Clear high-performance mode for traditional approach
            this.ClearSignalManager();
            
            // Traditional method (individual signal lookups and updates)
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations / 10; i++) // Fewer iterations for batch test
            {
                foreach (var kvp in _testData)
                {
                    var signal = GetSignal(kvp.Key); // Slow lookup
                    var signalComponent = signal?.GetComponent<Signal>();
                    signalComponent?.SetValue(kvp.Value);
                }
            }
            _stopwatch.Stop();
            long traditionalTime = _stopwatch.ElapsedMilliseconds;
            
            // Re-enable high-performance mode
            this.RefreshSignalManager();
            
            // High-performance method
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations / 10; i++)
            {
                this.SetMultipleSignalValues(_testData); // Fast batch update
            }
            _stopwatch.Stop();
            long fastTime = _stopwatch.ElapsedMilliseconds;
            
            float improvement = traditionalTime / (float)Mathf.Max(1, fastTime);
            UnityEngine.Debug.Log($"Batch Updates: Traditional={traditionalTime}ms, High-Performance={fastTime}ms, Improvement={improvement:F1}x");
        }
        
        private void TestAllValuesReading()
        {
            // Clear high-performance mode for traditional approach
            this.ClearSignalManager();
            
            // Traditional method (individual signal queries)
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations / 10; i++)
            {
                var result = new Dictionary<string, object>();
                // Simulate slow traditional approach by reading signals individually
                for (int j = 0; j < 20; j++)
                {
                    var signal = GetSignal($"TestBool{j}"); // Slow lookup
                    if (signal != null)
                    {
                        var signalComponent = signal.GetComponent<Signal>();
                        if (signalComponent != null)
                            result[$"TestBool{j}"] = signalComponent.GetValue();
                    }
                }
            }
            _stopwatch.Stop();
            long traditionalTime = _stopwatch.ElapsedMilliseconds;
            
            // Re-enable high-performance mode
            this.RefreshSignalManager();
            
            // High-performance method
            _stopwatch.Restart();
            for (int i = 0; i < TestIterations / 10; i++)
            {
                var values = this.ReadAllInputs(); // Fast batch read
            }
            _stopwatch.Stop();
            long fastTime = _stopwatch.ElapsedMilliseconds;
            
            float improvement = traditionalTime / (float)Mathf.Max(1, fastTime);
            UnityEngine.Debug.Log($"All Values Reading: Traditional={traditionalTime}ms, High-Performance={fastTime}ms, Improvement={improvement:F1}x");
        }
        
        protected override async Task CommunicationLoop(CancellationToken cancellationToken)
        {
            // Example of high-performance communication loop
            if (CycleCount % 1000 == 0 && ShowDetailedResults)
            {
                ThreadSafeLogger.LogInfo($"High-performance communication running at {1000f / UpdateCycleMs} Hz", GetType().Name);
            }
            
            // Get inputs from Unity signals (thread-safe)
            var inputs = GetInputsForPLC();
            
            // Process some test values
            bool emergency = inputs.ContainsKey("TestBool0") && (bool)inputs["TestBool0"];
            int speed = inputs.ContainsKey("TestInt5") ? (int)inputs["TestInt5"] : 0;
            
            var outputs = new Dictionary<string, object>();
            if (emergency)
            {
                outputs["TestInt0"] = 0;
                outputs["TestBool5"] = true;
            }
            else
            {
                outputs["TestInt0"] = speed + 1;
                outputs["TestFloat0"] = speed * 1.5f;
            }
            
            // Batch update every 10 cycles for maximum efficiency
            if (CycleCount % 10 == 0)
            {
                outputs["TestInt10"] = CycleCount;
                outputs["TestFloat10"] = CycleCount * 0.1f;
                outputs["TestBool10"] = CycleCount % 20 < 10;
            }
            
            // Apply output values
            if (outputs.Count > 0)
            {
                SetOutputsFromPLC(outputs);
            }
            
            await Task.CompletedTask;
        }
        
        protected override void CloseConnection()
        {
            // High-performance mode is automatically cleaned up by base class
            
            if (threadSafeDebugMode)
                ThreadSafeLogger.LogInfo($"Performance test interface closed after {CycleCount} cycles", GetType().Name);
        }
    }
}