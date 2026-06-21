// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using NaughtyAttributes;
using UnityEngine;

namespace realvirtual
{
    [SelectionBase]
    public class RebelTarget : MonoBehaviour
    {
        public float Speed = 100;
        public bool MotionTypePTP = false;
        public bool HideGhostGripperOnPlay = true;
        public GameObject GhostGripper;

        [HideInInspector] public float[] Joints;
        [ReadOnly] public bool IsMovingToTarget = false;
        [ReadOnly] public igusREBELInterface rebelInterface;


        [Button("Move To Position")]
        public void MoveToPosition()
        {
            GetInterface().MoveToPosition(this, Vector3.zero,Speed, MotionTypePTP);
        }


        private igusREBELInterface GetInterface()
        {
            if (rebelInterface == null)
            {
                // find rebelinterface in parent
                rebelInterface = GetComponentInParent<igusREBELInterface>();

                // if it is still null find it anywhere
                if (rebelInterface == null)
                {
                    rebelInterface = FindFirstObjectByType<igusREBELInterface>();
                }
            }
            // print out error if rebelinterface not defined
            if (rebelInterface == null)
            {
                Debug.LogError("RebelPosition - No igusREBELInterface found in parent");
            }

            return rebelInterface;
        }

        private void Start()
        {
            if (HideGhostGripperOnPlay)
            {
                if (GhostGripper != null)
                {
                    GhostGripper.SetActive(false);
                }
            }
            else
            {
                if (GhostGripper != null)
                {
                    GhostGripper.SetActive(true);
                }
            }
        }
    }
}
