using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUIContent")]
    public abstract class rvUIContent : MonoBehaviour, IRuntimeUIColorScheme
    {

    public UnityEvent<ColorScheme> OnApplyColorScheme = new UnityEvent<ColorScheme>();
    
    public abstract void RefreshLayout();
    
    
    [Button]
    void OnValidate()
    {
        // Defer layout refresh to avoid Unity error about SendMessage during OnValidate
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                RefreshLayoutRecursive();
        };
        #else
        RefreshLayoutRecursive();
        #endif
    }
    
    
    public void RefreshLayoutRecursive()
    {
        List<rvUIContent> childContents = GetChildContents();
        foreach (var content in childContents)
        {
            content.RefreshLayout();
        }

    }

    public void RefreshLayoutBottomUp()
    {
        // Post-order traversal: refresh children first, then self
        List<rvUIContent> childContents = GetChildContents();
        foreach (var content in childContents)
        {
            content.RefreshLayoutBottomUp();
        }

        // After all children are refreshed, refresh this node
        RefreshLayout();
    }
    
    public List<rvUIContent> GetChildContents()
    {
        // Check if this is a container - containers have a specific content root
        rvUIContainer container = this as rvUIContainer;
        if (container != null)
        {
            // Use container's method which correctly gets children from content root
            return container.GetUIContents();
        }

        // For non-container content, get direct transform children
        List<rvUIContent> contents = new List<rvUIContent>();
        Transform transform = this.transform;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            // Get ALL rvUIContent components on this GameObject (there can be multiple)
            rvUIContent[] componentsOnChild = child.GetComponents<rvUIContent>();
            if (componentsOnChild != null && componentsOnChild.Length > 0)
            {
                contents.AddRange(componentsOnChild);
            }
        }

        return contents;
    }

    public void MoveToContainer(rvUIContainer container, bool refresh = true)
    {
        if (container == null)
        {
            realvirtual.Logger.Warning("Cannot move to null container", this);
            return;
        }

        RectTransform contentRoot = container.GetContentRoot();
        if (contentRoot == null)
        {
            realvirtual.Logger.Warning("Container content root is null, cannot move content", this);
            return;
        }

        RectTransform rt = GetComponent<RectTransform>();
        rt.SetParent(contentRoot, true);

    }


    public rvUIContainer GetContainer()
    {
        // Start search from parent to exclude self (important for containers)
        // Without this, a container would return itself instead of its parent container
        if (transform.parent == null) return null;
        return transform.parent.GetComponentInParent<rvUIContainer>(true);
    }

    public List<rvUIContent> GetPathToRoot()
    {
        // Collect path from this node to the root (leaf to root order)
        List<rvUIContent> path = new List<rvUIContent>();
        rvUIContent current = this;

        while (current != null)
        {
            path.Add(current);
            rvUIContainer container = current.GetContainer();

            // Stop if we reached the root or the container is the same as current
            if (container == null || container == current)
            {
                break;
            }

            current = container;
        }

        return path;
    }


    public void ApplyColorScheme(ColorScheme colorScheme)
    {
        OnApplyColorScheme.Invoke(colorScheme);
    }
}
}
