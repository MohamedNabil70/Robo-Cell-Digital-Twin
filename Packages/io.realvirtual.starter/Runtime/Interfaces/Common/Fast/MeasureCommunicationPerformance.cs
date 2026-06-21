// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEngine;

namespace realvirtual
{
    //! Comprehensive communication performance measurement for Unity-PLC interfaces.
    //! Measures update rates, round-trip times, and communication quality metrics.
    public class MeasureCommunicationPerformance : MonoBehaviour
    {
        [Header("Round-Trip Time Measurement")]
        [Tooltip("Sends incrementing counter to PLC")]
        public PLCInputInt RTT_SendCounter; //!< Counter sent to PLC for round-trip measurement
        
        [Tooltip("Receives echoed counter from PLC")]
        public PLCOutputInt RTT_EchoCounter; //!< Counter echoed back from PLC
        
        [Tooltip("Sends timestamp to PLC (ms)")]
        public PLCInputInt RTT_SendTimestamp; //!< Timestamp sent to PLC in milliseconds
        
        [Tooltip("Receives echoed timestamp from PLC")]
        public PLCOutputInt RTT_EchoTimestamp; //!< Timestamp echoed back from PLC
        
        [Header("Update Rate Measurement")]
        [Tooltip("Any PLCInput signal to measure output rate")]
        public PLCInputBool SampleOutputSignal; //!< Sample output signal for rate measurement
        
        [Tooltip("Any PLCOutput signal to measure input rate")]
        public PLCOutputBool SampleInputSignal; //!< Sample input signal for rate measurement
        
        [Header("Optional: PLC Processing Time")]
        [Tooltip("PLC calculation time in microseconds")]
        public PLCOutputInt PLCProcessingTimeUs; //!< Time PLC spent processing (optional)
        
        [Header("Current Measurements")]
        [InfoBox("Round-trip time from Unity→PLC→Unity")]
        [ReadOnly] public float CurrentRTT_ms; //!< Current round-trip time in milliseconds
        
        [InfoBox("Time for Unity→PLC communication")]
        [ReadOnly] public float UnityToPLC_ms; //!< Estimated Unity to PLC time
        
        [InfoBox("Time for PLC→Unity communication")]
        [ReadOnly] public float PLCToUnity_ms; //!< Estimated PLC to Unity time
        
        [InfoBox("Update rate of outputs (Unity→PLC)")]
        [ReadOnly] public float OutputUpdateRate_Hz; //!< Output signal update rate in Hz
        
        [InfoBox("Update rate of inputs (PLC→Unity)")]
        [ReadOnly] public float InputUpdateRate_Hz; //!< Input signal update rate in Hz
        
        [Header("Statistics")]
        [ReadOnly] public float MinRTT_ms = float.MaxValue; //!< Minimum RTT observed
        [ReadOnly] public float AvgRTT_ms; //!< Average RTT over sample window
        [ReadOnly] public float MaxRTT_ms; //!< Maximum RTT observed
        [ReadOnly] public float StdDevRTT_ms; //!< Standard deviation of RTT
        
        [Header("Send/Echo Gap Analysis")]
        [ReadOnly] public int CurrentSendCounter; //!< Current send counter value
        [ReadOnly] public int CurrentEchoCounter; //!< Current echo counter value  
        [ReadOnly] public int SendEchoGap; //!< Gap between sent and echoed counter
        [ReadOnly] public float GapGrowthRate; //!< How fast the gap is growing (counters/sec)
        
        [Header("Update Rate Statistics")]
        [ReadOnly] public float AvgOutputRate_Hz; //!< Average output update rate
        [ReadOnly] public float MinOutputRate_Hz = float.MaxValue; //!< Minimum output update rate
        [ReadOnly] public float MaxOutputRate_Hz; //!< Maximum output update rate
        
        [ReadOnly] public float AvgInputRate_Hz; //!< Average input update rate
        [ReadOnly] public float MinInputRate_Hz = float.MaxValue; //!< Minimum input update rate
        [ReadOnly] public float MaxInputRate_Hz; //!< Maximum input update rate
        
        [Header("Configuration")]
        [Tooltip("Number of samples for statistics calculation")]
        [Range(10, 1000)]
        public int StatisticsSampleSize = 100; //!< Sample size for moving statistics
        
        
        [Tooltip("Enable detailed logging")]
        public bool EnableDiagnostics = false; //!< Enable diagnostic logging
        
        [Tooltip("Log warning if RTT exceeds this value (ms)")]
        [ShowIf("EnableDiagnostics")]
        public float RTTWarningThresholdMs = 100f; //!< RTT warning threshold
        
