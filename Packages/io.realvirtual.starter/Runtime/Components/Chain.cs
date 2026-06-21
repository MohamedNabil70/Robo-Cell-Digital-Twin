// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;
#if REALVIRTUAL_BURST
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Jobs;
#endif

namespace realvirtual
{
    public enum ChainOrientation
    {
          Horizontal,
            Vertical
    }    
    #region doc
    //! Chain component creates continuous loop transport systems with elements following spline-defined paths in industrial automation.
    
    //! The Chain component is designed for simulating chain-driven transport systems where individual elements
    //! (links, buckets, carriers, or fixtures) move along a continuous path. It automatically generates and
    //! positions chain elements along spline curves, providing realistic visualization and physics simulation
    //! of chain conveyors, bucket elevators, overhead conveyors, and similar transport mechanisms.
    //!
    //! Key features:
    //! - Automatic generation of chain elements along spline paths
    //! - Unity Splines backend for spline path definition
    //! - Configurable element spacing with automatic or manual distribution
    //! - Horizontal and vertical chain orientations for different applications
    //! - Edit-mode preview for design-time visualization
    //! - Integration with Drive components for speed control
    //! - Scalable chain length with automatic element adjustment
    //! - Support for complex 3D paths including curves and elevation changes
    //!
    //! Common applications in industrial automation:
    //! - Chain conveyors for heavy-duty material transport
    //! - Bucket elevators for vertical material handling
    //! - Overhead power and free conveyors in assembly lines
    //! - Carousel systems for buffering and accumulation
    //! - Pallet transport systems with carriers
    //! - Drag chain conveyors for bulk material
    //! - Accumulating chain conveyors with individual carriers
    //! - Festoon systems for cable management
    //!
    //! The Chain component works by:
    //! 1. Analyzing the spline path to calculate total length
    //! 2. Generating specified number of elements at calculated intervals
    //! 3. Positioning each element along the spline based on normalized position
    //! 4. Updating element positions based on drive speed during simulation
    //! 5. Maintaining proper orientation along the path tangent
    //!
    //! Chain element types and configurations:
    //! - Simple chain links for basic visualization
    //! - Buckets for bucket elevator simulation
    //! - Carriers with fixtures for workpiece transport
    //! - Pallets for automated storage systems
    //! - Custom prefabs for specialized applications
    //!
    //! Integration with other components:
    //! - Requires IChain interface implementation (ChainUnitySpline)
    //! - Connects to Drive component for movement control
    //! - Chain elements implement IChainElement for position updates
    //! - Works with sensors for element detection
    //! - Compatible with MU components for load handling
    //! - Can trigger events based on element positions
    //!
    //! Path definition options:
    //! - Unity Splines (SplineContainer) for advanced curve control — recommended and the only
    //!   officially supported path definition for new projects
    //! - Support for closed loops and open-ended paths
    //! - Multiple spline segments for complex routing
    //! - Tangent and normal control for element orientation
    //!
    //! Deprecated: the legacy point-and-tangent spline system (Spline component with P1..Pn child
    //! GameObjects holding InTangent/OutTangent transforms) is no longer maintained. It does not
    //! receive bug fixes or performance improvements. Existing scenes using it keep working, but
    //! new chains must use a Unity SplineContainer. See the Unity Splines section in the docs.
    //!
    //! Performance considerations:
    //! - Element count affects performance - optimize for visible detail
    //! - Use simplified meshes for chain elements when possible
    //! - Consider LOD systems for large chain installations
    //! - Batch element updates for better performance
    //! - Use appropriate update rates based on chain speed
    //!
    //! The Chain component provides essential functionality for industries requiring
    //! continuous material flow along defined paths, offering both visual accuracy
    //! and functional simulation capabilities.
    //!
    //! For detailed documentation and examples, see:
    //! https://doc.realvirtual.io/components-and-scripts/motion/chain
    #endregion
    [AddComponentMenu("realvirtual/Mechanical/Chain")]
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/motion/chain")]
    public class Chain : realvirtualBehavior, IXRPlaceable
    {
#if !REALVIRTUAL_BURST
  
