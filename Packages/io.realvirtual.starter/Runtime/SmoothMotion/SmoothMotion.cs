// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using UnityEngine;

namespace realvirtual
{
    //! Implements advanced S-curve motion profiles for smooth, jerk-limited movement in industrial automation.
    //! This professional motion control system generates optimized acceleration profiles that minimize mechanical
    //! stress and vibration in high-speed automation equipment. Essential for precise positioning in robotics,
    //! CNC machines, and material handling systems where smooth acceleration transitions prevent product damage
    //! and extend equipment lifetime. Supports real-time trajectory modification and speed override capabilities.
    [System.Serializable]
    public class SmoothMotion
    {
        [HideInInspector] public float maxVelocity = 5f;
        [HideInInspector] public float maxAcceleration = 2f;
        [HideInInspector] public float jerk = 5f;
    
        public SCurve profile;
    
        public MotionState _state = new MotionState();
        public MotionState _targetState = new MotionState();
        [HideInInspector] public float speedOverride = 1;
    
        private float _time;
        private float durationScale;
    
    
        public void SetTarget(float position, float velocity)
        {
            MotionState state = new MotionState();
            state.position = position;
            state.velocity = velocity;
            state.acceleration = 0;
            durationScale = 1;
            InitTransitionToState(state);
        }

        public bool Integrate(float dt)
        {
            if (profile == null)
                return false;
            _time += Time.fixedDeltaTime;
            _state = profile.EvaluateCurves(_time/durationScale*speedOverride);

            return true;
        }
    
        public void SetInitialPosition(float position)
        {
            _state.position = position;
        }
    
        public void SetInitialVelocity(float velocity)
        {
            _state.velocity = velocity;
        }
    
        public void SetInitialAcceleration(float acceleration)
        {
            _state.acceleration = acceleration;
        }
    
        public void AdjustDuration(float duration)
        {
            durationScale = duration / profile.duration;
        }
    
        public float GetPosition()
        {
            return _state.position;
        }
    
        public float GetVelocity()
        {
            return _state.velocity;
        }
    
        public float GetAcceleration()
        {
            return _state.acceleration;
        }

        public float GetDuration()
        {
            return profile.duration * durationScale;    
        }
    
        public float GetTime()
        {
            return _time;
        }

        public bool IsAtTarget()
        {
            return _state.position == _targetState.position;
        }
    
        void InitTransitionToState(MotionState state)
        {
            _targetState = state;
            profile = SCurve.Generate(_state, _targetState, maxVelocity, maxAcceleration, jerk);
            _time = 0;
        }
    
    
    }
}