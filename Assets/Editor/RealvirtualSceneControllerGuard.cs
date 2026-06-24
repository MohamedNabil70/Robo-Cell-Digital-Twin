#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using realvirtual;

/// <summary>
/// Keeps Test scene's original camera/UI hierarchy while supplying only the
/// core realvirtual controller required by Drive, Sensor and Fixer components.
/// </summary>
[InitializeOnLoad]
internal static class RealvirtualSceneControllerGuard
{
    const string TargetScenePath = "Assets/Scenes/Test.unity";
    const string ControllerPrefabPath =
        "Packages/io.realvirtual.starter/Assets/Prefabs/realvirtual.prefab";
    const string DemoRobotPrefabPath =
        "Packages/io.realvirtual.starter/Runtime/DataForDemos/Prefabs/DemoCellRobot.prefab";

    static bool repairing;

    static RealvirtualSceneControllerGuard()
    {
        EditorApplication.delayCall += RepairOpenTargetScene;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            EditorApplication.delayCall += () => RepairScene(scene);
    }

    static void RepairOpenTargetScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
            RepairScene(SceneManager.GetSceneAt(i));
    }

    static void RepairScene(Scene scene)
    {
        if (repairing || !scene.IsValid() || !scene.isLoaded ||
            scene.path != TargetScenePath || !UsesRealvirtual(scene))
            return;

        repairing = true;
        try
        {
            bool changed = false;
            GameObject controllerInstance = null;

            int removedFullPrefabs = RemoveFullControllerPrefabs(scene);
            changed |= removedFullPrefabs > 0;

            if (!HasController(scene))
            {
                controllerInstance = new GameObject("realvirtualController (Core Only)");
                SceneManager.MoveGameObjectToScene(controllerInstance, scene);
                Undo.RegisterCreatedObjectUndo(controllerInstance,
                                               "Add required realvirtual controller");
                realvirtualController controller =
                    Undo.AddComponent<realvirtualController>(controllerInstance);
                ConfigureCoreController(controller);
                changed = true;
            }

            int disabledRecorders = DisableInvalidAutoReplay(scene);
            changed |= disabledRecorders > 0;

            if (!changed)
                return;

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError($"[SCENE REPAIR] Repaired '{scene.path}', but Unity could not " +
                               "save it. Save the scene manually.");
                return;
            }

            if (controllerInstance != null)
                Debug.Log($"[SCENE REPAIR] Added a camera-free core realvirtual controller to " +
                          $"'{scene.path}'.", controllerInstance);
            if (removedFullPrefabs > 0)
                Debug.Log($"[SCENE REPAIR] Removed {removedFullPrefabs} full realvirtual " +
                          "prefab instance(s), including their extra cameras and runtime UI.");
            if (disabledRecorders > 0)
                Debug.Log($"[SCENE REPAIR] Disabled Play On Start on {disabledRecorders} " +
                          "demo DrivesRecorder component(s); their bundled recording does not " +
                          "match these customized robot instances.");

            Debug.Log($"[SCENE REPAIR] Saved repaired scene '{scene.path}'.");
        }
        finally
        {
            repairing = false;
        }
    }

    static bool HasController(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<realvirtualController>(true) != null)
                return true;
        }

        return false;
    }

    static int RemoveFullControllerPrefabs(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) !=
                ControllerPrefabPath)
                continue;

            Undo.DestroyObjectImmediate(root);
            count++;
        }

        return count;
    }

    static void ConfigureCoreController(realvirtualController controller)
    {
        controller.Connected = false;
        controller.ModelCheckerEnabled = false;
        controller.ValidationOnComponentsAdded = false;
        controller.ValidateBeforeStart = false;
        controller.AdditiveLoadScenes = false;
        controller.EnablePositionDebug = false;
        controller.ShowHierarchyIcons = false;
        controller.ShowComponents = false;
        controller.EnableHotkeys = false;
        controller.UIEnabledOnStart = false;
        controller.RuntimeInspectorEnabled = false;
        controller.ObjectSelectionEnabled = false;
        controller.HideInfoBox = true;
        controller.StandardSource = null;
        controller.EditorGizmoSettings = null;
        controller.RuntimeApplicationUI = null;
        controller.RuntimeAutomationUI = null;
        controller.InspectorController = null;
        EditorUtility.SetDirty(controller);
    }

    static bool UsesRealvirtual(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            realvirtualBehavior[] behaviours =
                root.GetComponentsInChildren<realvirtualBehavior>(true);
            if (behaviours.Length > 0)
                return true;
        }

        return false;
    }

    static int DisableInvalidAutoReplay(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (DrivesRecorder recorder in
                     root.GetComponentsInChildren<DrivesRecorder>(true))
            {
                if (!recorder.PlayOnStart)
                    continue;

                DrivesRecorder sourceRecorder =
                    PrefabUtility.GetCorrespondingObjectFromSource(recorder) as DrivesRecorder;
                if (sourceRecorder == null ||
                    AssetDatabase.GetAssetPath(sourceRecorder) != DemoRobotPrefabPath)
                    continue;

                Undo.RecordObject(recorder, "Disable invalid demo drive replay");
                recorder.PlayOnStart = false;
                EditorUtility.SetDirty(recorder);
                count++;
            }
        }

        return count;
    }
}
#endif
