// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using NaughtyAttributes;
using UnityEngine;


namespace realvirtual
{
    public class TestSmoothDrive : MonoBehaviour
    {
        public Drive drive;
        public float target;

        [Button]
        public void DriveToTarget()
        {
            drive.DriveTo(target);
        }
    }
}