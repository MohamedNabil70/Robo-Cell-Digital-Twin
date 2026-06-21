// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;


namespace realvirtual
{
#pragma warning disable 0414
    [InitializeOnLoad]
    //! Class to handle the creation of the realvirtual menu
    public class ModelChecker : EditorWindow
    {
        private Vector2 scrollPos;

        // create a list class with a hint string field and a link string field
        public class hint
        {
            public string header;
            public string text;
            public string link;
            public List<GameObject> objects;
        }

        // List for the hints
        public static List<hint> hints = new();

        // method for adding hints
        public static void AddHint(string header, string text, string link = "", List<GameObject> objects = null)
        {
            hints.Add(new hint { header = header, text = text, link = link, objects = objects });
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Apply any pending ModelChecker state change from Play mode
                var scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                var prefKey = "ModelCheckerEnabled_" + scenePath;

                if (EditorPrefs.HasKey(prefKey))
                {
                    var enabled = EditorPrefs.GetBool(prefKey);
                    var controller = FindAnyObjectByType<realvirtualController>();
                    if (controller != null)
                    {
                        controller.ModelCheckerEnabled = enabled;
                        EditorUtility.SetDirty(controller);
                    }
                    EditorPrefs.DeleteKey(prefKey);
                }
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                // Clean up any stale EditorPrefs keys if Unity is exiting play mode normally
                // This prevents stale keys from persisting after crashes
                var scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                var prefKey = "ModelCheckerEnabled_" + scenePath;
                if (!EditorPrefs.HasKey(prefKey))
                    return;
                // Key exists - will be applied in EnteredEditMode
            }
        }


        private const string SessionKeyLastCheckedScene = "rv_modelchecker_last_scene";

        [MenuItem("Tools/realvirtual/Model Checker (Alt+T) &T", false, 400)]
        public static void Init()
        {
            Check();
        }

        //! Runs model checks only if the scene has changed since the last check.
        //! Called automatically from realvirtualController.Start() on play mode entry.
        public static void InitIfSceneChanged()
        {
            var scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            var lastScene = SessionState.GetString(SessionKeyLastCheckedScene, "");

            // Skip if same scene was already checked this session
            if (lastScene == scenePath)
                return;

            Check();

            SessionState.SetString(SessionKeyLastCheckedScene, scenePath);
        }

        private static void CheckNonStaticMeshes()
        {
            var finalmeshes = FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(go => go.GetComponent<MeshRenderer>() != null && !go.isStatic)
                .Where(go => go.GetComponentInParent<Drive>() == null)
                .Where(go => go.GetComponentInParent<realvirtualController>() == null)
                .Where(go => go.GetComponentInParent<MU>() == null)
                .Where(go => go.GetComponentInParent<ChainElement>() == null)
                .ToList();

            // get group names from kinematic objects (only those that also have a Group component)
            var groupNames = new HashSet<string>(
                finalmeshes
                    .Where(go => go.GetComponent<Kinematic>() != null && go.GetComponent<Group>() != null)
                    .Select(go => go.GetComponent<Group>().GetGroupName()));

            // remove objects that have a Group but whose group is NOT in the kinematic group names
            finalmeshes.RemoveAll(go =>
            {
                var group = go.GetComponent<Group>();
                return group != null && !groupNames.Contains(group.GetGroupName());
            });

            if (finalmeshes.Count > 0)
            {
                AddHint("Non static meshes",
                    $"There are {finalmeshes.Count} non static meshes which don't seem to move (there is no drive in a parent)." +
                    "\nSetting all non moving Gameobjects to static will increase performance.",
                    "https://doc.realvirtual.io/advanced-topics/improving-performance#gameobject-static-settings",
                    finalmeshes);
            }
        }

        private static void CheckStaticMeshes()
        {
            var staticMeshesWithDrive = FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(go => go.GetComponent<MeshRenderer>() != null && go.isStatic)
                .Where(go => go.GetComponentInParent<Drive>() != null)
                .Where(go => go.GetComponentInParent<Drive>().GetTransportSurfaces().Count == 0)
                .ToList();

            if (staticMeshesWithDrive.Count > 0)
            {
                AddHint("Static Meshes at Drives",
                    $"There are {staticMeshesWithDrive.Count} static meshes as children of Drives." +
                    "\nThis will prevent the meshes from moving.",
                    "https://doc.realvirtual.io/components-and-scripts/motion/drive", staticMeshesWithDrive);
            }
        }

