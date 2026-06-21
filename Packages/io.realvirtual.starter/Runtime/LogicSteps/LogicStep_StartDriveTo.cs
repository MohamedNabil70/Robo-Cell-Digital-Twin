// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using NaughtyAttributes;
using UnityEngine;

namespace realvirtual
{
    //! Logic step that starts a drive movement to a target position without waiting for completion.
    //! For 360° continuous rotation drives, the Direction option allows specifying the movement direction.
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/defining-logic/logicsteps")]
    public class LogicStep_StartDriveTo : LogicStep
    {
        public Drive drive; //!< The drive component to control
        [OnValueChanged("EditorPosition")] public float Destination; //!< Target position for the drive movement
        public bool Relative = false; //!< If true, the destination is relative to current position

        [Header("360° Rotation")]
        [ShowIf("IsContinuousRotation")]
        [Tooltip("Direction for 360° continuous rotation drives")]
        public DriveToDirection Direction = DriveToDirection.Automatic; //!< Movement direction for continuous rotation drives

        private bool IsContinuousRotation => drive != null && drive.UseLimits && drive.JumpToLowerLimitOnUpperLimit;

        [OnValueChanged("LiveEditStart")] public bool LiveEdit = false; //!< Enables live preview of the target position in editor
        protected new bool NonBlocking()
        {
            return true;
        }

        private void LiveEditStart()
        {
            if (drive!=null)
                if (LiveEdit)
                {

                    drive.StartEditorMoveMode();
                    EditorPosition();
                }
                else
                    drive.EndEditorMoveMode();
        }


        private void EditorPosition()
        {
            if (drive != null)
            {
                if (LiveEdit)
                {

                    drive.SetPositionEditorMoveMode(Destination);
                }
            }
        }

        protected override void OnStarted()
        {
            State = 0;
            if (drive != null)
            {
                var des = Destination;
                if (Relative)
                    des = drive.CurrentPosition + Destination;

                // Direction adjustment for 360° continuous rotation drives
                if (drive.UseLimits && drive.JumpToLowerLimitOnUpperLimit && Direction != DriveToDirection.Automatic)
                {
                    float range = drive.UpperLimit - drive.LowerLimit;
                    float currentPos = drive.CurrentPosition;

                    if (Direction == DriveToDirection.Forward && currentPos > des)
                    {
                        // Forward: increase target to force forward movement over upper limit
                        des += range;
                    }
                    else if (Direction == DriveToDirection.Backward && currentPos < des)
                    {
                        // Backward: decrease target to force backward movement under lower limit
                        des -= range;
                    }
                }

                drive.DriveTo(des);
            }

            NextStep();
        }
    }

}
