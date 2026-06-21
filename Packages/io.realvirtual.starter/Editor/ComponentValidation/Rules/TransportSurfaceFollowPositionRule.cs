// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Validates that TransportSurface is not used with Drive_FollowPosition behavior.
    //! This combination is not supported - use KinematicMU instead for position-based transport.
    public class TransportSurfaceFollowPositionRule : PrePlayRule<TransportSurface>
    {
        public override string RuleName => "TransportSurface FollowPosition Check";

        public override bool Validate(TransportSurface transportSurface)
        {
            // Get the Drive component (either from hierarchy or DriveReference)
            var drive = transportSurface.DriveReference != null
                ? transportSurface.DriveReference
                : transportSurface.GetComponentInParent<Drive>();

            if (drive == null)
            {
                return true; // No drive found, other validation rules will handle this
            }

            // Check if Drive has Drive_FollowPosition behavior
            var followPositionBehavior = drive.GetComponent<Drive_FollowPosition>();
            if (followPositionBehavior != null)
            {
                LogWarning($"TransportSurface with Drive_FollowPosition behavior is not supported. Use KinematicMU instead for position-based transport of objects. See: https://doc.realvirtual.io/components-and-scripts/motion/kinematicmu-pro", transportSurface);
                return false;
            }

            return true;
        }
    }
}