        [HideInInspector]
        public bool _burstSetupInfo = true;

        [Button("Enable Burst Optimization (Install Package & Define)")]
        private void EnableBurstOptimization()
        {
#if UNITY_EDITOR
            // Install Burst package
            UnityEditor.PackageManager.Client.Add("com.unity.burst");
            UnityEngine.Debug.Log("Installing Unity Burst Compiler package...");

            // Enable REALVIRTUAL_BURST define
            string defines = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(
                UnityEditor.BuildTargetGroup.Standalone);

            if (!defines.Contains("REALVIRTUAL_BURST"))
            {
                if (!string.IsNullOrEmpty(defines))
                    defines += ";";
                defines += "REALVIRTUAL_BURST";

                UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(
                    UnityEditor.BuildTargetGroup.Standalone, defines);

                UnityEngine.Debug.Log("REALVIRTUAL_BURST compiler define enabled. Unity will recompile after package installation.");
            }
            else
            {
                UnityEngine.Debug.Log("REALVIRTUAL_BURST define is already enabled. Installing Burst package only.");
            }
#endif
        }
#endif

#if REALVIRTUAL_BURST
        [InfoBox("⚡ Burst Optimization Enabled\n\n" +
                 "✓ Maximum performance mode active (20-33x speedup)\n\n" +
                 "💡 Performance Tip: Set MoveRigidBody=false on ChainElements for maximum speed (29-33x) if physics interaction is not needed.\n" +
                 "Elements with MoveRigidBody=true still benefit from 19-23x speedup.",
                 EInfoBoxType.Normal)]
        [HideInInspector]
        public bool _burstEnabled = true;
#endif

        [Header("Chain Settings")]
        [Tooltip("Orientation of the chain elements along the spline")]
        public ChainOrientation chainOrientation = ChainOrientation.Horizontal; //!< Orientation of the chain (Horizontal or Vertical)
        [Tooltip("Prefab to use as chain element (e.g., chain link, bucket, carrier)")]
        public GameObject ChainElement; //!< Chainelements which needs to be created along the chain
        [Tooltip("Base name for generated chain elements")]
        public string NameChainElement; //!< Name for the chain elements
        [Tooltip("Drive component that controls the chain movement")]
        public Drive ConnectedDrive; //!< The drive which is moving this chain
        [OnValueChanged("Modify")]
        [Tooltip("Number of chain elements to create along the spline")]
        public int NumberOfElements; //!< The number of elements which needs to be created along the chain
        [Tooltip("Starting position offset in mm for the first chain element")]
        public float StartPosition; //!< The start position in millimeters on the chain (offset) for the first element
        [OnValueChanged("Modify")]
        [Tooltip("Create and position chain elements in edit mode (preview)")]
        public bool CreateElementeInEditMode = false; //!< Create chain elements in edit mode
        [Tooltip("Automatically calculate spacing based on chain length and element count")]
        public bool CalculatedDeltaPosition = true; //!< True if the distance (DeltaPosition) between the chain elements should be calculated based on number and chain length

        [Tooltip("Distance in mm between chain elements (manual setting)")]
        public float DeltaPosition; //!< Distance in millimeters between chain elements
        [Tooltip("Scale chain elements to maintain a fixed total length")]
        public bool ScaledOnFixedLength = false; //!< Scale chain to fixed length
        [ShowIf("ScaledOnFixedLength")]
        [Tooltip("Target total length of the chain in mm")]
        public float FixedLength = 1500; //!< Fixed chain length in millimeters
        [Tooltip("Minimum drive movement in millimeters before elements are updated (0 = update every drive event)")]
        [MinValue(0f)]
        public float DriveUpdateThreshold = 0f;
        [Tooltip("Minimum movement per element in millimeters before recalculating transforms (0 = update every time)")]
        [MinValue(0f)]
        public float ElementUpdateThreshold = 0f;
        [ReadOnly] public float Length; //!< The calculated length of the spline in millimeters
        [HideInInspector]public SplineContainer splineContainer;
        [HideInInspector]public bool unitySplineActive = false;
        private GameObject newbeltelement;
        private IChain ichain;
        [HideInInspector]public bool usepath = false;

