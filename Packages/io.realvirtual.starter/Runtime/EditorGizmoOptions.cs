// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    //! Internal data structure for storing mesh gizmo visualization settings.
    //! Used by editor tools to track mesh selections and their visual properties for gizmo display.
    [Serializable]
    public class MeshGizmo
    {
        public List<MeshFilter> meshFilterList = new List<MeshFilter>(); //!< List of mesh filters to visualize
        public float pivotSize; //!< Size of the pivot point visualization in world units
        public bool DrawMeshPivot; //!< Enable drawing the mesh pivot point
        public bool DrawMeshCenter; //!< Enable drawing the mesh center point
        public bool DrawBoundingBox; //!< Draw bounding box instead of wire mesh
        public bool DrawLabels; //!< Draw labels for center and pivot
        public Color MeshColor; //!< Color used for mesh visualization
        public Color PivotColor; //!< Color for pivot marker
        public Color CenterColor; //!< Color for center marker
        public GameObject mainGO; //!< Main GameObject associated with this mesh gizmo
    }

    //! Configures visual appearance and behavior settings for editor gizmos across realvirtual components.
    //!
    //! This ScriptableObject stores preferences for various editor visualization tools including drive handles,
    //! kinematic tools, and move pivot functionality. Settings are accessed through realvirtualController and
    //! control both visual appearance and interaction behavior of scene gizmos.
    //!
    //! Key Features:
    //! - Drive gizmo interaction settings (arrow clicking, visual feedback)
    //! - Move pivot tool color schemes for selection states
    //! - Kinematic tool visualization colors and styles
    //!
    //! Configuration:
    //! - Created via Assets > Create > realvirtual > Add EditorGizmoOptions
    //! - Referenced by realvirtualController for global settings
    //! - Settings apply immediately to all editor gizmos in the scene
    [CreateAssetMenu(fileName = "EditorGizmoOptions", menuName = "realvirtual/Add EditorGizmoOptions", order = 1)]
    public class EditorGizmoOptions : ScriptableObject
    {
        [Header("Drive Gizmo Settings")]
        [Tooltip("Enable clicking on drive gizmo arrows to jog the drive or change direction")]
        public bool EnableDriveGizmoArrowClicking = true; //!< If true, allows clicking on drive gizmo arrows to jog drives in play mode or change direction in edit mode
        [Tooltip("Color for main direction arrow")]
        public Color DriveDirectionColor = new Color(0f, 0.7f, 1f, 1f); //!< Color for main direction arrow (default: bright blue)
        [Tooltip("Color for inactive direction arrows")]
        public Color DriveInactiveColor = new Color(0.7f, 0.7f, 0.7f, 0.4f); //!< Color for inactive direction arrows (default: light gray)
        [Tooltip("Color for running forward")]
        public Color DriveRunningForwardColor = new Color(0f, 1f, 0.3f, 1f); //!< Color for running forward (default: bright green)
        [Tooltip("Color for running in reverse")]
        public Color DriveRunningReverseColor = new Color(1f, 0.3f, 0f, 1f); //!< Color for running in reverse (default: orange-red)
        [Tooltip("Color for stopped/ready state")]
        public Color DriveStoppedColor = new Color(1f, 0.85f, 0f, 1f); //!< Color for stopped/ready state (default: golden yellow)
        [Tooltip("Color for limit indicators and arc")]
        public Color DriveLimitsColor = new Color(0.5f, 0.8f, 1f, 0.5f); //!< Color for limit indicators and arc (default: blue)
        [Tooltip("Color for limit warning")]
        public Color DriveLimitWarningColor = new Color(1f, 0.4f, 0.1f, 1f); //!< Color for limit warning (default: bright orange)
        [Tooltip("Size multiplier for direction arrow cones")]
        public float DriveConeSize = 0.36f; //!< Size multiplier for cone-shaped direction arrows (default: 0.36)
        [Tooltip("Size multiplier for position marker cubes")]
        public float DriveCubeSize = 0.24f; //!< Size multiplier for cube-shaped position markers (default: 0.24)
        [Tooltip("Distance offset from center for gizmo elements")]
        public float DriveDistanceCenter = 0.18f; //!< Distance offset from center for positioning gizmo elements (default: 0.18)
        [Tooltip("Arc radius for rotational drive visualization")]
        public float DriveArcSize = 0.96f; //!< Arc radius multiplier for rotational drive limits (default: 0.96)
        [Tooltip("Font size for drive labels")]
        public float DriveFontSize = 14f; //!< Font size for drive position and status labels (default: 14)

        [Header("Move Pivot Tool Settings")]
        [Tooltip("Color for the current pivot position")]
        public Color MovePivotCurrentColor = new Color(0.2f, 0.8f, 0.2f, 0.8f); //!< Color for visualizing the current pivot position (default: green)
        [Tooltip("Color for the preview pivot position")]
        public Color MovePivotPreviewColor = new Color(0.8f, 0.8f, 0.2f, 0.8f); //!< Color for visualizing the preview pivot position (default: yellow)
        [Tooltip("Color for highlighting vertices on hover")]
        public Color MovePivotVertexHighlightColor = new Color(0.8f, 0.3f, 0.3f, 1f); //!< Color for highlighting vertices when hovering (default: red)
        [Tooltip("Color for the first selected point")]
        public Color MovePivotPoint1Color = new Color(0.2f, 0.4f, 0.8f, 1f); //!< Color for the first selected point (default: dark blue)
        [Tooltip("Color for the second selected point")]
        public Color MovePivotPoint2Color = new Color(0.3f, 0.5f, 0.9f, 1f); //!< Color for the second selected point (default: medium blue)
        [Tooltip("Color for the third selected point")]
        public Color MovePivotPoint3Color = new Color(0.4f, 0.6f, 1.0f, 1f); //!< Color for the third selected point (default: light blue)

        [Header("Duplicate Finder Settings")]
        [Tooltip("Color for wireframe visualization of duplicate objects")]
        public Color DuplicateFinderWireframeColor = Color.yellow; //!< Color for wireframe visualization of duplicate objects (default: yellow)
        [Tooltip("Color for persistent outlines of identical objects")]
        public Color DuplicateFinderOutlineColor = Color.cyan; //!< Color for persistent outlines of identical objects (default: cyan)
        [Tooltip("Color for ping/flash effect when locating objects")]
        public Color DuplicateFinderPingColor = Color.yellow; //!< Color for ping/flash effect when locating objects (default: yellow)

        [Header("Material Window Settings")]
        [Tooltip("Color for wireframe visualization in material preview")]
        public Color MaterialWindowWireframeColor = Color.yellow; //!< Color for wireframe visualization in material preview (default: yellow)

        [Header("Kinematic Tool - Dialog Settings")]
        [Tooltip("Button background active Button")]
        public Color ActiveButtonBackground; //!< Background color for active buttons in kinematic tool dialogs

        [Header("Kinematic Tool - Scene Settings")]
        [Tooltip("Default color for the selection sphere in kinematic tool")]
        public Color DefaultColorSelectionSphere; //!< Default color for the selection sphere in kinematic tool
        [Tooltip("Color currently hovered mesh.")]
        public Color KT_HoverMeshColor; //!< Color applied to meshes when hovering in kinematic tool
        [Tooltip("Color selected mesh.")]
        public Color KT_SelectedMeshColor; //!< Color applied to selected meshes in kinematic tool
        [Tooltip("Color of axis direction line.")]
        public Color AxisColor; //!< Color for primary axis direction visualization lines
        [Tooltip("Color of axis direction line of sub objects.")]
        public Color AxisColorSecondaryAxis; //!< Color for secondary axis direction lines on child objects
        [Tooltip("Mesh color of the connected axis.")]
        public Color MeshColorConnectedAxis; //!< Color for meshes connected to the active axis
        [Tooltip("Mesh color of the upper axis.")]
        public Color MeshColorUpperAxis; //!< Color for meshes on upper hierarchy axes
        [Tooltip("Color for Point 1 in radius center selection.")]
        public Color KT_Point1Color = Color.yellow; //!< Color for Point 1 in radius center selection (default: yellow)
        [Tooltip("Color for Point 2 in radius center selection.")]
        public Color KT_Point2Color = new Color(0.5f, 0.5f, 0.5f, 1f); //!< Color for Point 2 in radius center selection (default: gray)
        [Tooltip("Color for Point 3 in radius center selection.")]
        public Color KT_Point3Color = new Color(1f, 0.5f, 0f, 1f); //!< Color for Point 3 in radius center selection (default: orange)
        [Tooltip("Color for the calculated center point.")]
        public Color KT_CenterPointColor = Color.red; //!< Color for the calculated center point (default: red)

        [HideInInspector] public List<MeshGizmo> SelectedMeshes = new List<MeshGizmo>(); //!< Internal list of currently selected meshes with their gizmo settings
       
    }
}