        private static void HugeMeshes()
        {
            // collect mesh filters excluding realvirtualController children, cache vertex counts
            var meshData = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
                .Where(mf => mf.sharedMesh != null && mf.GetComponentInParent<realvirtualController>() == null)
                .Select(mf => new { go = mf.gameObject, vertices = mf.sharedMesh.vertexCount })
                .ToList();

            if (meshData.Count == 0) return;

            var totalVertices = meshData.Sum(m => m.vertices);
            var threshold = totalVertices * 0.05f;

            var bigobjects = meshData
                .Where(m => m.vertices > threshold)
                .Select(m => m.go)
                .ToList();

            if (bigobjects.Count > 0)
            {
                AddHint("Massive Meshes",
                    $"The scene contains {bigobjects.Count} object(s) with a significantly larger number of vertices (>5% of total) compared to others." +
                    "\nFor large-scale models, consider optimization strategies or reducing the vertex count to enhance performance."+
                    "\nFor more information you can use CADChecker (Pro) for getting more insights into the model mesh data.",
                    "https://doc.realvirtual.io/advanced-topics/improving-performance#simplifying-meshes", bigobjects);
            }
        }

        private static void NumberOfMeshes()
        {
            var totalMeshes = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Count(mr => mr.enabled);

            if (totalMeshes > 1000)
            {
                AddHint("Number of Meshes",
                    $"The scene contains {totalMeshes} meshes.\nConsider using the Performance Optimizer (Pro) to combine meshes and improve performance." +
                    "\nYou can also use the complexity of meshes or delete unnecessary meshes.",
                    "https://doc.realvirtual.io/advanced-topics/improving-performance#performance-optimizer-only-included-in-professional-version");
            }
        }

        private static void NonSharedMaterials()
        {
            var materialCount = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Select(mr => mr.sharedMaterial)
                .Distinct()
                .Count();

            if (materialCount > 20)
            {
                AddHint("Many materials",
                    $"There are {materialCount} distinct materials in your scene." +
                    "\nTo enhance performance, consider minimizing the number of materials for example by assigning Material Assets to the GameObjects.",
                    "https://doc.realvirtual.io/advanced-topics/improving-performance",
                    null);
            }
        }

        private static void MUsWithoutRigidbody()
        {
            var musWithoutRigidbody = FindObjectsByType<MU>(FindObjectsSortMode.None)
                .Where(mu => mu.GetComponent<Rigidbody>() == null
                          && mu.GetComponent("DESMU") == null)  // DES MUs use event-driven positioning, no Rigidbody needed
                .Select(mu => mu.gameObject)
                .ToList();

            if (musWithoutRigidbody.Count > 0)
            {
                AddHint("MUs without Rigidbody",
                    $"There are {musWithoutRigidbody.Count} MUs without a Rigidbody." +
                    "\nTo enable physics interactions, consider adding a Rigidbody component to the MUs.",
                    "https://doc.realvirtual.io/components-and-scripts/motion/mu",
                    musWithoutRigidbody);
            }
        }

        private static void GuidedTransport()
        {
            // Check if there are any guided transports in the scene
            var guidedTransport = FindObjectsByType<TransportGuided>(FindObjectsSortMode.None);

            if (guidedTransport.Length == 0)
                return;
            
            var mus = FindObjectsByType<MU>(FindObjectsSortMode.None);
            
            var musObjects = mus.Select(mu => mu.gameObject).ToList();
            
            var musObjectsWithoutGuidedMU = musObjects.Where(mu => mu.GetComponent<GuidedMU>() == null).ToList();
            
            if (musObjectsWithoutGuidedMU.Any())
            {
                AddHint("Guided Transport",
                    $"There are {musObjectsWithoutGuidedMU.Count} MUs without a GuidedMU component." +
                    "\nThere are Guided Transportsurfaces in the scene. \nTo enable guided transport, consider adding a GuidedMU component to the MUs.",
                    "https://doc.realvirtual.io/components-and-scripts/motion/guided-transport#prerequisites-guidedmu",
                    musObjectsWithoutGuidedMU);
            }
        }