        // Batch update support
        [HideInInspector] public List<ChainElement> chainElements = new List<ChainElement>();
        private bool driveEventSubscribed = false;
        private float pendingDriveDelta = 0f;
        private float lastDrivePosition = float.NaN;


        // Burst optimization fields
#if !REALVIRTUAL_BURST
        [InfoBox("⚡ Burst Optimization Not Enabled\n\n" +
           "For maximum performance (20-33x faster), install Unity Burst Compiler and enable the REALVIRTUAL_BURST compiler define.\n\n" +
           "• Kinematic elements (MoveRigidBody=false): 29-33x speedup\n" +
           "• Physics elements (MoveRigidBody=true): 19-23x speedup\n\n" +
           "💡 Performance Tip: Set MoveRigidBody=false on ChainElements for maximum speed if physics interaction is not needed.\n\n" +
           "Click the button below to enable Burst optimization.",
           EInfoBoxType.Warning)]
#endif
#if REALVIRTUAL_BURST
        [InfoBox("⚠️ Burst Optimization Available", 
                 EInfoBoxType.Warning)]
#endif
        [Tooltip("Enable Burst-optimized batch updates for better performance")]
        public bool UseBurstOptimization = true;
#if REALVIRTUAL_BURST
        // Persistent NativeArrays for Burst
        private NativeArray<float> startPositions;
        private NativeArray<float> offsetPositions;
        private NativeArray<float3> resultPositions;
        private NativeArray<quaternion> resultRotations;
        private NativeArray<float3> alignVectors;
        private NativeArray<float3> bakedSplinePositions;
        private NativeArray<float3> bakedSplineTangents;
        private bool nativeArraysInitialized = false;

        // TransformAccessArray for kinematic (non-physics) elements
        private TransformAccessArray kinematicTransforms;
        private List<ChainElement> kinematicElements = new List<ChainElement>();
        private List<ChainElement> physicsElements = new List<ChainElement>();
        private NativeArray<float> kinematicStartPositions;
        private NativeArray<float> kinematicOffsetPositions;
        private NativeArray<float3> kinematicAlignVectors;

        [Tooltip("Number of cached spline samples used by the Burst job (higher = smoother, but more memory)")]
        [MinValue(8)]
        public int SplineBakeResolution = 100;
#endif
        

        //! Initializes the chain for XR/AR placement.
        //! IMPLEMENTS IXRPlaceable::OnXRInit
        public void OnXRInit(GameObject placedobj) { }

        //! Invalidates Burst data when placement begins to prevent stale world-space positions.
        //! IMPLEMENTS IXRPlaceable::OnXRStartPlace
        public void OnXRStartPlace(GameObject placedobj)
        {
            MarkNativeDataDirty();
        }

        //! Re-initialization happens automatically on next drive event after placement ends.
        //! IMPLEMENTS IXRPlaceable::OnXREndPlace
        public void OnXREndPlace(GameObject placedobj)
        {
        }

        private void Init()
        {
            ichain = gameObject.GetComponent<IChain>();
            if (ichain != null)
            {
                usepath = ichain.UseSimulationPath();
            }

            splineContainer = GetComponent<SplineContainer>();

            if(splineContainer!=null)
            {
                unitySplineActive = true;
            }
            else
            {
                if(!usepath)
                    Logger.Warning("No SplineContainer found in Chain. Please add a Unity SplineContainer component.", this);
            }

            if (realvirtualController == null)
                realvirtualController = FindFirstObjectByType<realvirtualController>();
            
            if(ScaledOnFixedLength && FixedLength==0)
                Debug.LogError("FixedLength of "+this.gameObject.name+" is 0. Please set a value for FixedLength");
            
        }
        private void Reset()
        {
            Init();
        }
        
