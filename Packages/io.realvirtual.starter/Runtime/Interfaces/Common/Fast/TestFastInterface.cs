// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

namespace realvirtual
{
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/custom-interfaces")]
    public class TestFastInterface : FastInterfaceBase
    {
        [Header("Test Settings")]
        public bool SimulateError = false; //!< Simulate communication error for testing
        public int ErrorAfterCycles = 50; //!< Trigger error after this many cycles
        
        private int errorCycleCounter = 0;
        
        protected override async Task CommunicationLoop(CancellationToken cancellationToken)
        {
            // Simulate some communication work
            await Task.Delay(1, cancellationToken); // Simulate 1ms communication time
            
            // Get inputs from Unity signals (thread-safe)
            var inputs = GetInputsForPLC();
            
            // Process test signals
            if (inputs.ContainsKey("TestInput"))
            {
                // Toggle value every 50 cycles
                var newValue = (CycleCount % 100) < 50;
                SetOutputFromPLC("TestInput", newValue);
            }
            
            // Set test output
            SetOutputFromPLC("TestOutput", CycleCount);
            
            // Test error simulation
            if (SimulateError && ++errorCycleCounter >= ErrorAfterCycles)
            {
                errorCycleCounter = 0;
                throw new System.Exception("Simulated communication error for testing");
            }
        }
        
        protected override void CloseConnection()
        {
            if (threadSafeDebugMode)
                ThreadSafeLogger.LogInfo($"Test interface communication thread closed after {CycleCount} cycles", GetType().Name);
        }
    }
}