// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using NaughtyAttributes;

namespace realvirtual
{
    //! Logic step that pauses the Unity Editor for debugging automation flows.
    //! Acts as a breakpoint — the simulation pauses when this step is reached,
    //! allowing inspection of signals, drives, and scene state via MCP or the Inspector.
    //! Resume the simulation (Editor play button or sim_resume via MCP) to continue the flow.
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/defining-logic/logicsteps")]
    public class LogicStep_Pause : LogicStep
    {
        [Header("Pause Settings")]
        public string Message; //!< Optional message displayed in the console when the pause is triggered

        private int _pauseFrame;

        protected override void OnStarted()
        {
            _pauseFrame = Time.frameCount;
            State = 50;
            IsWaiting = true;
            if (!string.IsNullOrEmpty(Message))
                Debug.Log($"[LogicStep Pause] {Message} - {gameObject.name}");
            else
                Debug.Log($"[LogicStep Pause] {gameObject.name}");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPaused = true;
#endif
        }

        public void FixedUpdate()
        {
            if (StepActive && Time.frameCount > _pauseFrame)
            {
                IsWaiting = false;
                NextStep();
            }
        }
    }

}