        private static void SensorRaycastLayer()
        {
            var rvMULayer = LayerMask.NameToLayer("rvMU");
            var rvMUSensorLayer = LayerMask.NameToLayer("rvMUSensor");

            var badSensors = FindObjectsByType<Sensor>(FindObjectsSortMode.None)
                .Where(sensor => sensor.UseRaycast)
                .Where(sensor => !sensor.AdditionalRayCastLayers.Contains("rvMU") &&
                                 !sensor.AdditionalRayCastLayers.Contains("rvMUSensor"))
                .Where(sensor => sensor.gameObject.layer != rvMULayer &&
                                 sensor.gameObject.layer != rvMUSensorLayer)
                .Select(sensor => sensor.gameObject)
                .ToList();

            if (badSensors.Count > 0)
            {
                AddHint("Sensor Layer",
                    $"There are {badSensors.Count} Raycast Sensors without the standard layers rvMU or rvMUSensor." +
                    "\nThis might be a custom implementation and it could be ok.\nTo enable collision Sensor detection of MUs, consider adding the layers rvMU and rvMUSensor to the AdditionalRaycastLayers.",
                    "https://doc.realvirtual.io/components-and-scripts/sensor#sensor-using-raycasts",
                    badSensors);
            }
        }

        private static void CheckSinkLayer()
        {
            var rvSensorLayer = LayerMask.NameToLayer("rvSensor");
            var sinksWithoutLayer = FindObjectsByType<Sink>(FindObjectsSortMode.None)
                .Where(sink => sink.gameObject.layer != rvSensorLayer)
                .Select(sink => sink.gameObject)
                .ToList();

            if (sinksWithoutLayer.Count > 0)
            {
                AddHint("Sink Layer",
                    $"There are {sinksWithoutLayer.Count} Sinks without the standard layer rvSensor." +
                    "\nTo enable collision detection of MUs, consider using the layers rvSensor.",
                    "https://doc.realvirtual.io/components-and-scripts/sink",
                    sinksWithoutLayer);
            }
        }

        private static void SensorColliderLayer()
        {
            var rvSensorLayer = LayerMask.NameToLayer("rvSensor");
            var colliderSensorsWithoutLayer = FindObjectsByType<Sensor>(FindObjectsSortMode.None)
                .Where(sensor => !sensor.UseRaycast)
                .Where(sensor => sensor.gameObject.layer != rvSensorLayer)
                .Select(sensor => sensor.gameObject)
                .ToList();

            if (colliderSensorsWithoutLayer.Count > 0)
            {
                AddHint("Sensor Layer",
                    $"There are {colliderSensorsWithoutLayer.Count} Collider Sensors without the standard layer rvSensor." +
                    "\nTo enable collision Sensor detection of MUs, consider using the layer rvSensor.",
                    "https://doc.realvirtual.io/components-and-scripts/sensor#sensor-using-colliders",
                    colliderSensorsWithoutLayer);
            }
        }

        private static void ComplexColliders()
        {
            var meshColliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
            var count = meshColliders.Length;

            if (count > 20)
            {
                AddHint("Complex Colliders",
                    $"There are {count} Mesh Colliders in the scene." +
                    "\nTo enhance performance, consider using less or simpler colliders like Box Colliders.",
                    "https://doc.realvirtual.io/advanced-topics/improving-performance#colliders",
                    meshColliders.Select(mc => mc.gameObject).ToList());
            }
        }

        private static void ManyColliders()
        {
            var allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            var count = allColliders.Length;

            if (count > 300)
            {
                AddHint("Many Colliders",
                    $"There are {count} Colliders in the scene." +
                    "\nTo enhance performance, consider using less colliders if possible.",
                    "https://doc.realvirtual.io/advanced-topics/improving-performance#colliders",
                    allColliders.Select(mc => mc.gameObject).ToList());
            }
        }

        private static void CollidersOnNonStandardLayers()
        {
            var rvMU = LayerMask.NameToLayer("rvMU");
            var rvMUSensor = LayerMask.NameToLayer("rvMUSensor");
            var rvTransport = LayerMask.NameToLayer("rvTransport");
            var rvSelection = LayerMask.NameToLayer("rvSelection");
            var rvSensor = LayerMask.NameToLayer("rvSensor");

            var badColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.gameObject.layer != rvMU &&
                            c.gameObject.layer != rvMUSensor &&
                            c.gameObject.layer != rvTransport &&
                            c.gameObject.layer != rvSelection &&
                            c.gameObject.layer != rvSensor)
                .Select(c => c.gameObject)
                .ToList();

