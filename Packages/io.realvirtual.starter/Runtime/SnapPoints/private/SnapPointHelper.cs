// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using UnityEngine;

namespace realvirtual
{
    //! Utility class providing helper methods for snap point operations.
    //!
    //! This class simplifies implementation of ISnapable components by providing
    //! reusable alignment and attachment logic. Reduces boilerplate code and ensures
    //! consistent behavior across all snapable components.
    //!
    //! Key Features:
    //! - Standardized snap point alignment with rotation handling
    //! - Support for SnapIn/SnapOut, SnapLeft/SnapRight connections
    //! - Automatic 180-degree rotation for reverse connections
    //! - Configurable additional rotations for specialized cases
    //!
    //! Usage Example:
    //! <code>
    //! public void Connect(SnapPoint own, SnapPoint mate, ISnapable obj, bool ismoved)
    //! {
    //!     if (ismoved)
    //!         SnapPointHelper.Align(transform, own, mate, Quaternion.identity);
    //!     OnSnapped?.Invoke(own, mate);
    //! }
    //! </code>
    public static class SnapPointHelper
    {
        //! Aligns a transform to a snap point connection with rotation handling.
        //!
        //! This method handles the standard alignment patterns for snap points:
        //! - SnapOut to SnapIn: Standard forward connection
        //! - SnapIn to SnapOut: Reverse connection with 180° rotation
        //! - Same named snap points: 180° rotation
        //! - SnapLeft/SnapRight: Side connections with appropriate rotation
        //!
        //! <param name="transform">The transform to align</param>
        //! <param name="ownSnapPoint">The snap point on the object being moved</param>
        //! <param name="mateSnapPoint">The snap point being connected to</param>
        //! <param name="additionalRotation">Additional rotation to apply after alignment (default: no rotation)</param>
        //! <param name="applyRotation">Whether to apply rotation (default: true)</param>
        //!
        //! Example:
        //! <code>
        //! // Standard alignment
        //! SnapPointHelper.Align(transform, mySnapOut, theirSnapIn, Quaternion.identity);
        //!
        //! // With 180° additional rotation
        //! SnapPointHelper.Align(transform, mySnapOut, theirSnapLeft, Quaternion.Euler(0, 180, 0));
        //!
        //! // Position only, no rotation
        //! SnapPointHelper.Align(transform, mySnap, theirSnap, Quaternion.identity, false);
        //! </code>
        public static void Align(Transform transform, SnapPoint ownSnapPoint, SnapPoint mateSnapPoint,
            Quaternion additionalRotation, bool applyRotation = true)
        {
            if (transform == null || ownSnapPoint == null || mateSnapPoint == null)
                return;

            // Initial position alignment
            transform.Translate(mateSnapPoint.transform.position - ownSnapPoint.transform.position, Space.World);

            if (!applyRotation)
                return;

            // Calculate rotation based on snap point names
            if (ownSnapPoint.name == "SnapOut" && mateSnapPoint.name == "SnapIn")
            {
                // Standard forward connection - align rotations
                var rotationDelta = mateSnapPoint.transform.rotation * Quaternion.Inverse(ownSnapPoint.transform.rotation);
                transform.rotation = rotationDelta * transform.rotation;
            }
            else if (ownSnapPoint.name == "SnapIn" && mateSnapPoint.name == "SnapOut")
            {
                // Reverse connection - align and add 180° rotation
                var rotationDelta = mateSnapPoint.transform.rotation * Quaternion.Inverse(ownSnapPoint.transform.rotation);
                transform.rotation = rotationDelta * transform.rotation;
                transform.rotation = Quaternion.Euler(0, 180, 0) * transform.rotation;
            }
            else if (ownSnapPoint.name == mateSnapPoint.name)
            {
                // Same snap point names - facing each other, apply 180° rotation
                var rotationDelta = mateSnapPoint.transform.rotation * Quaternion.Inverse(ownSnapPoint.transform.rotation);
                transform.rotation = rotationDelta * transform.rotation;
                transform.rotation = Quaternion.Euler(0, 180, 0) * transform.rotation;
            }
            else if (mateSnapPoint.name != ownSnapPoint.name)
            {
                // Different snap point types - basic alignment
                var rotationDelta = mateSnapPoint.transform.rotation * Quaternion.Inverse(ownSnapPoint.transform.rotation);
                transform.rotation = rotationDelta * transform.rotation;
            }

            // Apply additional rotation if specified
            if (additionalRotation != Quaternion.identity)
            {
                transform.rotation = additionalRotation * transform.rotation;
            }

            // Final position adjustment after rotation
            transform.Translate(mateSnapPoint.transform.position - ownSnapPoint.transform.position, Space.World);
        }