        // Round-trip tracking
        private Dictionary<int, float> pendingPackets = new Dictionary<int, float>();
        private Queue<float> rttSamples = new Queue<float>();
        private int sendCounter = 0;
        private int lastReceivedCounter = -1;
        
        // Update rate tracking
        private float lastOutputUpdateTime;
        private float lastInputUpdateTime;
        private object lastOutputValue;
        private object lastInputValue;
        private Queue<float> outputRateSamples = new Queue<float>();
        private Queue<float> inputRateSamples = new Queue<float>();
        
        // Statistics
        private float sumRTT;
        private float sumSquaredRTT;
        private float sumOutputRate;
        private float sumInputRate;
        
        // Diagnostics
        private float lastDiagnosticTime;
        private const float DIAGNOSTIC_INTERVAL = 5f;
        
        // Gap tracking
        private float lastGapCheckTime;
        private int lastGapValue;
        
        private void Start()
        {
            // Validate configuration and show capabilities
            ValidateConfiguration();
            ResetMeasurements();
        }
        
        private void ValidateConfiguration()
        {
            bool hasBasicRTT = (RTT_SendCounter != null && RTT_EchoCounter != null);
            bool hasTimestamps = (RTT_SendTimestamp != null && RTT_EchoTimestamp != null);
            bool hasProcessingTime = (PLCProcessingTimeUs != null);
            bool hasOutputSample = (SampleOutputSignal != null);
            bool hasInputSample = (SampleInputSignal != null);
            
            // Log measurement capabilities
            Debug.Log($"[{name}] Communication Measurement Configuration:");
            
            if (hasBasicRTT)
            {
                Debug.Log($"  ✓ Basic RTT measurement (counter echo)");
                if (hasTimestamps)
                    Debug.Log($"  ✓ Enhanced RTT with timestamps");
                else
                    Debug.Log($"  ○ No timestamps - RTT accuracy may be reduced");
                    
                if (hasProcessingTime)
                    Debug.Log($"  ✓ PLC processing time measurement");
                else
                    Debug.Log($"  ○ No PLC processing time - assuming symmetric communication");
            }
            else
            {
                Debug.LogWarning($"  ✗ No RTT measurement - missing counter signals");
            }
            
            if (hasOutputSample)
                Debug.Log($"  ✓ Output rate measurement enabled");
            else
                Debug.Log($"  ○ No output rate measurement");
                
            if (hasInputSample)
                Debug.Log($"  ✓ Input rate measurement enabled");
            else
                Debug.Log($"  ○ No input rate measurement");
                
            if (!hasBasicRTT && !hasOutputSample && !hasInputSample)
            {
                Debug.LogError($"[{name}] No signals configured! Please connect at least one measurement signal.");
            }
        }
        
        private void FixedUpdate()
        {
            float currentTime = Time.time;
            
            // Send RTT packet (only needs SendCounter)
            if (RTT_SendCounter != null)
            {
                SendRTTPacket(currentTime);
            }
            
            // Check for RTT response
            if (RTT_EchoCounter != null)
            {
                CheckRTTResponse(currentTime);
            }
            
            // Measure update rates
            MeasureOutputUpdateRate(currentTime);
            MeasureInputUpdateRate(currentTime);
            
            // Clean up old pending packets to prevent memory leak
            CleanupOldPendingPackets(currentTime);
            
            // Periodic diagnostics
            if (EnableDiagnostics && currentTime - lastDiagnosticTime > DIAGNOSTIC_INTERVAL)
            {
                LogDiagnostics();
                lastDiagnosticTime = currentTime;
            }
        }
        
        private void SendRTTPacket(float currentTime)
        {
            // Send counter (required)
            sendCounter++;
            RTT_SendCounter.Value = sendCounter;
            CurrentSendCounter = sendCounter;
            
            // Send timestamp (optional)
            if (RTT_SendTimestamp != null)
            {
                int timestampMs = (int)(currentTime * 1000);
                RTT_SendTimestamp.Value = timestampMs;
            }
            
            // Track pending packet
            pendingPackets[sendCounter] = currentTime;
            
            // Update gap
            SendEchoGap = CurrentSendCounter - CurrentEchoCounter;
            
            // Calculate gap growth rate
            if (lastGapCheckTime > 0 && currentTime - lastGapCheckTime > 1f)
            {
                float timeDelta = currentTime - lastGapCheckTime;
                int gapDelta = SendEchoGap - lastGapValue;
                GapGrowthRate = gapDelta / timeDelta;
                
                lastGapCheckTime = currentTime;
                lastGapValue = SendEchoGap;
            }
            else if (lastGapCheckTime == 0)
            {
                lastGapCheckTime = currentTime;
                lastGapValue = SendEchoGap;
            }
            
            if (EnableDiagnostics && sendCounter % 100 == 0)
            {
                Debug.Log($"[{name}] Sent packet #{sendCounter}, Gap: {SendEchoGap}");
            }
        }
        
