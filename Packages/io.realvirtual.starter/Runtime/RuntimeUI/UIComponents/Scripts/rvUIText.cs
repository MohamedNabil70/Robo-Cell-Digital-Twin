// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

using NaughtyAttributes;
using UnityEngine.Events;

namespace realvirtual
{
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

#pragma warning disable CS3009 // Base type is not CLS-compliant
#pragma warning disable CS3003 // Base type is not CLS-compliant
    //! Links the RectTransform size to the TextMeshPro text size with optional padding.
    //! Automatically adjusts the GameObject's RectTransform dimensions to match the rendered text size,
    //! useful for creating dynamic UI elements that adapt to text content.
    public class rvUIText : rvUIContent
    {
        [TextArea]
        public string text;

        public bool useWidth = true; //!< Synchronize the width dimension with text width
        public float defaultWidth = 100; //!< Default width if useWidth is false
        
        public bool useHeight = true; //!< Synchronize the height dimension with text height
        public float defaultHeight = 30; //!< Default height if useHeight is false

        
        public Vector2 padding = Vector2.zero; //!< Additional padding to add to the text size

        public bool updateInRuntime = false; //!< Update the size continuously during runtime (performance cost)

        
        public UnityEvent OnEmptyText = new UnityEvent();
        public UnityEvent OnNonEmptyText = new UnityEvent();

        private TextMeshProUGUI textComponent; //!< The TextMeshPro component to link size from
        private RectTransform rectTransform;
        private LayoutElement layoutElement; //!< Optional LayoutElement component for layout control
        private Vector2 lastPreferredSize;


        void OnValidate()
        {
            rectTransform = GetComponent<RectTransform>();
            textComponent = GetComponentInChildren<TextMeshProUGUI>();
            layoutElement = GetComponent<LayoutElement>();

            // Defer layout refresh to avoid Unity error about SendMessage during OnValidate
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                    RefreshLayout();
            };
            #else
            RefreshLayout();
            #endif
        }
        
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            textComponent = GetComponentInChildren<TextMeshProUGUI>();
            layoutElement = GetComponent<LayoutElement>();
        }
        
        private void LateUpdate()
        {
            if (updateInRuntime)
            {
                RefreshLayout();
            }
        }
        

        public override void RefreshLayout()
        {
            
            if (textComponent == null)
            {
                // Try to find it again if missing
                textComponent = GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent == null) return;
            }

            if (textComponent.text != text)
            {
                if(string.IsNullOrEmpty(text))
                    OnEmptyText.Invoke();
                else
                    OnNonEmptyText.Invoke();
            }
            
            textComponent.text = text;

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
                if (rectTransform == null) return;
            }

            // Force the text component to update its mesh info
            textComponent.ForceMeshUpdate();

            // Get the preferred size of the text
            Vector2 preferredSize = textComponent.GetPreferredValues();

            // Only update if size has changed (optimization)
            if (preferredSize == lastPreferredSize && !updateInRuntime)
                return;

            lastPreferredSize = preferredSize;

            // Get TextMeshPro margins from Extra Settings (left, top, right, bottom)
            Vector4 tmpMargin = textComponent.margin;

            // Get current size delta
            Vector2 newSize = rectTransform.sizeDelta;

            // Apply width if enabled
            if (useWidth)
            {
                // Add local padding + TMP margins (left + right)
                newSize.x = preferredSize.x + padding.x * 2 + tmpMargin.x + tmpMargin.z;
            }else
            {
                newSize.x = defaultWidth;
            }

            // Apply height if enabled
            if (useHeight)
            {
                // Add local padding + TMP margins (top + bottom)
                newSize.y = preferredSize.y + padding.y * 2 + tmpMargin.y + tmpMargin.w;
            }else
            {
                newSize.y = defaultHeight;
            }

            // Set the new size
            rectTransform.sizeDelta = newSize;

            if (layoutElement == null)
            {
                layoutElement = GetComponent<LayoutElement>();
            }

            // Update LayoutElement if present and enabled
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = newSize.x;
                layoutElement.minWidth = newSize.x;
                layoutElement.preferredHeight = newSize.y;
                layoutElement.minHeight = newSize.y;
            }
        }

        
        public void SetText(string text)
        {
            this.text = text;
            RefreshLayout();
        }
    }
}
#pragma warning restore CS3009 // Base type is not CLS-compliant
#pragma warning restore CS3003 // Base type is not CLS-compliant