        private void CreateElements()
        {
            if (NameChainElement == "" && ChainElement != null)
                NameChainElement = ChainElement.name;
            var position = StartPosition;
            if (ichain != null)
            {
                Length = ichain.CalculateLength();
                if (!usepath)
                    Length = Length * realvirtualController.Scale;
            }

            // Clear old list before recreating
            chainElements.Clear();
            MarkNativeDataDirty();

            if (CalculatedDeltaPosition)
                DeltaPosition = Length / NumberOfElements;
            for (int i = 0; i < NumberOfElements; i++)
            {
                var j = i + 1;
                GameObject newelement;
                if (CreateElementeInEditMode && GetChildByName(NameChainElement + "_" + j)!=null )
                {
                    newelement = GetChildByName(NameChainElement + "_" + j);
                }
                else
                {
                    newelement = Instantiate(ChainElement, ChainElement.transform.parent);
                }
                newelement.transform.parent = this.transform;
                newelement.name = NameChainElement + "_" + j;
                var chainelement = newelement.GetComponent<IChainElement>();
                chainelement.UsePath = usepath;
                chainelement.StartPosition = position;
                chainelement.Position = position;
                chainelement.ConnectedDrive = ConnectedDrive;
                chainelement.Chain = this;
                chainelement.UseUnitySpline = unitySplineActive;
                chainelement.InitPos(position);

                // Add to batch update list
                if (chainelement is ChainElement chainElement)
                {
                    chainElements.Add(chainElement);
                    chainElement.UseBatchUpdate = true; // Signal element to skip individual subscription
                }

                position = position + DeltaPosition;
            }
        }
        
        protected override void AfterAwake()
        {
            Init();
            CreateElements();
            SubscribeToDriveEvents();
        }

        private void SubscribeToDriveEvents()
        {
            if (ConnectedDrive != null && !driveEventSubscribed)
            {
#if REALVIRTUAL_BURST
                if (UseBurstOptimization && chainElements.Count > 0)
                {
                    ConnectedDrive.OnAfterDriveCalculation.AddListener(UpdateAllElementsBurst);
                }
                else
                {
                    ConnectedDrive.OnAfterDriveCalculation.AddListener(UpdateAllElementsManaged);
                }
#else
                ConnectedDrive.OnAfterDriveCalculation.AddListener(UpdateAllElementsManaged);
#endif
                driveEventSubscribed = true;
            }
        }
       public void Modify()
        {
            if (!CreateElementeInEditMode)
            {
                var chainelements = GetComponentsInChildren<ChainElement>();
                foreach (var ele in chainelements)
                {
                    DestroyImmediate(ele.gameObject);
                }
                return;
            }
            Init();
            CreateElements();
        }

#if REALVIRTUAL_BURST
        // Helper method for NaughtyAttributes ShowIf condition
        private bool IsBurstAvailableButDisabled()
        {
            return !UseBurstOptimization;
        }
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            DriveUpdateThreshold = Mathf.Max(0f, DriveUpdateThreshold);
            ElementUpdateThreshold = Mathf.Max(0f, ElementUpdateThreshold);
#if REALVIRTUAL_BURST
            SplineBakeResolution = Mathf.Max(8, SplineBakeResolution);
            MarkNativeDataDirty();
#else
            ResetDriveUpdateState();
#endif
        }
#endif
       public Vector3 GetPosition(float normalizedposition)
        {
            if (ichain != null)
                return ichain.GetPosition(normalizedposition,true);
            else
            {
                return Vector3.zero;
            }
        }
        public Vector3 GetTangent(float normalizedposition)
        {
            if (ichain != null)
                return ichain.GetDirection(normalizedposition,true);
            else
            {
                return Vector3.zero;
            }
            
        }
        public Vector3 GetUpDirection(float normalizedposition)
        {
            if (ichain != null)
                return ichain.GetUpDirection(normalizedposition,true);
            else
            {
                return Vector3.zero;
            }
        }

