// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace realvirtual
{
    
    /// <summary>
    /// Data structure representing one segment of a jerk-limited S-curve.
    /// </summary>
    [Serializable]
    public struct ScurveSegment
    {
        public float duration; // Duration of this segment
        public float jerk; // Jerk (m/s^3) is constant over the segment
        public float a_start; // Acceleration at segment start
        public float a_end; // Acceleration at segment end
        public float v_start; // Velocity at segment start
        public float v_end; // Velocity at segment end
        public float x_start; // Position at segment start
        public float x_end; // Position at segment end
        public float t_start; // Global time at segment start
        public float t_end; // Global time at segment end

        public ScurveSegment(float duration, float jerk,
            float a_start, float a_end,
            float v_start, float v_end,
            float x_start, float x_end,
            float t_start, float t_end)
        {
            this.duration = duration;
            this.jerk = jerk;
            this.a_start = a_start;
            this.a_end = a_end;
            this.v_start = v_start;
            this.v_end = v_end;
            this.x_start = x_start;
            this.x_end = x_end;
            this.t_start = t_start;
            this.t_end = t_end;
        }
    }

    /// <summary>
    /// Holds the final result of a 7-segment S-curve calculation:
    ///   - A list of the segment objects
    /// </summary>
    [Serializable]
    public class SCurveResult
    {
        public List<ScurveSegment> phases;

        public SCurveResult()
        {
            phases = new List<ScurveSegment>();
        }
    }

    /// <summary>
    /// Utility class for generating a jerk-limited 7-segment S-curve
    /// that covers distance x, from v0->v1, with constraints on
    /// v_max, a_max, j_max, assuming final accel=0 and an arbitrary initial accel=a0.
    /// If v_max cannot be reached, we solve for a smaller v_peak.
    /// </summary>
    public static class SCurveGenerator7Segment
    {
        /// <summary>
        /// Main entry point:
        ///   1) Adjust initial acceleration a0 to zero if needed.
        ///   2) If we can fully accelerate to v_max and decelerate to v1 within the remaining distance, do so (and add a cruise if leftover).
        ///   3) Otherwise, bisection to find a smaller peak velocity.
        ///   4) Build the 7 time durations T1..T7 and expand the phases.
        /// Returns a SCurveResult with phases.
        /// </summary>
        public static SCurveResult SCurve7SegmentClosedFormPhases(
            float x, // total distance
            float v0, // initial velocity (>= 0)
            float a0, // initial acceleration (in [-aMax, aMax])
            float v1, // final velocity (>= 0)
            float vMax, // max velocity, absolute value
            float aMax, // max acceleration, absolute value
            float jMax, // max jerk, absolute value
            int v_max_itr = 60 // max iterations for bisection
        )
        {
            SCurveResult result = new SCurveResult();

            // Initialize state variables with nonzero a0
            float currentX = 0f;
            float currentV = v0;
            float currentA = a0;
            float accumulatedTime = 0f;

            // Helper to integrate a single segment (constant jerk for time segTime)
            ScurveSegment IntegrateSegment(float jerk, float segTime)
            {
                float x0 = currentX;
                float v0_ = currentV;
                float a0_ = currentA;
                float t = segTime;
                float t0 = accumulatedTime;
                accumulatedTime += t;
                float t1 = accumulatedTime;

                float aEnd = a0_ + jerk * t;
                float vEnd = v0_ + a0_ * t + 0.5f * jerk * (t * t);
                float xEnd = x0 + v0_ * t + 0.5f * a0_ * (t * t) + (jerk * (t * t * t)) / 6.0f;

                ScurveSegment seg = new ScurveSegment(
                    t, jerk, a0_, aEnd, v0_, vEnd, x0, xEnd, t0, t1
                );

                currentX = xEnd;
                currentV = vEnd;
                currentA = aEnd;

                return seg;
            }

            // Preliminary adjustment: if initial acceleration is not zero, bring it to zero.
            if (Mathf.Abs(currentA) > 1e-6f && a0 < 0)
            {
                float T_init = Mathf.Abs(currentA) / jMax;
                float initJerk = -Mathf.Sign(currentA) * jMax;
                result.phases.Add(IntegrateSegment(initJerk, T_init));
                // After this step, currentA should be approximately 0.
            }

            // Update starting conditions for the main algorithm.
            float v0_new = currentV;
            float x_remaining = x - currentX;

            // 1) Check if we can reach vMax with accelerate+decelerate within the remaining distance.
            float distFull = DistanceForVpeak(v0_new, v1, vMax, aMax, jMax, currentA);
            float vPeak;
            float T4; // cruise time

            if (distFull <= x_remaining)
            {
                vPeak = vMax;
                float leftover = x_remaining - distFull;
                T4 = (Mathf.Abs(vMax) > 1e-6f) ? (leftover / vMax) : 0f;
                if (T4 < 0f) T4 = 0f;
            }
            else
            {
                float lower = Mathf.Min(v0_new, v1);
                float upper = vMax;
                float xTolerance = 1e-6f;

                for (int i = 0; i < v_max_itr; i++)
                {
                    float mid = 0.5f * (lower + upper);
                    float dMid = DistanceForVpeak(v0_new, v1, mid, aMax, jMax, currentA);
                    if (dMid > x_remaining)
                        upper = mid;
                    else
                        lower = mid;

                    if (Mathf.Abs(dMid - x_remaining) < xTolerance)
                        break;
                }

                vPeak = 0.5f * (lower + upper);
                T4 = 0f;
            }

            // 2) Now we have vPeak, build times T1..T3, T5..T7
            (float T1, float T2, float T3) = AccelTimesTriOrTrap(v0_new, vPeak, aMax, jMax, currentA);
            (float T5, float T6, float T7) = DecelTimesTriOrTrap(vPeak, v1, aMax, jMax);

            // 3) Expand into segments for the remaining phases
            if (T1 > 1e-6f) result.phases.Add(IntegrateSegment(+jMax, T1));
            if (T2 > 1e-6f) result.phases.Add(IntegrateSegment(0f, T2));
            if (T3 > 1e-6f) result.phases.Add(IntegrateSegment(-jMax, T3));
            if (T4 > 1e-6f) result.phases.Add(IntegrateSegment(0f, T4));
            if (T5 > 1e-6f) result.phases.Add(IntegrateSegment(-jMax, T5));
            if (T6 > 1e-6f) result.phases.Add(IntegrateSegment(0f, T6));
            if (T7 > 1e-6f) result.phases.Add(IntegrateSegment(+jMax, T7));

            // Correct the final position to match the target
            if (result.phases.Count > 0)
            {
                ScurveSegment lastSeg = result.phases[result.phases.Count - 1];
                lastSeg.x_end = x;
                lastSeg.v_end = v1;
                result.phases[result.phases.Count - 1] = lastSeg;
            }

            return result;
        }

        private static (float, float, float) AccelTimesTriOrTrap(
            float vStart, float vEnd, float aMax, float jMax, float a0)
        {
            float dv = vEnd - vStart;
            if (dv < 1e-6f)
            {
                return (0f, 0f, 0f); // no accelerate
            }

            float Tj_corrected = (aMax - Mathf.Abs(a0)) / jMax;
            float dv_j_corrected = Mathf.Abs(a0) * Tj_corrected + 0.5f * jMax * Tj_corrected * Tj_corrected;

            float Tj_full = aMax / jMax;
            float dv_j_full = 0.5f * aMax * Tj_full;

            if ((dv_j_corrected + dv_j_full) > dv)
            {
                // Triangular
                float T1 = Mathf.Sqrt((dv / jMax) + (a0 * a0) / (2f * jMax * jMax));
                float T2 = 0f;
                float T3 = Mathf.Sqrt((dv / jMax));
                return (T1, T2, T3);
            }
            else
            {
                // Trapezoidal
                float dvLeft = dv - (dv_j_corrected + dv_j_full);
                float T1 = Tj_corrected;
                float T2 = dvLeft / aMax;
                float T3 = Tj_full;
                return (T1, T2, T3);
            }
        }

        private static (float, float, float) DecelTimesTriOrTrap(
            float vStart, float vEnd, float aMax, float jMax)
        {
            float dv = vStart - vEnd; // >0 if decelerating
            if (dv < 1e-6f)
            {
                return (0f, 0f, 0f); // no deceleration needed
            }

            float Tj = aMax / jMax;
            float dv_j = 0.5f * aMax * Tj;

            if (2f * dv_j > dv)
            {
                // Triangular deceleration
                float T5 = Mathf.Sqrt(dv / jMax);
                float T6 = 0f;
                float T7 = T5;
                return (T5, T6, T7);
            }
            else
            {
                // Trapezoidal deceleration
                float dvLeft = dv - 2f * dv_j;
                float T5 = Tj;
                float T6 = dvLeft / aMax;
                float T7 = Tj;
                return (T5, T6, T7);
            }
        }

        private static float DistanceForVpeak(float v0, float v1, float vPeak,
            float aMax, float jMax, float a0)
        {
            (float T1, float T2, float T3) = AccelTimesTriOrTrap(v0, vPeak, aMax, jMax, a0);
            (float T5, float T6, float T7) = DecelTimesTriOrTrap(vPeak, v1, aMax, jMax);

            float distAcc = DistAccelerate(v0, T1, T2, T3, jMax, a0);
            float distDec = DistDecelerate(vPeak, T5, T6, T7, aMax, jMax);
            return distAcc + distDec;
        }

        private static float DistAccelerate(float vStart,
            float T1, float T2, float T3,
            float jMax, float a0)
        {
            float j = jMax;
            float dist1 = vStart * T1 + a0 * T1 * T1 / 2 + (j * T1 * T1 * T1) / 6;
            float a1 = a0 + j * T1;
            float vMid1 = vStart + a0 * T1 + 0.5f * j * T1 * T1;

            float dist2 = vMid1 * T2 + 0.5f * a1 * T2 * T2;
            float vMid2 = vMid1 + a1 * T2;

            float dist3 = vMid2 * T3 + 0.5f * a1 * T3 * T3 - (j * T3 * T3 * T3) / 6;
            return dist1 + dist2 + dist3;
        }

        private static float DistDecelerate(float vStart,
            float T5, float T6, float T7,
            float aMax, float jMax)
        {
            float j = jMax;
            float dist5 = vStart * T5 + (-j * (T5 * T5 * T5)) / 6f;
            float a5 = -jMax * T5;
            float vMid5 = vStart + 0.5f * a5 * T5;

            float dist6 = vMid5 * T6 + 0.5f * a5 * (T6 * T6);
            float vMid6 = vMid5 + a5 * T6;

            float dist7 = vMid6 * T7 + 0.5f * a5 * (T7 * T7) + (j * (T7 * T7 * T7)) / 6f;

            return dist5 + dist6 + dist7;
        }
    }
}