            if (badColliders.Count > 0)
            {
                AddHint("Collider Layer",
                    $"There are {badColliders.Count} Colliders in the scene which are not on the standard layers." +
                    "\nFor good performance collision detections it is recommended to use standard layers",
                    "https://doc.realvirtual.io/advanced-topics/improving-performance#colliders",
                    badColliders);
            }
        }

        private static void NumberOfLights()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var count = lights.Length;

            if (count > 2)
            {
                AddHint("Number of Lights",
                    $"There are {count} Lights in the scene." +
                    "\nTo enhance performance, consider using less lights if possible.\nTurning off shadows also improves performance.",
                    "https://doc.realvirtual.io/advanced-topics/improving-performance#lights-and-shadows",
                    lights.Select(l => l.gameObject).ToList());
            }
        }

        private static void PerformChecks()
        {
            CheckNonStaticMeshes();
            CheckStaticMeshes();
            HugeMeshes();
            NumberOfMeshes();
            NonSharedMaterials();
            MUsWithoutRigidbody();
            GuidedTransport();
            SensorRaycastLayer();
            SensorColliderLayer();
            CheckSinkLayer();
            ComplexColliders();
            ManyColliders();
            NumberOfLights();
            CollidersOnNonStandardLayers();
        }

        public static void Check()
        {
            hints.Clear();

            AddHint("Model Checker",
                "The Modelchecker performs a pre-scene check to identify common issues and suggest performance optimizations.\nYou can enable or disable it in the realvirtualController.",
                "https://doc.realvirtual.io");

            PerformChecks();

            if (hints.Count == 1)
                AddHint("Check Finished", "Congratulation, there is no issue in your current scene.", "");
            else
            {
                AddHint("Check Finished", $"There are {hints.Count - 1} issues in your current scene.", "");
            }

            if (hints.Count > 1)
            {
                var window =
                    (ModelChecker)GetWindow(typeof(ModelChecker));

                window.Show();
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
            EditorGUILayout.Separator();
            foreach (var hint in hints)
            {
                EditorGUILayout.LabelField(hint.header, EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                var textContent = new GUIContent(hint.text);
                GUI.skin.label.wordWrap = true;
                var size = GUI.skin.label.CalcHeight(textContent, position.width * 0.8f);
                EditorGUILayout.LabelField(textContent, GUILayout.MaxWidth(position.width * 0.8f),
                    GUILayout.Height(size), GUILayout.ExpandWidth(true));
                GUILayout.FlexibleSpace();
                if (hint.objects != null && hint.objects.Count > 0 && GUILayout.Button("Show ", GUILayout.Width(50)))
                {
                    EditorApplication.delayCall += () => Selection.objects = hint.objects.ToArray();
                }

                if (!string.IsNullOrEmpty(hint.link) && GUILayout.Button("Info", GUILayout.Width(50)))
                    Application.OpenURL(hint.link);

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Separator();
            }

            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("Update Check"))
            {
                Check();
            }

            // Check if enabled if not show button for enable

            // get the realvirtualController
            var controller = FindAnyObjectByType<realvirtualController>();
            if (controller != null)
            {
                if (controller.ModelCheckerEnabled)
                {
                    if (GUILayout.Button("Disable checks for current scene"))
                    {
                        controller.ModelCheckerEnabled = false;
                        
                        if (Application.isPlaying)
                        {
                            // In Play mode, save to EditorPrefs for later restoration in Edit mode
                            var scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                            var prefKey = "ModelCheckerEnabled_" + scenePath;
                            EditorPrefs.SetBool(prefKey, false);
                        }
                        else
                        {
                            // In Edit mode, mark the controller as dirty to save to scene
                            EditorUtility.SetDirty(controller);
                        }
                        Close();
                    }
                }
                else
                {
                    if (GUILayout.Button("Enable checks for current scene"))
                    {
                        controller.ModelCheckerEnabled = true;
                        
                        if (Application.isPlaying)
                        {
                            // In Play mode, save to EditorPrefs for later restoration in Edit mode
                            var scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                            var prefKey = "ModelCheckerEnabled_" + scenePath;
                            EditorPrefs.SetBool(prefKey, true);
                        }
                        else
                        {
                            // In Edit mode, mark the controller as dirty to save to scene
                            EditorUtility.SetDirty(controller);
                        }
                    }
                }
            }


            if (GUILayout.Button("Close"))
            {
                Close();
            }
            
            // make a distance
            EditorGUILayout.Separator();


        }
    }
}
#endif