        private void ResetDriveUpdateState()
        {
            pendingDriveDelta = 0f;
            lastDrivePosition = float.NaN;
        }

        private bool ShouldSkipDriveUpdate(float drivePosition)
        {
            if (float.IsNaN(lastDrivePosition))
            {
                lastDrivePosition = drivePosition;
                pendingDriveDelta = 0f;
                return false;
            }

            pendingDriveDelta += Mathf.Abs(drivePosition - lastDrivePosition);
            lastDrivePosition = drivePosition;

            if (DriveUpdateThreshold <= 0f)
            {
                pendingDriveDelta = 0f;
                return false;
            }

            if (pendingDriveDelta < DriveUpdateThreshold)
            {
                return true;
            }

            pendingDriveDelta = 0f;
            return false;
        }

        private void MarkNativeDataDirty()
        {
            ResetDriveUpdateState();
#if REALVIRTUAL_BURST
            if (nativeArraysInitialized)
            {
                DisposeNativeArrays();
            }
#endif
        }

        // Managed batch update (fallback when Burst is disabled)
        private void UpdateAllElementsManaged(Drive drive)
        {
            if (chainElements == null || chainElements.Count == 0)
                return;

            float drivePosition = drive.CurrentPosition;
            if (ShouldSkipDriveUpdate(drivePosition))
                return;

            // Single loop with optimal cache access pattern
            for (int i = 0; i < chainElements.Count; i++)
            {
                var element = chainElements[i];
                if (element != null && element.isActiveAndEnabled)
                {
                    // Calculate position once per element
                    float position = drivePosition + element.StartPosition + element.OffsetToDrivePosition;
                    element.UpdatePositionBatch(position);
                }
            }
        }

        // Public API for dynamic element management
        public void RegisterElement(ChainElement element)
        {
            if (!chainElements.Contains(element))
            {
                chainElements.Add(element);
                element.UseBatchUpdate = true;
                MarkNativeDataDirty();
            }
        }

        public void UnregisterElement(ChainElement element)
        {
            if (chainElements.Remove(element))
            {
                MarkNativeDataDirty();
            }
        }

#if REALVIRTUAL_BURST
        private int GetBakeResolution()
        {
            return Mathf.Max(8, SplineBakeResolution);
        }

        private int CalculateBatchSize(int elementCount)
        {
            int workerCount = math.max(1, JobsUtility.JobWorkerCount);
            int perWorker = math.max(1, elementCount / workerCount);
            return math.clamp(perWorker, 1, 64);
        }

