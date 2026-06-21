// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Logic step that sets a float signal to a specified value and immediately proceeds.
    //! This non-blocking step is used to control float signals during automation sequences.
    //! Commonly used for setting speed values, position targets, or analog control signals.
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/defining-logic/logicsteps")]
    public class LogicStep_SetSignalFloat : LogicStep
    {
        [Header("Signal Configuration")]
        public Signal Signal; //!< The float signal to set
        public float Value; //!< The float value to set the signal to in signal units

        private bool signalnotnull = false;

        protected new bool NonBlocking()
        {
            return true;
        }

        protected override void OnStarted()
        {
            State = 50;

            if (signalnotnull)
            {
                // Type safety check - ensure the signal actually contains a float value
                var currentValue = Signal.GetValue();
                if (currentValue is float)
                {
                    Signal.SetValue((float)Value);
                }
                else
                {
                    Logger.Warning($"LogicStep_SetSignalFloat: Signal '{Signal.name}' is not a float signal. Expected float but got {currentValue?.GetType().Name ?? "null"}. Step will be skipped.", this);
                }
            }

            NextStep();
        }

        protected new void Start()
        {
            signalnotnull = Signal != null;
            base.Start();
        }
    }
}
