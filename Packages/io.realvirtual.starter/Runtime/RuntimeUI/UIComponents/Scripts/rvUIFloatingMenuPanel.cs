using System;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using realvirtual;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;


namespace realvirtual
{
    public class rvUIFloatingMenuPanel : rvUIContainer
    {
        public bool hideText;
        public bool hideHeader;
        
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private RectTransform header;
        
        [NaughtyAttributes.ReadOnly] public rvUIContent[] elements;
    
       
    
        public override RectTransform GetContentRoot()
        {
            if (contentRoot != null)
            {
                return contentRoot;
            }
            return GetComponent<RectTransform>();
        }
    
        private void Start()
        {
            Refresh();
        }
    
    
        [Button]
        public void Refresh()
        {
            elements = FindTopLevelMenuElements();
            RefreshLayout();
            
        }
        
        rvUIContent[] FindTopLevelMenuElements()
        {
            
            List<rvUIContent> topLevelElements = new List<rvUIContent>();
            
            for(int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
    
                rvUIContent element = child.GetComponent<rvUIContent>();
                if (element != null)
                {
    
                    topLevelElements.Add(element);
                    
                }
            }
            
            return topLevelElements.ToArray();
            
        }
        
        
    
    
        public override void RefreshLayout()
        {
            ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
    
            
            rvUIMenuButton[] buttons = FindTopLevelButtons();
    
            // Refresh all buttons first
            foreach (rvUIMenuButton button in buttons)
            {
                
                button.hideText = hideText;
                button.RefreshLayout();
                
            }
    
    
            // Apply header visibility
            RefreshHeaderVisibility();
    
            // Only toggle ContentSizeFitter if it exists and was already enabled
            // This prevents automatically enabling it when user wants it disabled
            if (fitter != null && fitter.enabled)
            {
                // Toggle to force layout update
                fitter.enabled = false;
                fitter.enabled = true;
            }
    
            // Force immediate layout rebuild
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }
    
        private rvUIMenuButton[] FindTopLevelButtons()
        {
            List<rvUIMenuButton> topLevelElements = new List<rvUIMenuButton>();
            
            Transform rootTransform = GetContentRoot();
            
            for(int i = 0; i < rootTransform.childCount; i++)
            {
                Transform child = rootTransform.GetChild(i);
    
                rvUIMenuButton element = child.GetComponent<rvUIMenuButton>();
                if (element != null)
                {
    
                    topLevelElements.Add(element);
                    
                }
            }
            
            return topLevelElements.ToArray();
        }
    
        void RefreshHeaderVisibility()
        { 
            if (header != null)
            {
                header.gameObject.SetActive(!hideHeader);
            }
        }
    
        /// <summary>
        /// Toggles the header visibility.
        /// </summary>
        public void ToggleHeader()
        {
            hideHeader = !hideHeader;
            RefreshHeaderVisibility();
    
           
        }
    
        /// <summary>
        /// Shows the header.
        /// </summary>
        public void ShowHeader()
        {
            if (hideHeader)
            {
                hideHeader = false;
                RefreshHeaderVisibility();
            }
        }
    
        /// <summary>
        /// Hides the header.
        /// </summary>
        public void HideHeader()
        {
            if (!hideHeader)
            {
                hideHeader = true;
                RefreshHeaderVisibility();
            }
        }
    
        public void SetRelativePlacement(rvUIRelativePlacement.Placement placementstyle, RectTransform target = null, float margin = -1)
        {
             rvUIRelativePlacement placement = GetComponent<rvUIRelativePlacement>();
             if (placement != null)
             {
                 
                 if (margin >= 0)
                 {
                     placement.margin = margin;
                 }
                 if (target != null)
                 {
                     placement.target = target.GetComponent<RectTransform>();
                 }
                 placement.SetPlacement(placementstyle);
                 placement.RefreshPosition();
             }
        }
    
        public rvUIHeader GetHeader()
        {
            return header.GetComponent<rvUIHeader>();
            
        }
    
        public void DeactivateOnClick(bool outsideOnly, Action callback = null)
        {
            rvUIDeactivateOnClick deactivateOnClick = GetComponent<rvUIDeactivateOnClick>();
            if (deactivateOnClick == null)
            {
                deactivateOnClick = gameObject.AddComponent<rvUIDeactivateOnClick>();
            }
            deactivateOnClick.outsideOnly = outsideOnly;
            deactivateOnClick.enabled = true;
            deactivateOnClick.callback = callback;
        }
    }
}