        // Initialize NativeArrays for Burst
        private void InitializeNativeArrays()
        {
            if (nativeArraysInitialized || chainElements.Count == 0)
                return;

            // Split elements into kinematic (non-physics) and physics groups
            kinematicElements.Clear();
            physicsElements.Clear();

            foreach (var element in chainElements)
            {
                if (element != null)
                {
                    if (element.MoveRigidBody)
                        physicsElements.Add(element);
                    else
                        kinematicElements.Add(element);
                }
            }

            // Initialize TransformAccessArray for kinematic elements (direct Burst access)
            if (kinematicElements.Count > 0)
            {
                Transform[] transforms = new Transform[kinematicElements.Count];
                for (int i = 0; i < kinematicElements.Count; i++)
                {
                    transforms[i] = kinematicElements[i].transform;
                }

                kinematicTransforms = new TransformAccessArray(transforms);
                kinematicStartPositions = new NativeArray<float>(kinematicElements.Count, Allocator.Persistent);
                kinematicOffsetPositions = new NativeArray<float>(kinematicElements.Count, Allocator.Persistent);
                kinematicAlignVectors = new NativeArray<float3>(kinematicElements.Count, Allocator.Persistent);

                for (int i = 0; i < kinematicElements.Count; i++)
                {
                    kinematicStartPositions[i] = kinematicElements[i].StartPosition;
                    kinematicOffsetPositions[i] = kinematicElements[i].OffsetToDrivePosition;

                    // Compute world-space align vector for this element
                    Vector3 alignVector = kinematicElements[i].AlignVector;
                    if (kinematicElements[i].AlignObjectLocalZUp != null)
                    {
                        alignVector = kinematicElements[i].transform.TransformDirection(
                            kinematicElements[i].AlignObjectLocalZUp.transform.forward);
                    }
                    kinematicAlignVectors[i] = new float3(alignVector.x, alignVector.y, alignVector.z);
                }
            }

            // Initialize arrays for physics elements (traditional approach)
            if (physicsElements.Count > 0)
            {
                startPositions = new NativeArray<float>(physicsElements.Count, Allocator.Persistent);
                offsetPositions = new NativeArray<float>(physicsElements.Count, Allocator.Persistent);
                resultPositions = new NativeArray<float3>(physicsElements.Count, Allocator.Persistent);
                resultRotations = new NativeArray<quaternion>(physicsElements.Count, Allocator.Persistent);
                alignVectors = new NativeArray<float3>(physicsElements.Count, Allocator.Persistent);

                for (int i = 0; i < physicsElements.Count; i++)
                {
                    startPositions[i] = physicsElements[i].StartPosition;
                    offsetPositions[i] = physicsElements[i].OffsetToDrivePosition;

                    // Compute world-space align vector for this element
                    Vector3 alignVector = physicsElements[i].AlignVector;
                    if (physicsElements[i].AlignObjectLocalZUp != null)
                    {
                        alignVector = physicsElements[i].transform.TransformDirection(
                            physicsElements[i].AlignObjectLocalZUp.transform.forward);
                    }
                    alignVectors[i] = new float3(alignVector.x, alignVector.y, alignVector.z);
                }
            }

            // Bake spline data (shared by both paths)
            BakeSplineData();

            nativeArraysInitialized = true;
        }