        private void CheckRTTResponse(float currentTime)
        {
            int receivedCounter = RTT_EchoCounter.Value;
            
            // Check if this is a new response
            if (receivedCounter != lastReceivedCounter && receivedCounter > 0)
            {
                // Check for sequence gaps (not packet loss, but sequence jumps)
                if (lastReceivedCounter > 0)
                {
                    int expectedNext = lastReceivedCounter + 1;
                    int gap = receivedCounter - expectedNext;
                    
                    if (gap > 0)
                    {
                        // We skipped some sequence numbers - this is NORMAL
                        // It just means Unity sent a new counter before PLC echoed the previous one
                        if (EnableDiagnostics && gap > 10)
                        {
                            Debug.Log($"[{name}] Sequence jump: {lastReceivedCounter} → {receivedCounter} (gap: {gap})");
                        }
                    }
                    else if (gap < 0 && receivedCounter > 10)
                    {
                        // Out of order or wraparound
                        if (EnableDiagnostics)
                        {
                            Debug.LogWarning($"[{name}] Out-of-order echo: expected >{lastReceivedCounter}, got {receivedCounter}");
                        }
                    }
                }
                
                lastReceivedCounter = receivedCounter;
                CurrentEchoCounter = receivedCounter;
                
                // Check if we were tracking this packet
                if (pendingPackets.TryGetValue(receivedCounter, out float sendTime))
                {
                    // Calculate RTT
                    float deltaTime = currentTime - sendTime;
                    float rtt = deltaTime * 1000f; // Convert to ms
                    CurrentRTT_ms = rtt;
                    
                    if (EnableDiagnostics && rtt > 1000f) // Log if > 1 second
                    {
                        Debug.LogWarning($"[{name}] Very high RTT! Counter: {receivedCounter}, " +
                                       $"SendTime: {sendTime:F3}s, CurrentTime: {currentTime:F3}s, " +
                                       $"Delta: {deltaTime:F3}s, RTT: {rtt:F1}ms");
                    }
                    
                    // If we have PLC processing time, calculate directional times
                    if (PLCProcessingTimeUs != null && PLCProcessingTimeUs.Value > 0)
                    {
                        float plcProcessingMs = PLCProcessingTimeUs.Value / 1000f;
                        float networkTimeMs = rtt - plcProcessingMs;
                        UnityToPLC_ms = networkTimeMs / 2f; // Assume symmetric
                        PLCToUnity_ms = networkTimeMs / 2f;
                    }
                    else
                    {
                        // Assume symmetric communication
                        UnityToPLC_ms = rtt / 2f;
                        PLCToUnity_ms = rtt / 2f;
                    }
                    
                    // Update statistics
                    UpdateRTTStatistics(rtt);
                    
                    // Remove from pending
                    pendingPackets.Remove(receivedCounter);
                    
                    // Check for warnings
                    if (EnableDiagnostics && rtt > RTTWarningThresholdMs)
                    {
                        Debug.LogWarning($"[{name}] High RTT detected: {rtt:F2}ms (threshold: {RTTWarningThresholdMs}ms)");
                    }
                }
                else
                {
                    // We received an echo for a counter we didn't track
                    // This can happen if:
                    // 1. The echo is from before measurement started
                    // 2. The packet was already timed out and removed
                    // 3. It's an out-of-order echo
                    
                    // Only count as "lost" if it was recent and we should have been tracking it
                    if (receivedCounter > sendCounter - 100 && receivedCounter < sendCounter)
                    {
                        // This was likely a timeout that eventually arrived
                        if (EnableDiagnostics)
                        {
                            Debug.LogWarning($"[{name}] Late echo received: {receivedCounter} (current: {sendCounter})");
                        }
                    }
                }
                
                // Check if we have timestamp echo for validation
                if (RTT_EchoTimestamp != null && RTT_SendTimestamp != null)
                {
                    int echoTimestamp = RTT_EchoTimestamp.Value;
                    if (echoTimestamp > 0) // Only validate if PLC is echoing timestamps
                    {
                        int currentTimestampMs = (int)(currentTime * 1000);
                        int timeDiff = currentTimestampMs - echoTimestamp;
                        
                        // Use timestamp difference for more accurate RTT if available
                        if (timeDiff > 0 && timeDiff < 10000) // Sanity check: < 10 seconds
                        {
                            float timestampRTT = (float)timeDiff;
                            // Average with counter-based RTT for robustness
                            CurrentRTT_ms = (CurrentRTT_ms + timestampRTT) / 2f;
                        }
                        
                        if (EnableDiagnostics && Mathf.Abs(timeDiff - CurrentRTT_ms) > 10)
                        {
                            Debug.LogWarning($"[{name}] RTT mismatch: calculated {CurrentRTT_ms:F2}ms, timestamp diff {timeDiff}ms");
                        }
                    }
                }
            }
        }
        