        //! Aligns a transform to a snap point connection with default settings.
        //! Convenience overload that uses Quaternion.identity for additional rotation.
        //!
        //! <param name="transform">The transform to align</param>
        //! <param name="ownSnapPoint">The snap point on the object being moved</param>
        //! <param name="mateSnapPoint">The snap point being connected to</param>
        public static void Align(Transform transform, SnapPoint ownSnapPoint, SnapPoint mateSnapPoint)
        {
            Align(transform, ownSnapPoint, mateSnapPoint, Quaternion.identity, true);
        }

        //! Attaches a transform to a snap point by matching position and rotation.
        //! Simpler than Align - just copies the snap point's transform directly.
        //!
        //! <param name="transform">The transform to attach</param>
        //! <param name="snapPoint">The snap point to attach to</param>
        //!
        //! Example:
        //! <code>
        //! public void AttachTo(SnapPoint attachto)
        //! {
        //!     SnapPointHelper.AttachTo(transform, attachto);
        //! }
        //! </code>
        public static void AttachTo(Transform transform, SnapPoint snapPoint)
        {
            if (transform == null || snapPoint == null)
                return;

            transform.position = snapPoint.transform.position;
            transform.rotation = snapPoint.transform.rotation;
        }

        //! Checks all child SnapPoint components for connections.
        //! Common implementation for ISnapable.CheckSnap() method.
        //!
        //! <param name="component">The component whose children should be checked</param>
        //!
        //! Example:
        //! <code>
        //! public void CheckSnap()
        //! {
        //!     SnapPointHelper.CheckChildSnapPoints(this);
        //! }
        //! </code>
        public static void CheckChildSnapPoints(Component component)
        {
            if (component == null)
                return;

            var snapPoints = component.GetComponentsInChildren<SnapPoint>(true);
            foreach (var snapPoint in snapPoints)
            {
                if (snapPoint != null)
                    snapPoint.CheckSnap();
            }
        }

        //! Standard Connect implementation that handles alignment and event invocation.
        //! Common implementation for ISnapable.Connect() method.
        //!
        //! <param name="transform">The transform to align</param>
        //! <param name="ownSnapPoint">The snap point on the object being moved</param>
        //! <param name="snapPointMate">The snap point being connected to</param>
        //! <param name="ismoved">Whether this object is being moved</param>
        //! <param name="onSnapped">The OnSnapped event to invoke (can be null)</param>
        //!
        //! Example:
        //! <code>
        //! public void Connect(SnapPoint own, SnapPoint mate, ISnapable obj, bool ismoved)
        //! {
        //!     SnapPointHelper.StandardConnect(transform, own, mate, ismoved, OnSnapped);
        //! }
        //! </code>
        public static void StandardConnect(Transform transform, SnapPoint ownSnapPoint, SnapPoint snapPointMate,
            bool ismoved, OnSnappedEvent onSnapped)
        {
            if (transform == null || ownSnapPoint == null || snapPointMate == null)
                return;

            // Align if this object is being moved
            if (ismoved)
            {
                Align(transform, ownSnapPoint, snapPointMate);
            }

            // Invoke OnSnapped event if set
            if (onSnapped != null)
                onSnapped.Invoke(ownSnapPoint, snapPointMate);
        }
    }
}
