// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using System;
using NaughtyAttributes;

namespace realvirtual
{
    //! Blueprint interface for manual physics control synchronized with FixedUpdate.
    //! Simulates external systems (e.g., FMU co-simulation, hardware interfaces) by controlling
    //! physics simulation timing. Physics.Simulate() is called in PostFixedUpdate immediately after
    //! all FixedUpdate scripts have run. Useful for testing how realvirtual behaves when physics
    //! stepping needs to be externally controlled. Derived classes implement specific communication
    //! protocols while inheriting the physics stepping control.
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/custom-interfaces")]
    public class ManualPhysicsBlueprint : FastInterfaceBase
    {
        [BoxGroup("Manual Physics Control/Status")]
        [ReadOnly]
        public string ManualPhysicsStatus = "Not Initialized"; //!< Current manual physics control status

        [BoxGroup("Manual Physics Control")]
        [Range(0, 200)]
        public float SpeedPercentage = 100f; //!< Physics simulation speed as percentage (100=real-time, 50=half speed via frame skipping, 25=quarter speed, 0=paused). Simulates external system throttling by skipping physics steps.

        [BoxGroup("Manual Physics Control")]
        public bool EnableManualControl = true; //!< Enable manual physics control on start

        [Button("Enable Manual Mode", EButtonEnableMode.Playmode)]
        private void EnableManualModeButton()
        {
            if (!Application.isPlaying)
                return;

            if (!manualPhysicsInitialized)
            {
                Logger.Message("Runtime: Initializing manual physics control...", this);
                SetManualPhysicsMode(true);
                manualPhysicsInitialized = true;
                ManualPhysicsStatus = $"✓ Enabled (Speed: {SpeedPercentage}%)";
                Logger.Message("Runtime: Manual physics control enabled", this);
            }
            else
            {
                Logger.Warning("Manual physics already initialized", this);
                ManualPhysicsStatus = $"✓ Already Enabled (Speed: {SpeedPercentage}%)";
            }
        }

        [Button("Disable Manual Mode", EButtonEnableMode.Playmode)]
        private void DisableManualModeButton()
        {
            if (!Application.isPlaying)
                return;

            if (manualPhysicsInitialized)
            {
                SetManualPhysicsMode(false);
                manualPhysicsInitialized = false;
                ManualPhysicsStatus = "⚫ Disabled (Automatic Physics)";
                Logger.Message("Runtime: Manual physics control disabled", this);
            }
            else
            {
                Logger.Warning("Manual physics not initialized", this);
                ManualPhysicsStatus = "⚫ Not Initialized";
            }
        }

        [Button("Single Physics Step", EButtonEnableMode.Playmode)]
        private void SingleStepButton()
        {
            if (!Application.isPlaying || !IsManualPhysicsMode())
            {
                Logger.Warning("Manual physics mode must be enabled first", this);
                return;
            }

            StepPhysics(Time.fixedDeltaTime);
            Logger.Message($"Single physics step executed (timestep={Time.fixedDeltaTime:F4}s)", this);
        }

        private bool manualPhysicsInitialized = false;
        private int physicsStepCount = 0;
        private int fixedUpdateFrameCount = 0; // Counts FixedUpdate frames for throttling
        private static bool physicsWillStepThisFrame = false; // Global flag readable by other components

        //! Returns true if physics will be stepped this FixedUpdate frame.
        //! Components can check this in their FixedUpdate to coordinate with manual physics.
        public static bool PhysicsWillStepThisFrame => physicsWillStepThisFrame;

        //! Override PreFixedUpdate to signal whether physics will step this frame
        public override void PreFixedUpdate()
        {
            // Call base implementation first (syncs outputs to Unity)
            base.PreFixedUpdate();

            // Determine if we'll step physics this frame
            if (manualPhysicsInitialized && IsManualPhysicsMode() && SpeedPercentage > 0)
            {
                fixedUpdateFrameCount++;
                int stepInterval = Mathf.Max(1, Mathf.RoundToInt(100f / SpeedPercentage));

                // Set global flag BEFORE FixedUpdate runs
                physicsWillStepThisFrame = (fixedUpdateFrameCount >= stepInterval);
            }
            else
            {
                // Auto physics or paused - let Unity handle it
                physicsWillStepThisFrame = !IsManualPhysicsMode();
            }
        }

