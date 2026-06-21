// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System;
using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    [AddComponentMenu("realvirtual/Motion/Drive Behaviors/Drive Position Switch")]
    [RequireComponent(typeof(Drive))]
    #region doc
    //! Controls a boolean signal based on drive position ranges with support for wrapping drives.

    //! The Drive Position Switch component monitors the drive's position and sets a boolean output
    //! signal when the position is within defined ranges. Multiple areas use OR logic, meaning the
    //! signal is true if the position is in ANY area. The component fully supports wrapping drives
    //! (JumpToLowerLimitOnUpperLimit) with automatic position normalization and wrapped area detection.
    //!
    //! Key Features:
    //! - Multiple position areas with start and end positions in millimeters or degrees
    //! - Single boolean output controlled by all areas using OR logic
    //! - Global position offset with automatic wrapping for rotational drives
    //! - Area inversion mode to define false zones instead of true zones
    //! - Automatic detection of wrapped areas like 350-10 degrees on rotational drives
    //! - Position normalization handles drives temporarily exceeding limits during wrapping
    //!
    //! Common Applications:
    //! - Position-based activation zones on conveyors or linear axes
    //! - Angular zones on rotational tables or robotic joints with continuous rotation
    //! - Safe zones or work zones in automated systems
    //! - Multi-zone detection with single output signal for PLC integration
    //!
    //! For detailed documentation see: https://doc.realvirtual.io/components-and-scripts/motion/drive-behavior
    #endregion
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/motion/drive-behavior")]
    public class Drive_PositionSwitch : BehaviorInterface, IDriveBehavior
    {
        //! Represents a position area that controls the output signal
        [Serializable]
        public class PositionArea
        {
            public string Name = "Area 1"; //!< Name of the area for identification
            public float StartPosition = 0; //!< Start position of the area in millimeters or degrees
            public float EndPosition = 100; //!< End position of the area in millimeters or degrees
        }

        [Header("Position Areas")]
        [Tooltip("List of position areas that control the output signal using OR logic")]
        public List<PositionArea> Areas = new List<PositionArea>(); //!< List of position areas that control the output signal using OR logic

        [Header("Settings")]
        [Tooltip("If true, areas define false zones instead of true zones (inverts the output)")]
        public bool InvertAreas = false; //!< If true, areas define false zones instead of true zones (inverts the output)
        [Tooltip("Global offset in millimeters or degrees applied to drive position before area checking")]
        public float PositionOffset = 0; //!< Global offset in millimeters or degrees applied to drive position before area checking

        [Header("PLC IOs")]
        [Tooltip("Output signal to PLC, true when position is in any area")]
        public PLCInputBool OutputSignal; //!< Output signal to PLC, true when position is in any area, or inverted if InvertAreas is true

        private Drive _drive; //!< Reference to the attached Drive component
        private bool _outputSignalNotNull; //!< Cached null check for OutputSignal to avoid checks in FixedUpdate

        //! Called when simulation starts - initializes component references and caches
        //! IMPLEMENTS BehaviorInterface::OnStartSim
        protected override void OnStartSim()
        {
            _drive = GetComponent<Drive>();
            _outputSignalNotNull = OutputSignal != null;
        }

        //! Called every physics frame by the Drive component to update the position switch logic
        //! IMPLEMENTS IDriveBehavior::CalcFixedUpdate
        public void CalcFixedUpdate()
        {
            if (ForceStop || !this.enabled)
                return;

            // Get current position and apply offset
            float adjustedPos = _drive.CurrentPosition + PositionOffset;

            // Normalize position if drive uses wrapping
            // This is critical because Drive.CurrentPosition can temporarily exceed UpperLimit
            // before wrapping occurs (e.g., 364° before wrapping to 4° on a 0-360° drive)
            if (_drive.UseLimits && _drive.JumpToLowerLimitOnUpperLimit)
            {
                float range = _drive.UpperLimit - _drive.LowerLimit;

                // Handle positions that exceed limits
                while (adjustedPos >= _drive.UpperLimit)
                    adjustedPos -= range;
                while (adjustedPos < _drive.LowerLimit)
                    adjustedPos += range;
            }

            // Check if position is in any area (OR logic)
            bool inAnyArea = false;

            foreach (var area in Areas)
            {
                bool isInArea;

                // Detect wrapped area: StartPosition > EndPosition
                // Example: Area [350-10] on a 0-360° drive means position is in area if >= 350 OR <= 10
                if (_drive.UseLimits && _drive.JumpToLowerLimitOnUpperLimit &&
                    area.StartPosition > area.EndPosition)
                {
                    // Wrapped area - position is in area if >= start OR <= end
                    isInArea = (adjustedPos >= area.StartPosition) ||
                               (adjustedPos <= area.EndPosition);
                }
                else
                {
                    // Normal area - standard range check
                    isInArea = (adjustedPos >= area.StartPosition &&
                               adjustedPos <= area.EndPosition);
                }

                if (isInArea)
                {
                    inAnyArea = true;
                    break; // OR logic - one match is enough
                }
            }

            // Apply inversion if needed and set output signal
            bool outputValue = InvertAreas ? !inAnyArea : inAnyArea;

            if (_outputSignalNotNull)
                OutputSignal.Value = outputValue;
        }
    }
}
