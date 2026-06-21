// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    //! Logic step that sets the ActiveOnly property of specific realvirtualBehavior components to Always or Never.
    //! This non-blocking step is used to control component activation during automation sequences.
    //! Drag specific components (Drives, Sensors, etc.) into the list to control exactly which are toggled.
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/defining-logic/logicsteps")]
    public class LogicStep_SetActiveOnly : LogicStep
    {
        [Header("ActiveOnly Control")]
        public List<realvirtualBehavior> Behaviors = new List<realvirtualBehavior>(); //!< Specific realvirtualBehavior components to modify (Drives, Sensors, etc.)
        public bool SetToAlways = true; //!< If true, sets ActiveOnly to Always; if false, sets to Never

        protected new bool NonBlocking()
        {
            return true;
        }

        protected override void OnStarted()
        {
            State = 50;

            var targetState = SetToAlways
                ? realvirtualBehavior.ActiveOnly.Always
                : realvirtualBehavior.ActiveOnly.Never;

            foreach (var behavior in Behaviors)
            {
                if (behavior != null)
                {
                    behavior.Active = targetState;
                    // Apply the change by calling ChangeConnectionMode
                    // For Always/Never the connection state doesn't matter
                    behavior.ChangeConnectionMode(false);
                }
            }

            NextStep();
        }
    }
}
