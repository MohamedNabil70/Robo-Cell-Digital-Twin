// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using System;
using NaughtyAttributes;

namespace realvirtual
{
    using UnityEngine;
    using UnityEngine.UI;

#pragma warning disable CS3009 // Base type is not CLS-compliant
#pragma warning disable CS3003 // Base type is not CLS-compliant

    [ExecuteInEditMode]
    public class rvUISizeLink : MonoBehaviour
    {
        public RectTransform target;
        public bool linkWidth = true; //!< Link the width dimension to the target
        public bool linkHeight = true; //!< Link the height dimension to the target
        
        public Vector2 padding;

        private LayoutElement layoutElement; //!< Optional LayoutElement component for layout control

        private void Awake()
        {
            layoutElement = GetComponent<LayoutElement>();
            Refresh();
        }

        private void OnValidate()
        {
            Refresh();
        }
        
        private void LateUpdate()
        {
            
                Refresh();
            
        }

        
        [Button]
        public void Refresh()
        {
            if (target == null) return;

            var rt = GetComponent<RectTransform>();
            
            
            Vector2 newSize = target.sizeDelta;
            if (!linkWidth)
            {
                newSize.x = rt.sizeDelta.x;
            }
            if (!linkHeight)
            {
                newSize.y = rt.sizeDelta.y;
            }
            
            rt.sizeDelta = newSize;

            if (layoutElement == null)
            {
                layoutElement = GetComponent<LayoutElement>();
            }

            // Update LayoutElement if present
            if (layoutElement != null)
            {

                if (linkWidth)
                {
                    layoutElement.preferredWidth = newSize.x;
                    layoutElement.minWidth = newSize.x;
                }

                if (linkHeight)
                {
                    layoutElement.preferredHeight = newSize.y;
                    layoutElement.minHeight = newSize.y;
                }
                
                
            }
        }
    }
}
#pragma warning restore CS3009 // Base type is not CLS-compliant
#pragma warning restore CS3003 // Base type is not CLS-compliant