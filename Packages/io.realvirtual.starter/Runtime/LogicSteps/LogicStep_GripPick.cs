// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Logic step that triggers a Grip component to pick (Fix) an MU.
    //! When Blocking is true the step waits until at least one MU is gripped before proceeding.
    [AddComponentMenu("realvirtual/LogicSteps/LogicStep GripPick")]
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/defining-logic/logicsteps")]
    public class LogicStep_GripPick : LogicStep
    {
        public Grip Grip; //!< The Grip component to trigger the pick operation on
        public bool Blocking = false; //!< If true the step waits until the Grip has at least one MU before proceeding

        private float _waitTimer;
        private const float MAX_WAIT_TIME = 10f; // safety timeout in seconds

        protected override void OnStarted()
        {
            if (Grip == null)
            {
                Debug.LogWarning("LogicStep_GripPick: No Grip assigned!", this);
                NextStep();
                return;
            }

            Grip.Pick();

            if (!Blocking || (Grip.PickedMUs != null && Grip.PickedMUs.Count > 0))
            {
                NextStep();
            }
            else
            {
                IsWaiting = true;
                _waitTimer = 0f;
            }
        }

        private void FixedUpdate()
        {
            if (!IsWaiting) return;

            _waitTimer += Time.fixedDeltaTime;

            if (Grip != null && Grip.PickedMUs != null && Grip.PickedMUs.Count > 0)
            {
                IsWaiting = false;
                NextStep();
                return;
            }

            if (_waitTimer >= MAX_WAIT_TIME)
            {
                Debug.LogWarning("LogicStep_GripPick: Timeout waiting for MU to be gripped.", this);
                IsWaiting = false;
                NextStep();
            }
        }
    }
}