        // Bake spline data for Burst-compatible access
        private void BakeSplineData()
        {
            int resolution = GetBakeResolution();

            if (bakedSplinePositions.IsCreated) bakedSplinePositions.Dispose();
            if (bakedSplineTangents.IsCreated) bakedSplineTangents.Dispose();

            bakedSplinePositions = new NativeArray<float3>(resolution, Allocator.Persistent);
            bakedSplineTangents = new NativeArray<float3>(resolution, Allocator.Persistent);

            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                Vector3 pos = GetPosition(t);
                Vector3 tangent = GetTangent(t);

                bakedSplinePositions[i] = new float3(pos.x, pos.y, pos.z);
                bakedSplineTangents[i] = math.normalize(new float3(tangent.x, tangent.y, tangent.z));
            }
        }

        // Burst-optimized batch update (hybrid: TransformAccessArray for kinematic, traditional for physics)
        private void UpdateAllElementsBurst(Drive drive)
        {
            if (chainElements == null || chainElements.Count == 0)
                return;

            float drivePosition = drive.CurrentPosition;
            if (ShouldSkipDriveUpdate(drivePosition))
                return;

            if (!nativeArraysInitialized)
                InitializeNativeArrays();

            float relevantLength = ScaledOnFixedLength ? FixedLength : Length;
            int bakeResolution = GetBakeResolution();

            // Pre-compute inverse for multiplication (faster than division in Burst)
            float invLength = relevantLength > 0.001f ? 1f / relevantLength : 0f;

            JobHandle kinematicHandle = default;
            JobHandle physicsHandle = default;
            bool hasKinematicJob = false;
            bool hasPhysicsJob = false;

            // Schedule kinematic elements job (direct transform writes - fastest)
            if (kinematicElements.Count > 0 && kinematicTransforms.isCreated)
            {
                var kinematicJob = new UpdateKinematicTransformsJob
                {
                    startPositions = kinematicStartPositions,
                    offsetPositions = kinematicOffsetPositions,
                    alignVectors = kinematicAlignVectors,
                    drivePosition = drivePosition,
                    invChainLength = invLength,
                    bakedSplinePositions = bakedSplinePositions,
                    bakedSplineTangents = bakedSplineTangents,
                    splineResolution = bakeResolution,
                    isVerticalChain = chainOrientation == ChainOrientation.Vertical
                };

                kinematicHandle = kinematicJob.Schedule(kinematicTransforms);
                hasKinematicJob = true;
            }

            // Schedule physics elements job (traditional Rigidbody approach)
            if (physicsElements.Count > 0)
            {
                var physicsJob = new UpdateChainElementsJob
                {
                    startPositions = startPositions,
                    offsetPositions = offsetPositions,
                    alignVectors = alignVectors,
                    drivePosition = drivePosition,
                    chainLength = relevantLength,
                    invChainLength = invLength,
                    bakedSplinePositions = bakedSplinePositions,
                    bakedSplineTangents = bakedSplineTangents,
                    splineResolution = bakeResolution,
                    resultPositions = resultPositions,
                    resultRotations = resultRotations,
                    isVerticalChain = chainOrientation == ChainOrientation.Vertical
                };

                physicsHandle = physicsJob.Schedule(physicsElements.Count, CalculateBatchSize(physicsElements.Count));
                hasPhysicsJob = true;
            }

            // Complete all jobs
            if (hasKinematicJob && hasPhysicsJob)
            {
                JobHandle.CompleteAll(ref kinematicHandle, ref physicsHandle);
            }
            else if (hasKinematicJob)
            {
                kinematicHandle.Complete();
            }
            else if (hasPhysicsJob)
            {
                physicsHandle.Complete();
            }

            // Apply results to physics elements (kinematic elements already updated by job)
            if (hasPhysicsJob)
            {
                for (int i = 0; i < physicsElements.Count; i++)
                {
                    var element = physicsElements[i];
                    if (element != null && element.isActiveAndEnabled)
                    {
                        element.ApplyBurstResult(resultPositions[i], resultRotations[i]);
                    }
                }
            }
        }

        // Cleanup
        private void OnDisable()
        {
            ResetDriveUpdateState();
            DisposeNativeArrays();

            if (ConnectedDrive != null && driveEventSubscribed)
            {
                ConnectedDrive.OnAfterDriveCalculation.RemoveListener(UpdateAllElementsBurst);
                ConnectedDrive.OnAfterDriveCalculation.RemoveListener(UpdateAllElementsManaged);
                driveEventSubscribed = false;
            }
        }

        private void DisposeNativeArrays()
        {
            // Dispose physics element arrays
            if (startPositions.IsCreated) startPositions.Dispose();
            if (offsetPositions.IsCreated) offsetPositions.Dispose();
            if (resultPositions.IsCreated) resultPositions.Dispose();
            if (resultRotations.IsCreated) resultRotations.Dispose();
            if (alignVectors.IsCreated) alignVectors.Dispose();

            // Dispose kinematic element arrays
            if (kinematicStartPositions.IsCreated) kinematicStartPositions.Dispose();
            if (kinematicOffsetPositions.IsCreated) kinematicOffsetPositions.Dispose();
            if (kinematicAlignVectors.IsCreated) kinematicAlignVectors.Dispose();
            if (kinematicTransforms.isCreated) kinematicTransforms.Dispose();

            // Dispose shared spline data
            if (bakedSplinePositions.IsCreated) bakedSplinePositions.Dispose();
            if (bakedSplineTangents.IsCreated) bakedSplineTangents.Dispose();

            nativeArraysInitialized = false;
        }
#endif


    }

