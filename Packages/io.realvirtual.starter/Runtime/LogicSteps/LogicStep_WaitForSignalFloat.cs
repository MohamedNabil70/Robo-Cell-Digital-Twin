// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;

namespace realvirtual
{
    //! Logic step that waits until a float signal meets a specified condition before proceeding.
    //! This blocking step is used to synchronize automation sequences based on float signal values.
    //! Supports multiple comparison operators (>, <, ==, >=, <=) with configurable tolerance for equality checks.
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/defining-logic/logicsteps")]
    public class LogicStep_WaitForSignalFloat : LogicStep
    {
        //! Comparison operators for float value checking
        public enum ComparisonType
        {
            GreaterThan,       //!< Signal value must be greater than the target value
            LessThan,          //!< Signal value must be less than the target value
            Equals,            //!< Signal value must equal the target value within tolerance
            GreaterOrEqual,    //!< Signal value must be greater than or equal to the target value
            LessOrEqual        //!< Signal value must be less than or equal to the target value
        }

        [Header("Signal Configuration")]
        public Signal Signal; //!< The float signal to monitor
        public ComparisonType Comparison = ComparisonType.GreaterOrEqual; //!< The comparison operator to use
        public float Value; //!< The target value to compare against in signal units
        public float Tolerance = 0.01f; //!< Tolerance for equality comparison (minimum 0.0001)

        private bool signalnotnull = false;

        protected new bool NonBlocking()
        {
            return false;
        }

        protected override void OnStarted()
        {
            IsWaiting = true;
            State = 50;

            // Re-check Signal directly to handle case where Start() hasn't run yet
            signalnotnull = Signal != null;

            if (signalnotnull == false)
                NextStep();
        }

        protected new void Start()
        {
            signalnotnull = Signal != null;
            base.Start();
        }

        private void FixedUpdate()
        {
            if (!StepActive)
                return;

            if (!signalnotnull)
                return;

            // Type safety check - ensure the signal actually contains a float value
            var currentValueObj = Signal.GetValue();
            if (!(currentValueObj is float))
            {
                Logger.Warning($"LogicStep_WaitForSignalFloat: Signal '{Signal.name}' is not a float signal. Expected float but got {currentValueObj?.GetType().Name ?? "null"}. Step will proceed.", this);
                NextStep();
                return;
            }

            float currentValue = (float)currentValueObj;

            // NaN check - if signal returns NaN, continue waiting
            if (float.IsNaN(currentValue))
                return;

            // Perform comparison based on selected operator
            bool conditionMet = false;
            switch (Comparison)
            {
                case ComparisonType.GreaterThan:
                    conditionMet = currentValue > Value;
                    break;

                case ComparisonType.LessThan:
                    conditionMet = currentValue < Value;
                    break;

                case ComparisonType.Equals:
                    // Enforce minimum tolerance to avoid floating point precision issues
                    float effectiveTolerance = Mathf.Max(Tolerance, 0.0001f);
                    conditionMet = Mathf.Abs(currentValue - Value) < effectiveTolerance;
                    break;

                case ComparisonType.GreaterOrEqual:
                    conditionMet = currentValue >= Value;
                    break;

                case ComparisonType.LessOrEqual:
                    conditionMet = currentValue <= Value;
                    break;
            }

            if (conditionMet)
                NextStep();
        }
    }
}
