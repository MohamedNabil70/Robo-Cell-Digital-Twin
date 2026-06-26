using UnityEngine;

/// <summary>
/// Makes a collider-equipped digital-twin object open its dashboard when clicked.
/// </summary>
public sealed class ClickableTwinObject : MonoBehaviour
{
    [Tooltip("Unique ID used by telemetry topics, for example conveyor1.")]
    public string objectId;

    private void OnMouseDown()
    {
        if (string.IsNullOrWhiteSpace(objectId))
        {
            Debug.LogWarning($"Clickable object '{name}' has no objectId assigned.", this);
            return;
        }

        if (DashboardManager.Instance == null)
        {
            Debug.LogError("Cannot show dashboard because no DashboardManager exists in the scene.", this);
            return;
        }

        Debug.Log($"Clicked twin object '{name}' ({objectId.Trim()}).", this);
        DashboardManager.Instance.ShowDashboard(objectId.Trim(), GetDashboardAnchorPosition());
    }

    /// <summary>
    /// Returns a stable world position near the physical center of this object.
    /// </summary>
    public Vector3 GetDashboardAnchorPosition()
    {
        Collider rootCollider = GetComponent<Collider>();
        if (rootCollider != null && rootCollider.enabled)
        {
            Bounds bounds = rootCollider.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        Renderer rootRenderer = GetComponentInChildren<Renderer>();
        if (rootRenderer != null)
        {
            Bounds bounds = rootRenderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        return transform.position;
    }
}