        private void MeasureOutputUpdateRate(float currentTime)
        {
            if (SampleOutputSignal == null) return;
            
            object currentValue = SampleOutputSignal.GetValue();
            
            // Check if value changed
            if (lastOutputValue == null || !lastOutputValue.Equals(currentValue))
            {
                if (lastOutputUpdateTime > 0)
                {
                    float deltaTime = currentTime - lastOutputUpdateTime;
                    float rate = 1f / deltaTime;
                    OutputUpdateRate_Hz = rate;
                    
                    UpdateOutputRateStatistics(rate);
                }
                
                lastOutputUpdateTime = currentTime;
                lastOutputValue = currentValue;
            }
        }
        
        private void MeasureInputUpdateRate(float currentTime)
        {
            if (SampleInputSignal == null) return;
            
            object currentValue = SampleInputSignal.GetValue();
            
            // Check if value changed
            if (lastInputValue == null || !lastInputValue.Equals(currentValue))
            {
                if (lastInputUpdateTime > 0)
                {
                    float deltaTime = currentTime - lastInputUpdateTime;
                    float rate = 1f / deltaTime;
                    InputUpdateRate_Hz = rate;
                    
                    UpdateInputRateStatistics(rate);
                }
                
                lastInputUpdateTime = currentTime;
                lastInputValue = currentValue;
            }
        }
        
        private void UpdateRTTStatistics(float rtt)
        {
            // Add to samples
            rttSamples.Enqueue(rtt);
            sumRTT += rtt;
            sumSquaredRTT += rtt * rtt;
            
            // Remove old samples
            if (rttSamples.Count > StatisticsSampleSize)
            {
                float oldRtt = rttSamples.Dequeue();
                sumRTT -= oldRtt;
                sumSquaredRTT -= oldRtt * oldRtt;
            }
            
            // Update statistics
            if (rttSamples.Count > 0)
            {
                AvgRTT_ms = sumRTT / rttSamples.Count;
                
                // Min/Max
                if (rtt < MinRTT_ms) MinRTT_ms = rtt;
                if (rtt > MaxRTT_ms) MaxRTT_ms = rtt;
                
                // Standard deviation
                if (rttSamples.Count > 1)
                {
                    float variance = (sumSquaredRTT / rttSamples.Count) - (AvgRTT_ms * AvgRTT_ms);
                    StdDevRTT_ms = Mathf.Sqrt(Mathf.Max(0, variance));
                }
            }
            
        }
        
        private void UpdateOutputRateStatistics(float rate)
        {
            outputRateSamples.Enqueue(rate);
            sumOutputRate += rate;
            
            if (outputRateSamples.Count > StatisticsSampleSize)
            {
                sumOutputRate -= outputRateSamples.Dequeue();
            }
            
            if (outputRateSamples.Count > 0)
            {
                AvgOutputRate_Hz = sumOutputRate / outputRateSamples.Count;
                if (rate < MinOutputRate_Hz) MinOutputRate_Hz = rate;
                if (rate > MaxOutputRate_Hz) MaxOutputRate_Hz = rate;
            }
        }
        
        private void UpdateInputRateStatistics(float rate)
        {
            inputRateSamples.Enqueue(rate);
            sumInputRate += rate;
            
            if (inputRateSamples.Count > StatisticsSampleSize)
            {
                sumInputRate -= inputRateSamples.Dequeue();
            }
            
            if (inputRateSamples.Count > 0)
            {
                AvgInputRate_Hz = sumInputRate / inputRateSamples.Count;
                if (rate < MinInputRate_Hz) MinInputRate_Hz = rate;
                if (rate > MaxInputRate_Hz) MaxInputRate_Hz = rate;
            }
        }
        
