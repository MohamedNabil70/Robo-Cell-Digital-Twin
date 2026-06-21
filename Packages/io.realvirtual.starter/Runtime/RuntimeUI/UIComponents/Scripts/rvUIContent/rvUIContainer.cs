using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;

namespace realvirtual
{
    [MovedFrom(false, sourceClassName: "rvUIContainer")]
    public abstract class rvUIContainer : rvUIContent, IrvUIContainer
    {
    [Header("Events")]
    public UnityEvent<rvUIContent> OnChildAdded = new UnityEvent<rvUIContent>();
    public UnityEvent<rvUIContent> OnChildRemoved = new UnityEvent<rvUIContent>();

    public abstract RectTransform GetContentRoot();

    public List<rvUIContent> GetUIContents()
    {
        RectTransform contentRoot = GetContentRoot();
        if (contentRoot == null)
        {
            return new List<rvUIContent>();
        }

        int childCount = contentRoot.childCount;
        List<rvUIContent> contents = new List<rvUIContent>();
        for (int i = 0; i < childCount; i++)
        {
            Transform child = contentRoot.GetChild(i);

            // Get ALL rvUIContent components on this GameObject (there can be multiple)
            rvUIContent[] componentsOnChild = child.GetComponents<rvUIContent>();
            if (componentsOnChild != null && componentsOnChild.Length > 0)
            {
                contents.AddRange(componentsOnChild);
            }
        }
        return contents;
    }

    public void MoveContentToContainer(rvUIContainer container)
    {
        List<rvUIContent> contents = GetUIContents();
        foreach (var content in contents)
        {
            content.MoveToContainer(container, false);
        }
        
        
    }

}
}
