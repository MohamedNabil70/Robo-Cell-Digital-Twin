// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using UnityEngine;
using System.Linq;

namespace realvirtual
{
    //! Validates that TransportSurface has meshes available for collider creation or has existing colliders.
    //! Checks both direct mesh children and Kinematic group integration scenarios.
    public class TransportSurfaceMeshValidationRule : PrePlayRule<TransportSurface>
    {
        public override string RuleName => "TransportSurface Mesh/Collider Availability";

        public override bool Validate(TransportSurface transportSurface)
        {
            // First check: Does it already have a collider? If yes, we're good.
            var existingCollider = transportSurface.GetComponent<Collider>();
            if (existingCollider != null)
            {
                return true; // Already has a collider, validation passed
            }

            // Check if using Kinematic group integration
            var kinematic = transportSurface.GetComponent<Kinematic>();
            if (kinematic != null && kinematic.IntegrateGroupEnable && !string.IsNullOrEmpty(kinematic.GroupName))
            {
                return ValidateKinematicGroup(transportSurface, kinematic);
            }

            // Not using groups - check for direct meshes
            return ValidateDirectMeshes(transportSurface);
        }

        private bool ValidateKinematicGroup(TransportSurface transportSurface, Kinematic kinematic)
        {
            // Check if group has any colliders already
            var groupColliders = Groups.GetAllCollidersInGroup(kinematic.GetGroupName());
            if (groupColliders != null && groupColliders.Count > 0)
            {
                return true; // Group has colliders
            }

            // No colliders in group - check if we can create them from meshes
            var groupObjects = Groups.GetGameObjectsWithGroupIncludingChildren(kinematic.GetGroupName());
            if (groupObjects == null || groupObjects.Count == 0)
            {
                LogWarning($"Kinematic group '{kinematic.GroupName}' contains no GameObjects. TransportSurface cannot create colliders.", transportSurface);
                return false;
            }

            // Check for meshes in the group
            var meshFilters = groupObjects
                .Where(obj => obj != null)
                .SelectMany(obj => obj.GetComponentsInChildren<MeshFilter>(true))
                .Where(mf => mf != null && mf.sharedMesh != null)
                .ToList();

            if (meshFilters.Count == 0)
            {
                LogWarning($"Kinematic group '{kinematic.GroupName}' contains no meshes. TransportSurface cannot create colliders without mesh geometry. Add mesh objects to the group or add a collider manually.", transportSurface);
                return false;
            }

            // Check if any meshes are readable (required for mesh combining)
            int readableCount = meshFilters.Count(mf => mf.sharedMesh.isReadable);
            int nonReadableCount = meshFilters.Count - readableCount;

            if (readableCount == 0 && transportSurface.UseMeshCollider)
            {
                LogWarning($"Kinematic group '{kinematic.GroupName}' has {nonReadableCount} mesh(es) but none have Read/Write enabled. Enable Read/Write in mesh import settings to allow TransportSurface to create a MeshCollider, or disable UseMeshCollider to use BoxCollider instead.", transportSurface);
                return false;
            }

            if (nonReadableCount > 0 && transportSurface.UseMeshCollider)
            {
                LogWarning($"Kinematic group '{kinematic.GroupName}' has {nonReadableCount} mesh(es) without Read/Write enabled. Only {readableCount} readable mesh(es) will be included in the MeshCollider. Enable Read/Write for all meshes in import settings for complete collision coverage.", transportSurface);
                // Return true but warn - partial collider is better than none
            }

            return true;
        }

        private bool ValidateDirectMeshes(TransportSurface transportSurface)
        {
            // Check for mesh on this GameObject or children
            var meshFilters = transportSurface.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters == null || meshFilters.Length == 0)
            {
                LogWarning("TransportSurface has no mesh geometry for collider creation. Add a MeshFilter component with a mesh, use Kinematic group integration, or add a collider manually.", transportSurface);
                return false;
            }

            // Check if meshes are readable (if using MeshCollider and need to combine)
            var meshFilter = transportSurface.GetComponent<MeshFilter>();
            if (meshFilter == null && transportSurface.UseMeshCollider)
            {
                // Will need to combine child meshes
                int readableCount = meshFilters.Count(mf => mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable);
                int totalCount = meshFilters.Count(mf => mf != null && mf.sharedMesh != null);

                if (readableCount == 0)
                {
                    LogWarning($"TransportSurface has {totalCount} child mesh(es) but none have Read/Write enabled. Enable Read/Write in mesh import settings to allow mesh combining for MeshCollider, or disable UseMeshCollider to use BoxCollider instead.", transportSurface);
                    return false;
                }

                if (readableCount < totalCount)
                {
                    LogWarning($"TransportSurface has {totalCount - readableCount} mesh(es) without Read/Write enabled. Only {readableCount} mesh(es) will be included in the combined MeshCollider. Enable Read/Write for all meshes for complete collision coverage.", transportSurface);
                    // Return true but warn - partial collider is better than none
                }
            }

            return true;
        }
    }
}
