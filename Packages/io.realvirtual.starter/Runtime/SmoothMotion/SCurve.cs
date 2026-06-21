using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    [System.Serializable]
    public class MotionState
    {
        public float position;
        public float velocity;
        public float acceleration;
    }

    [System.Serializable]
    public class SCurve
    {
        public AnimationCurve position;
        public AnimationCurve velocity;
        public AnimationCurve acceleration;
        public AnimationCurve jerk;

        public float duration;
        [HideInInspector] public List<ScurveSegment> phases;

        private static int max_solver_iterations = 60;
        private static int subdivisions = 3;
        private static bool createCurves = true;

        [HideInInspector] public float targetPosition;
        [HideInInspector] public float targetVelocity;
        [HideInInspector] public float targetAcceleration;

        public static SCurve Generate(MotionState initialState, MotionState finalState, float maxVelocity,
            float maxAcceleration, float jerk)
        {
            float x0 = initialState.position;
            float v0 = initialState.velocity;
            float a0 = initialState.acceleration;
            float x1 = finalState.position;
            float v1 = finalState.velocity;
            float a1 = finalState.acceleration;
            float dx = x1 - x0;
            bool invert = dx < 0;

            if (invert)
            {
                dx = -dx;
                v0 = -v0;
                v1 = -v1;
                a0 = -a0;
                a1 = -a1;
            }

            SCurveResult result = SCurveGenerator7Segment.SCurve7SegmentClosedFormPhases(
                dx, v0, a0, v1, maxVelocity, maxAcceleration, jerk, max_solver_iterations);

            SCurve curve = new SCurve();
            curve.targetPosition = x1;
            curve.targetVelocity = v1;
            curve.targetAcceleration = a1;

            curve.phases = result.phases;
            curve.ComputeDuration();

            if (invert)
                curve.InvertDirection();

            curve.OffsetPosition(initialState.position);

            if (createCurves)
                curve.CreateCurves(subdivisions);

            return curve;
        }

        public void InvertDirection()
        {
            for (int i = 0; i < phases.Count; i++)
            {
                ScurveSegment p = phases[i];
                p.v_start   = -p.v_start;
                p.v_end     = -p.v_end;
                p.a_start   = -p.a_start;
                p.a_end     = -p.a_end;
                p.jerk      = -p.jerk;
                p.x_start   = -p.x_start;
                p.x_end     = -p.x_end;
                phases[i]   = p;
            }
            ComputeDuration();
        }

        public void OffsetPosition(float offset)
        {
            for (int i = 0; i < phases.Count; i++)
            {
                ScurveSegment p = phases[i];
                p.x_start += offset;
                p.x_end   += offset;
                phases[i] = p;
            }
        }

        public MotionState EvaluateExact(float t)
        {
            if (t < 0)
            {
                var dt = t - phases[0].t_start;
                var v  = phases[0].v_start;
                var x  = phases[0].x_start + v * dt;
                return new MotionState { position = x, velocity = v, acceleration = 0 };
            }

            if (t > duration)
            {
                var last = phases[phases.Count - 1];
                var dt   = t - last.t_end;
                var v    = last.v_end;
                var x    = last.x_end + v * dt;
                return new MotionState { position = x, velocity = v, acceleration = 0 };
            }

            float elapsed = 0f;
            foreach (var phase in phases)
            {
                if (t <= phase.t_end)
                {
                    float dt = t - phase.t_start;
                    float a  = phase.a_start + phase.jerk * dt;
                    float v  = phase.v_start + phase.a_start * dt + 0.5f * phase.jerk * dt * dt;
                    float x  = phase.x_start + phase.v_start * dt + 0.5f * phase.a_start * dt * dt + (1f/6f) * phase.jerk * dt*dt*dt;
                    return new MotionState { position = x, velocity = v, acceleration = a };
                }
                elapsed += phase.duration;
            }

            return null;
        }

        public MotionState EvaluateCurves(float t)
        {
            var state = new MotionState();
            if (t < 0)
            {
                state.acceleration = 0;
                state.velocity     = velocity.Evaluate(t);
                state.position     = position.Evaluate(t);
                return state;
            }

            if (t >= duration)
            {
                state.acceleration = targetAcceleration;
                state.velocity     = targetVelocity;
                state.position     = targetPosition;
                return state;
            }

            state.position     = position.Evaluate(t);
            state.velocity     = velocity.Evaluate(t);
            state.acceleration = acceleration.Evaluate(t);
            return state;
        }

        public float GetTargetVelocity() => phases[phases.Count - 1].v_end;

        // Subdivision helpers (unchanged)...
        (List<float>, List<float>, List<float>, List<float>, List<float>) SubdivideOnce(
            List<float> positions, List<float> velocities, List<float> accelerations,
            List<float> jerks, List<float> times)
        {
            var newP = new List<float>();
            var newV = new List<float>();
            var newA = new List<float>();
            var newJ = new List<float>();
            var newT = new List<float>();

            for (int i=0; i<positions.Count; i++)
            {
                newP.Add(positions[i]); newV.Add(velocities[i]); newA.Add(accelerations[i]); newJ.Add(jerks[i]); newT.Add(times[i]);
                if (i < positions.Count-1)
                {
                    float t0 = times[i], t1 = times[i+1];
                    float dt = (t1 - t0)/2f;
                    float midT = t0 + dt;
                    float j    = jerks[i];
                    float a0   = accelerations[i];
                    float v0   = velocities[i];
                    float x0   = positions[i];

                    float aMid = a0 + j * dt;
                    float vMid = v0 + a0 * dt + 0.5f * j * dt*dt;
                    float xMid = x0 + v0*dt + 0.5f*a0*dt*dt + (1f/6f)*j*dt*dt*dt;

                    newP.Add(xMid); newV.Add(vMid); newA.Add(aMid); newJ.Add(j); newT.Add(midT);
                }
            }
            return (newP,newV,newA,newJ,newT);
        }

        (List<float>, List<float>, List<float>, List<float>, List<float>) GetSubdivided(int sub)
        {
            var positions     = new List<float>();
            var velocities    = new List<float>();
            var accelerations = new List<float>();
            var jerks         = new List<float>();
            var times         = new List<float>();

            float tAcc = 0f;
            foreach (var ph in phases)
            {
                positions.Add(ph.x_start);
                velocities.Add(ph.v_start);
                accelerations.Add(ph.a_start);
                jerks.Add(ph.jerk);
                times.Add(tAcc);
                tAcc += ph.duration;
            }
            var last = phases[phases.Count-1];
            positions.Add(last.x_end);
            velocities.Add(last.v_end);
            accelerations.Add(last.a_end);
            jerks.Add(last.jerk);
            times.Add(tAcc);

            for (int i=0; i<sub; i++)
                (positions, velocities, accelerations, jerks, times) = SubdivideOnce(positions, velocities, accelerations, jerks, times);

            return (positions, velocities, accelerations, jerks, times);
        }

        public void CreateCurves(int subdivisions)
        {
            var (positions, velocities, accelerations, jerks, times) = GetSubdivided(subdivisions);

            position     = new AnimationCurve();
            velocity     = new AnimationCurve();
            acceleration = new AnimationCurve();
            jerk         = new AnimationCurve();
            
            float[] accelerationInTangents = new float[positions.Count];
            float[] accelerationOutTangents = new float[positions.Count];
            float[] jerkInTangents = new float[positions.Count];
            float[] jerkOutTangents = new float[positions.Count];
            
            for (int i = 0; i < positions.Count; i++)
            {
                if (i == 0)
                {
                    float da = accelerations[i+1] - accelerations[i];
                    float dt = times[i+1] - times[i];
                    accelerationOutTangents[i] = da / dt;
                    accelerationInTangents[i]  = accelerationOutTangents[i];
                    
                    float dj = jerks[i+1] - jerks[i];
                    jerkOutTangents[i] = dj / dt;
                    jerkInTangents[i]  = jerkOutTangents[i];
                    
                    
                    
                }
                
                else if (i == positions.Count - 1)
                {
                    float da = accelerations[i] - accelerations[i-1];
                    float dt = times[i] - times[i-1];
                    accelerationInTangents[i]  = da / dt;
                    accelerationOutTangents[i] = accelerationInTangents[i];
                    
                    float dj = jerks[i] - jerks[i-1];
                    jerkInTangents[i]  = dj / dt;
                    jerkOutTangents[i] = jerkInTangents[i];
                }
                else
                {

                    accelerationInTangents[i] = accelerationOutTangents[i-1];
                    float da = accelerations[i+1] - accelerations[i];
                    float dt = times[i+1] - times[i];
                    accelerationOutTangents[i] = da / dt;
                    
                    jerkInTangents[i]  = jerkOutTangents[i];
                    float dj = jerks[i+1] - jerks[i];
                    jerkOutTangents[i] = dj / dt;
                }



            }

            for (int i = 0; i < positions.Count; i++)
            {
                var keyP = new Keyframe(times[i], positions[i]) { inTangent = velocities[i], outTangent = velocities[i] };
                position.AddKey(keyP);

                var keyV = new Keyframe(times[i], velocities[i]) { inTangent = accelerations[i], outTangent = accelerations[i] };
                velocity.AddKey(keyV);
                
                var keyA = new Keyframe(times[i], accelerations[i]) { inTangent = accelerationInTangents[i], outTangent = accelerationInTangents[i]};
                keyA.weightedMode = WeightedMode.Both;
                acceleration.AddKey(keyA);

                var keyJ = new Keyframe(times[i], jerks[i]);
                keyJ.weightedMode = WeightedMode.Both;
                jerk.AddKey(keyJ);
            }
            // No editor-only tangent-mode calls needed at runtime; Unity uses the numeric tangents above.
        }

        void ComputeDuration()
        {
            duration = phases[phases.Count - 1].t_end - phases[0].t_start;
        }
    }
}
