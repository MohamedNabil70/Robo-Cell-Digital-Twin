#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only helper for giving an imported mesh instance a centered scene pivot
/// without editing the source FBX asset.
/// </summary>
internal static class CenteredPivotUtility
{
    const string DefaultTurbineName = "TurbineExample";
    const string AlreadyWrappedSuffix = "_Visual";

    [MenuItem("Tools/Digital Twin/Center Pivot Of Selected Object", false, 2000)]
    static void CenterSelectedObjectPivot()
    {
        GameObject target = Selection.activeGameObject;
        if (target == null)
        {
            EditorUtility.DisplayDialog("Center Pivot", "Select a scene object first.", "OK");
            return;
        }

        CenterPivot(target);
    }

    [MenuItem("Tools/Digital Twin/Center Pivot Of Selected Object", true)]
    static bool ValidateCenterSelectedObjectPivot()
    {
        GameObject target = Selection.activeGameObject;
        return target != null && IsSceneObject(target) && HasRenderer(target);
    }

    [MenuItem("Tools/Digital Twin/Center Pivot Of TurbineExample", false, 2001)]
    static void CenterTurbineExamplePivot()
    {
        GameObject target = FindSceneObjectByName(DefaultTurbineName);
        if (target == null)
        {
            EditorUtility.DisplayDialog(
                "Center Pivot",
                $"Could not find a scene object named '{DefaultTurbineName}'. Select the turbine object and use 'Center Pivot Of Selected Object' instead.",
                "OK");
            return;
        }

        CenterPivot(target);
    }

    static void CenterPivot(GameObject target)
    {
        if (target == null || !IsSceneObject(target))
            return;

        target = ResolvePivotRoot(target);

        if (!TryGetRendererBounds(target, out Bounds bounds))
        {
            EditorUtility.DisplayDialog(
                "Center Pivot",
                $"'{target.name}' has no renderers to calculate a visual center from.",
                "OK");
            return;
        }

        if (LooksAlreadyWrapped(target))
        {
            RecenterExistingWrapper(target, bounds);
            return;
        }

        string originalName = target.name;
        Transform oldParent = target.transform.parent;
        int oldSiblingIndex = target.transform.GetSiblingIndex();
        Scene scene = target.scene;

        GameObject wrapper = new GameObject(originalName);
        Undo.RegisterCreatedObjectUndo(wrapper, "Create centered pivot wrapper");

        SceneManager.MoveGameObjectToScene(wrapper, scene);
        wrapper.transform.SetParent(oldParent, false);
        wrapper.transform.SetSiblingIndex(oldSiblingIndex);
        wrapper.transform.position = bounds.center;
        wrapper.transform.rotation = target.transform.rotation;
        wrapper.transform.localScale = target.transform.localScale;

        Undo.RecordObject(target, "Move object under centered pivot wrapper");
        Undo.RecordObject(target.transform, "Move object under centered pivot wrapper");
        target.name = MakeVisualChildName(originalName);
        target.transform.SetParent(wrapper.transform, true);

        EditorSceneManager.MarkSceneDirty(scene);

        Selection.activeGameObject = wrapper;
        EditorGUIUtility.PingObject(wrapper);

        Debug.Log(
            $"[PIVOT] Centered scene pivot for '{originalName}' at renderer bounds center {bounds.center}. The original visual object is now '{target.name}'.",
            wrapper);
    }

    static GameObject ResolvePivotRoot(GameObject selected)
    {
        if (selected == null)
            return null;

        Transform parent = selected.transform.parent;
        if (selected.name.EndsWith(AlreadyWrappedSuffix) &&
            parent != null &&
            LooksAlreadyWrapped(parent.gameObject))
        {
            return parent.gameObject;
        }

        return selected;
    }

    static void RecenterExistingWrapper(GameObject wrapper, Bounds bounds)
    {
        Transform wrapperTransform = wrapper.transform;
        Transform[] children = new Transform[wrapperTransform.childCount];
        Vector3[] childPositions = new Vector3[children.Length];
        Quaternion[] childRotations = new Quaternion[children.Length];

        for (int i = 0; i < children.Length; i++)
        {
            children[i] = wrapperTransform.GetChild(i);
            childPositions[i] = children[i].position;
            childRotations[i] = children[i].rotation;
            Undo.RecordObject(children[i], "Recenter pivot wrapper");
        }

        Undo.RecordObject(wrapperTransform, "Recenter pivot wrapper");
        wrapperTransform.position = bounds.center;

        for (int i = 0; i < children.Length; i++)
        {
            children[i].position = childPositions[i];
            children[i].rotation = childRotations[i];
        }

        EditorSceneManager.MarkSceneDirty(wrapper.scene);
        Selection.activeGameObject = wrapper;
        EditorGUIUtility.PingObject(wrapper);

        Debug.Log(
            $"[PIVOT] Recentered existing pivot wrapper '{wrapper.name}' at renderer bounds center {bounds.center}. Select this parent object to use the centered axes.",
            wrapper);
    }

    static string MakeVisualChildName(string originalName)
    {
        return originalName.EndsWith(AlreadyWrappedSuffix)
            ? originalName
            : originalName + AlreadyWrappedSuffix;
    }

    static bool LooksAlreadyWrapped(GameObject target)
    {
        if (target.GetComponent<Renderer>() != null)
            return false;

        if (target.transform.childCount != 1)
            return false;

        Transform child = target.transform.GetChild(0);
        return child != null && child.name.EndsWith(AlreadyWrappedSuffix) && HasRenderer(child.gameObject);
    }

    static bool TryGetRendererBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds();
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    static bool HasRenderer(GameObject target)
    {
        return target != null && target.GetComponentInChildren<Renderer>(true) != null;
    }

    static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate == null || candidate.name != objectName || !IsSceneObject(candidate))
                continue;

            return candidate;
        }

        return null;
    }

    static bool IsSceneObject(GameObject target)
    {
        return target != null &&
               target.scene.IsValid() &&
               !EditorUtility.IsPersistent(target);
    }
}
#endif