#if REALVIRTUAL_BURST
    // Burst-compiled job for kinematic element transforms (direct transform writes)
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct UpdateKinematicTransformsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float> startPositions;
        [ReadOnly] public NativeArray<float> offsetPositions;
        [ReadOnly] public NativeArray<float3> alignVectors;
        [ReadOnly] public float drivePosition;
        [ReadOnly] public float invChainLength;

        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> bakedSplinePositions;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> bakedSplineTangents;
        [ReadOnly] public int splineResolution;
        [ReadOnly] public bool isVerticalChain;

        public void Execute(int index, TransformAccess transform)
        {
            // Calculate position
            float position = drivePosition + startPositions[index] + offsetPositions[index];

            // Normalize position to [0, 1] range
            float relativePos = position * invChainLength;
            relativePos = relativePos - math.floor(relativePos);
            relativePos = math.clamp(relativePos, 0f, 1f);

            // Interpolate spline data
            float fIndex = relativePos * (splineResolution - 1);
            int i0 = math.clamp((int)math.floor(fIndex), 0, splineResolution - 1);
            int i1 = math.clamp(i0 + 1, 0, splineResolution - 1);
            float t = math.clamp(fIndex - i0, 0f, 1f);

            // Lerp position and tangent
            float3 pos = math.lerp(bakedSplinePositions[i0], bakedSplinePositions[i1], t);
            float3 tangent = math.normalizesafe(math.lerp(bakedSplineTangents[i0], bakedSplineTangents[i1], t));

            // Use per-element align vector for rotation
            float3 align = alignVectors[index];

            // Apply vertical chain flip logic (matches ChainElement.cs)
            if (isVerticalChain)
            {
                if (tangent.z < 0 || (tangent.z == 0 && tangent.x > 0))
                {
                    align = -align;
                }
            }

            quaternion rot = quaternion.LookRotationSafe(tangent, align);

            // Direct transform write from Burst (30-50% faster than managed)
            transform.position = pos;
            transform.rotation = rot;
        }
    }

    // Burst-compiled job for physics element updates (traditional approach)
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct UpdateChainElementsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> startPositions;
        [ReadOnly] public NativeArray<float> offsetPositions;
        [ReadOnly] public NativeArray<float3> alignVectors;
        [ReadOnly] public float drivePosition;
        [ReadOnly] public float chainLength;

        // Shared spline data accessed by all parallel workers - disable parallel restriction
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> bakedSplinePositions;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> bakedSplineTangents;

        [ReadOnly] public int splineResolution;
        [ReadOnly] public float invChainLength; // Pre-computed inverse for faster multiplication
        [ReadOnly] public bool isVerticalChain;

        [WriteOnly] public NativeArray<float3> resultPositions;
        [WriteOnly] public NativeArray<quaternion> resultRotations;

        public void Execute(int index)
        {
            // Calculate position
            float position = drivePosition + startPositions[index] + offsetPositions[index];

            // Normalize position to [0, 1] range with safety checks
            // Use multiplication instead of division (2-5% faster)
            float relativePos = position * invChainLength;
            relativePos = relativePos - math.floor(relativePos); // Wrap to 0-1
            relativePos = math.clamp(relativePos, 0f, 1f); // Clamp for floating point precision

            // Interpolate spline data using baked samples
            float fIndex = relativePos * (splineResolution - 1);

            // Clamp indices to valid range to prevent out-of-bounds access
            int i0 = math.clamp((int)math.floor(fIndex), 0, splineResolution - 1);
            int i1 = math.clamp(i0 + 1, 0, splineResolution - 1);
            float t = math.clamp(fIndex - i0, 0f, 1f);

            // Lerp position (SIMD-optimized)
            float3 pos = math.lerp(bakedSplinePositions[i0], bakedSplinePositions[i1], t);

            // Lerp pre-normalized tangents and normalize result
            // Note: Tangents are normalized during baking, but lerp result needs normalization
            float3 tangent = math.normalizesafe(math.lerp(bakedSplineTangents[i0], bakedSplineTangents[i1], t));

            // Use per-element align vector for rotation
            float3 align = alignVectors[index];

            // Apply vertical chain flip logic (matches ChainElement.cs)
            if (isVerticalChain)
            {
                if (tangent.z < 0 || (tangent.z == 0 && tangent.x > 0))
                {
                    align = -align;
                }
            }

            quaternion rot = quaternion.LookRotationSafe(tangent, align);

            resultPositions[index] = pos;
            resultRotations[index] = rot;
        }
    }
#endif
}
