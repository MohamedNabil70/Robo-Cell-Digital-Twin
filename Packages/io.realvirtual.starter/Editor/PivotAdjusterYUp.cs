using UnityEditor;
using UnityEngine;

namespace realvirtual
{
    public class PivotAdjusterYUp
    {
        [MenuItem("GameObject/realvirtual/Adjust Pivot for Y up", false, 0)]
        private static void AdjustPivot()
        {
            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject == null)
            {
                Debug.LogError("No GameObject selected. Please select a GameObject to adjust its pivot.");
                return;
            }

            // Check if the selected object has a Mesh attached
            MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = selectedObject.GetComponent<MeshRenderer>();

            if (meshFilter != null || meshRenderer != null)
            {
                // Create a new parent object to act as the adjusted pivot
                GameObject pivotObject = new GameObject(selectedObject.name);
                pivotObject.transform.position = selectedObject.transform.position;
                pivotObject.transform.rotation = Quaternion.identity;

                // Reparent the selected object to the new pivot object
                pivotObject.transform.SetParent(selectedObject.transform.parent, true);
                selectedObject.transform.SetParent(pivotObject.transform, true);

                // Ensure Y-axis points upwards
                pivotObject.transform.up = Vector3.up;

                // Select the new pivot object in the hierarchy
                Selection.activeGameObject = pivotObject;

                Debug.Log("Pivot adjusted successfully for " + selectedObject.name + " with a new parent created.");
                return;
            }

            // Store children temporarily
            Transform[] children = new Transform[selectedObject.transform.childCount];
            for (int i = 0; i < selectedObject.transform.childCount; i++)
            {
                children[i] = selectedObject.transform.GetChild(i);
            }

            // Detach children
            foreach (Transform child in children)
            {
                child.SetParent(null, true);
            }

            // Reorient the parent object
            selectedObject.transform.rotation = Quaternion.identity;
            selectedObject.transform.up = Vector3.up;

            // Reattach children
            foreach (Transform child in children)
            {
                child.SetParent(selectedObject.transform, true);
            }

            Debug.Log("Pivot adjusted successfully for " + selectedObject.name + " without creating a new parent.");
        }
    }
}