        private void CleanupOldPendingPackets(float currentTime)
        {
            // Remove packets older than 10 seconds to prevent memory leak
            const float maxPacketAge = 10f;
            
            var oldPackets = pendingPackets
                .Where(kvp => (currentTime - kvp.Value) > maxPacketAge)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (int counter in oldPackets)
            {
                pendingPackets.Remove(counter);
            }
        }
        
        private void LogDiagnostics()
        {
            string diagnostics = $"[{name}] Communication Performance:\n" +
                               $"  RTT: {AvgRTT_ms:F2}ms ± {StdDevRTT_ms:F2}ms (range: {MinRTT_ms:F2}-{MaxRTT_ms:F2}ms)\n" +
                               $"  Output Rate: {AvgOutputRate_Hz:F1}Hz (range: {MinOutputRate_Hz:F1}-{MaxOutputRate_Hz:F1}Hz)\n" +
                               $"  Input Rate: {AvgInputRate_Hz:F1}Hz (range: {MinInputRate_Hz:F1}-{MaxInputRate_Hz:F1}Hz)\n" +
                               $"  Send/Echo Gap: {SendEchoGap} counters\n" +
                               $"  Pending RTT Measurements: {pendingPackets.Count}";
            
            Debug.Log(diagnostics);
        }
        
        [Button("Reset Measurements")]
        private void ResetMeasurements()
        {
            // Clear collections
            pendingPackets.Clear();
            rttSamples.Clear();
            outputRateSamples.Clear();
            inputRateSamples.Clear();
            
            // Reset counters
            sendCounter = 0;
            lastReceivedCounter = -1;
            CurrentSendCounter = 0;
            CurrentEchoCounter = 0;
            SendEchoGap = 0;
            GapGrowthRate = 0;
            
            // Reset statistics
            CurrentRTT_ms = 0;
            UnityToPLC_ms = 0;
            PLCToUnity_ms = 0;
            MinRTT_ms = float.MaxValue;
            MaxRTT_ms = 0;
            AvgRTT_ms = 0;
            StdDevRTT_ms = 0;
            
            // Reset update rates
            OutputUpdateRate_Hz = 0;
            InputUpdateRate_Hz = 0;
            MinOutputRate_Hz = float.MaxValue;
            MaxOutputRate_Hz = 0;
            MinInputRate_Hz = float.MaxValue;
            MaxInputRate_Hz = 0;
            
            // Reset sums
            sumRTT = 0;
            sumSquaredRTT = 0;
            sumOutputRate = 0;
            sumInputRate = 0;
            
            // Reset timing
            lastOutputUpdateTime = 0;
            lastInputUpdateTime = 0;
            lastOutputValue = null;
            lastInputValue = null;
            
            Debug.Log($"[{name}] Measurements reset");
        }
        
        [Button("Export Statistics")]
        private void ExportStatistics()
        {
            string stats = $"Communication Performance Report - {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                          $"\nRound-Trip Time:\n" +
                          $"  Current: {CurrentRTT_ms:F3}ms\n" +
                          $"  Average: {AvgRTT_ms:F3}ms\n" +
                          $"  Std Dev: {StdDevRTT_ms:F3}ms\n" +
                          $"  Min: {MinRTT_ms:F3}ms\n" +
                          $"  Max: {MaxRTT_ms:F3}ms\n" +
                          $"\nDirectional Times:\n" +
                          $"  Unity→PLC: {UnityToPLC_ms:F3}ms\n" +
                          $"  PLC→Unity: {PLCToUnity_ms:F3}ms\n" +
                          $"\nSend/Echo Analysis:\n" +
                          $"  Current Send Counter: {CurrentSendCounter}\n" +
                          $"  Current Echo Counter: {CurrentEchoCounter}\n" +
                          $"  Gap: {SendEchoGap} counters\n" +
                          $"  Gap Growth Rate: {GapGrowthRate:F2} counters/sec\n" +
                          $"\nUpdate Rates:\n" +
                          $"  Output Rate: {AvgOutputRate_Hz:F2}Hz (range: {MinOutputRate_Hz:F2}-{MaxOutputRate_Hz:F2}Hz)\n" +
                          $"  Input Rate: {AvgInputRate_Hz:F2}Hz (range: {MinInputRate_Hz:F2}-{MaxInputRate_Hz:F2}Hz)\n";
            
            Debug.Log(stats);
            GUIUtility.systemCopyBuffer = stats;
            Debug.Log("Statistics copied to clipboard");
        }
    }
}