        //! Override PrepareForBackgroundThread to initialize manual physics control BEFORE communication starts
        protected override void PrepareForBackgroundThread()
        {
            base.PrepareForBackgroundThread();

            Logger.Message($"=== PrepareForBackgroundThread CALLED === EnableManualControl={EnableManualControl}", this);

            if (EnableManualControl && !manualPhysicsInitialized)
            {
                Logger.Message("Initializing manual physics control...", this);
                SetManualPhysicsMode(true);
                manualPhysicsInitialized = true;
                ManualPhysicsStatus = $"✓ Enabled (Speed: {SpeedPercentage}%)";
                Logger.Message("Manual physics control initialization complete", this);
            }
            else if (!EnableManualControl)
            {
                ManualPhysicsStatus = "⚫ Not Enabled (EnableManualControl=false)";
                Logger.Warning("EnableManualControl is FALSE - manual physics not started", this);
            }
            else
            {
                ManualPhysicsStatus = $"✓ Already Enabled (Speed: {SpeedPercentage}%)";
                Logger.Warning("Manual physics already initialized - skipping duplicate initialization", this);
            }
        }

        //! Override PostFixedUpdate to step physics immediately after all FixedUpdate methods have run
        public override void PostFixedUpdate()
        {
            // Call base implementation first (syncs inputs from Unity)
            base.PostFixedUpdate();

            // Step physics if manual control is enabled and not paused
            if (manualPhysicsInitialized && IsManualPhysicsMode() && SpeedPercentage > 0)
            {
                fixedUpdateFrameCount++;

                // Calculate how many FixedUpdate frames to skip based on speed percentage
                // 100% = step every frame (interval 1)
                // 50% = step every 2nd frame (interval 2)
                // 25% = step every 4th frame (interval 4)
                // 200% = step every frame (interval 1) - cannot go faster than FixedUpdate rate
                int stepInterval = Mathf.Max(1, Mathf.RoundToInt(100f / SpeedPercentage));

                // Only step physics when we've waited enough frames
                if (fixedUpdateFrameCount >= stepInterval)
                {
                    // Always use normal fixed timestep - don't scale it
                    StepPhysics(Time.fixedDeltaTime);
                    physicsStepCount++;
                    fixedUpdateFrameCount = 0; // Reset counter

                    // Log first few steps for debugging
                    if (physicsStepCount <= 3)
                    {
                        Logger.Message($"Physics stepped in PostFixedUpdate: timestep={Time.fixedDeltaTime:F4}s (step #{physicsStepCount}, interval={stepInterval})", this);
                    }

                    // Log every 100 steps
                    if (physicsStepCount % 100 == 0)
                    {
                        Logger.Message($"Physics step #{physicsStepCount} (Speed: {SpeedPercentage}%, interval: every {stepInterval} frames)", this);
                    }
                }
            }
        }

        //! Override CleanupAfterBackgroundThread to restore auto physics
        protected override void CleanupAfterBackgroundThread()
        {
            base.CleanupAfterBackgroundThread();

            Logger.Message("CleanupAfterBackgroundThread - Restoring automatic physics", this);

            if (EnableManualControl && manualPhysicsInitialized)
            {
                SetManualPhysicsMode(false);
                manualPhysicsInitialized = false;
                Logger.Message("Manual physics control cleaned up", this);
            }
        }

        //! Cleanup when application quits or play mode stops
        protected virtual void OnApplicationQuit()
        {
            Logger.Message("OnApplicationQuit - Final cleanup", this);

            if (EnableManualControl && manualPhysicsInitialized)
            {
                SetManualPhysicsMode(false);
                manualPhysicsInitialized = false;
            }
        }

        #region FastInterfaceBase Blueprint Implementation

        //! Override in derived classes to establish connection to external system
        protected override async System.Threading.Tasks.Task EstablishConnection(System.Threading.CancellationToken cancellationToken)
        {
            // Blueprint - implement in derived classes (e.g., FMU connection)
            await System.Threading.Tasks.Task.CompletedTask;
        }

        //! Override in derived classes for communication loop with external system
        protected override async System.Threading.Tasks.Task CommunicationLoop(System.Threading.CancellationToken cancellationToken)
        {
            // Blueprint - implement in derived classes (e.g., FMU data exchange)
            await System.Threading.Tasks.Task.CompletedTask;
        }

        //! Override in derived classes to close connection to external system
        protected override void CloseConnection()
        {
            // Blueprint - implement in derived classes
        }

        #endregion
    }
}
