// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Splines;

namespace realvirtual
{
    //! ChainUnitySpline provides spline-based path definition for Chain components using Unity Splines.
    //! It implements the IChain interface using Unity's built-in SplineContainer for path definition.
    //! Attach this component alongside a SplineContainer on the same GameObject as the Chain component.
    //!
    //! The input parameter of GetPosition / GetDirection / GetUpDirection is treated as an
    //! arc-length fraction along the spline (0..1), not as Unity's native t parameter. Unity
    //! Splines parameterizes by segment, so equal t-spacing does not yield equal physical spacing.
    //! Converting to arc length here ensures that chain elements stay equally spaced regardless
    //! of spline knot density and that symmetric spline layouts render symmetrically.
    public class ChainUnitySpline : SplineComponent, IChain
    {
        [Tooltip("Reference to the Unity Spline Container component")]
        public SplineContainer splineContainer;
        private float lastclosestperc;

        // Cached spline-local arc length, used to map an arc-length fraction (0..1) to the
        // spline's normalized t. Refreshed in CalculateLength(), which Chain calls whenever
        // the spline geometry or element layout is rebuilt.
        private float cachedSplineLength = -1f;

        private void Awake()
        {
            splineContainer = GetComponent<SplineContainer>();
            if(splineContainer==null)
                Debug.LogError("No SplineContainer found. Please add a SplineContainer to the GameObject");
        }

        public Vector3 GetClosestDirection(Vector3 position)
        {
            lastclosestperc = ClosestPoint(position, 100);
            return GetDirection(lastclosestperc);
        }

        public Vector3 GetClosestPoint(Vector3 position)
        {
            lastclosestperc = ClosestPoint(position, 100);
            return GetPosition(lastclosestperc);
        }

        public Vector3 GetPosition(float normalizedposition, bool normalized = true)
        {
            float t = ArcLengthFractionToT(normalizedposition);
            return splineContainer.EvaluatePosition(t);
        }

        public Vector3 GetDirection(float normalizedposition, bool normalized = true)
        {
            float t = ArcLengthFractionToT(normalizedposition);
            return splineContainer.EvaluateTangent(t);
        }

        public Vector3 GetUpDirection(float normalizedposition, bool normalized = true)
        {
            float t = ArcLengthFractionToT(normalizedposition);
            return splineContainer.EvaluateUpVector(t);
        }

        public float CalculateLength()
        {
            if(splineContainer==null)
                splineContainer = GetComponent<SplineContainer>();

            // Refresh arc-length cache used by ArcLengthFractionToT.
            if (splineContainer != null && splineContainer.Spline != null)
                cachedSplineLength = splineContainer.Spline.GetLength();

            return splineContainer.CalculateLength();
        }

        // Converts an arc-length fraction (0..1 along the spline) to Unity's normalized t parameter.
        // Required because Unity Splines parameterizes by segment, not by arc length.
        private float ArcLengthFractionToT(float arcLengthFraction)
        {
            if (splineContainer == null || splineContainer.Spline == null)
                return Mathf.Clamp01(arcLengthFraction);

            if (cachedSplineLength <= 0f)
                cachedSplineLength = splineContainer.Spline.GetLength();

            if (cachedSplineLength <= 0f)
                return Mathf.Clamp01(arcLengthFraction);

            arcLengthFraction = Mathf.Clamp01(arcLengthFraction);
            float distance = arcLengthFraction * cachedSplineLength;
            return SplineUtility.ConvertIndexUnit(
                splineContainer.Spline,
                distance,
                PathIndexUnit.Distance,
                PathIndexUnit.Normalized);
        }

        public bool UseSimulationPath()
        {
            return false;
        }

        public float ClosestPoint(Vector3 point, int divisions = 100)
        {
            //make sure we have at least one division:
            if (divisions <= 0) divisions = 1;

            //variables:
            float shortestDistance = float.MaxValue;
            Vector3 position = Vector3.zero;
            Vector3 offset = Vector3.zero;
            float closestPercentage = 0;
            float percentage = 0;
            float distance = 0;

            //iterate spline and find the closest point on the spline to the provided point:
            for (float i = 0; i < divisions + 1; i++)
            {
                percentage = i / divisions;
                position = GetPosition(percentage);
                offset = position - point;
                distance = offset.sqrMagnitude;

                //if this point is closer than any others so far:
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    closestPercentage = percentage;
                }
            }

            return closestPercentage;
        }
    }
}
