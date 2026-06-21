// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Logic step that triggers a Grip component to place (Unfix) all currently gripped MUs.
    //! When Blocking is true the step waits until no MU is gripped before proceeding.
    [AddComponentMenu("realvirtual/LogicSteps/LogicStep GripPlace")]
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/defining-logic/logicsteps")]
    public class LogicStep_GripPlace : LogicStep
    {
        public Grip Grip; //!< The Grip component to trigger the place operation on
        public bool Blocking = false; //!< If true the step waits until the Grip has released all MUs before proceeding

        private float _waitTimer;
        private const float MAX_WAIT_TIME = 10f; // safety timeout in seconds

        protected override void OnStarted()
        {
            if (Grip == null)
            {
                Debug.LogWarning("LogicStep_GripPlace: No Grip assigned!", this);
                NextStep();
                return;
            }

            Grip.Place();

            if (!Blocking || (Grip.PickedMUs == null || Grip.PickedMUs.Count == 0))
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

            if (Grip != null && (Grip.PickedMUs == null || Grip.PickedMUs.Count == 0))
            {
                IsWaiting = false;
                NextStep();
                return;
            }

            if (_waitTimer >= MAX_WAIT_TIME)
            {
                Debug.LogWarning("LogicStep_GripPlace: Timeout waiting for all MUs to be released.", this);
                IsWaiting = false;
                NextStep();
            }
        }
    }
}
