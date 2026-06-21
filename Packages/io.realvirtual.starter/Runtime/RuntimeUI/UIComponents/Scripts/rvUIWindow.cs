// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace realvirtual
{
    #region doc
    //! Manages UI window positioning and visibility within a canvas boundary for runtime UI systems.

    //! The rvUIWindow component ensures that UI windows remain fully visible within the canvas bounds
    //! by automatically repositioning them when they extend beyond the viewport. This is essential
    //! for industrial HMI applications where windows must always remain accessible to operators.
    //! The component provides automatic boundary checking, smooth repositioning, and integration
    //! with the UI scaling system for responsive layouts.
    //!
    //! Key Features:
    //! - Automatic window repositioning to keep content within canvas bounds
    //! - Edge padding to maintain visual separation from canvas borders
    //! - Optimized update patterns to minimize performance impact
    //! - Integration with rvUIScaler for responsive scaling
    //! - Lazy initialization for efficient resource usage
    //!
    //! Common Applications:
    //! - Popup dialogs and context menus in HMI interfaces
    //! - Floating tool windows and property panels
    //! - Alert and notification windows
    //! - Dynamic tooltip and information displays
    //!
    //! Performance Considerations:
    //! - Position checking occurs only once per window activation
    //! - Cached component references minimize GetComponent calls
    //! - World corner calculations are performed only when necessary
    //!
    //! For detailed documentation see: https://doc.realvirtual.io/components-and-scripts/ui/ui-window
    #endregion
    [HelpURL("https://doc.realvirtual.io/components-and-scripts/ui/ui-window")]
    public class rvUIWindow : MonoBehaviour
    {
        #region Inspector Properties

        [Header("Window Configuration")]
        [Tooltip("The GameObject containing the window content that will be positioned within canvas bounds")]
        public GameObject window; //!< The window GameObject to be managed and positioned within canvas bounds

        [Header("Layout Settings")]
        [SerializeField]
        [Tooltip("Minimum padding in pixels between window edges and canvas boundaries")]
        [Range(0f, 100f)]
        private float padding = 20f; //!< Edge padding in pixels to maintain separation from canvas boundaries

        #endregion

        #region Private Fields

        // Cached component references for performance
        private RectTransform _rectTransform;
        private RectTransform _canvasRectTransform;
        private Canvas _parentCanvas;
        private rvUIScaler _scaler;

        // State tracking
        private bool _positionChecked = false;
        private bool _isInitialized = false;

        // Pre-allocated arrays for corner calculations
        private readonly Vector3[] _canvasCorners = new Vector3[4];
        private readonly Vector3[] _windowCorners = new Vector3[4];

        #endregion

        #region Unity Lifecycle

        //! Initializes component references and caches frequently accessed components.
        public void Awake()
        {
            InitializeComponents();
        }

        //! Checks window visibility state and ensures proper positioning when activated.
        private void Update()
        {
            // Early exit if not initialized
            if (!_isInitialized || window == null)
                return;

            // Check if window became active and needs positioning
            if (window.activeSelf && !_positionChecked)
            {
                // Update scaler if available
                if (_scaler != null)
                    _scaler.UpdaterScale();

                EnsureVisible();
                _positionChecked = true;
            }
            // Reset position check flag when window is deactivated
            else if (!window.activeSelf && _positionChecked)
            {
                _positionChecked = false;
            }
        }

        //! Ensures window visibility when component is enabled.
        private void OnEnable()
        {
            if (!_isInitialized)
                InitializeComponents();

            EnsureVisible();
        }

        //! Cleans up cached references when component is destroyed.
        private void OnDestroy()
        {
            _rectTransform = null;
            _canvasRectTransform = null;
            _parentCanvas = null;
            _scaler = null;
        }

        #endregion

        #region Initialization

        //! Initializes and caches all required component references.
        private void InitializeComponents()
        {
            // Cache RectTransform reference
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                Logger.Error("rvUIWindow requires a RectTransform component", this);
                return;
            }

            // Cache scaler reference (optional component)
            _scaler = GetComponent<rvUIScaler>();

            // Find and cache parent canvas
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas == null)
            {
                Logger.Error("rvUIWindow must be a child of a Canvas", this);
                return;
            }

            // Cache canvas RectTransform
            _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            if (_canvasRectTransform == null)
            {
                Logger.Error("Parent Canvas is missing RectTransform component", this);
                return;
            }

            _positionChecked = false;
            _isInitialized = true;
        }

        #endregion

        #region Window Positioning

        //! Ensures the window remains fully visible within canvas bounds by repositioning if necessary.
        private void EnsureVisible()
        {
            // Validate required components
            if (!ValidateComponents())
                return;

            // Get world corners for bounds calculations
            _canvasRectTransform.GetWorldCorners(_canvasCorners);
            _rectTransform.GetWorldCorners(_windowCorners);

            // Calculate repositioning offset
            Vector2 offset = CalculateRepositionOffset();

            // Apply offset if needed
            if (offset != Vector2.zero)
            {
                ApplyRepositioning(offset);
            }
        }

        //! Validates that all required components are properly initialized.
        //! @return True if all components are valid, false otherwise
        private bool ValidateComponents()
        {
            if (_rectTransform == null)
            {
                Logger.Warning("RectTransform is null, reinitializing", this);
                InitializeComponents();
                return _rectTransform != null;
            }

            if (_canvasRectTransform == null)
            {
                Logger.Warning("Canvas RectTransform is null, reinitializing", this);
                InitializeComponents();
                return _canvasRectTransform != null;
            }

            return true;
        }

        //! Calculates the offset needed to keep the window within canvas bounds.
        //! @return Offset vector to apply for repositioning
        private Vector2 CalculateRepositionOffset()
        {
            Vector2 offset = Vector2.zero;

            // Calculate window dimensions from corners
            float windowHeight = (_windowCorners[1].y - _windowCorners[0].y) * 0.5f;
            float windowWidth = (_windowCorners[2].x - _windowCorners[0].x) * 0.5f;

            // Validate dimensions
            if (windowHeight <= 0 || windowWidth <= 0)
            {
                Logger.Warning("Invalid window dimensions detected", this);
                return offset;
            }

            // Calculate horizontal offset
            offset.x = CalculateHorizontalOffset(windowWidth);

            // Calculate vertical offset
            offset.y = CalculateVerticalOffset(windowHeight);

            return offset;
        }

        //! Calculates horizontal offset needed to keep window within horizontal bounds.
        //! @param windowWidth Half-width of the window
        //! @return Horizontal offset to apply
        private float CalculateHorizontalOffset(float windowWidth)
        {
            float offset = 0f;

            // Check left boundary
            float leftBoundary = _canvasCorners[0].x + padding;
            if (_windowCorners[0].x < leftBoundary)
            {
                offset = leftBoundary + windowWidth - _windowCorners[0].x;
            }
            // Check right boundary
            else
            {
                float rightBoundary = _canvasCorners[2].x - padding;
                if (_windowCorners[2].x > rightBoundary)
                {
                    offset = rightBoundary - windowWidth - _windowCorners[2].x;
                }
            }

            return offset;
        }

        //! Calculates vertical offset needed to keep window within vertical bounds.
        //! @param windowHeight Half-height of the window
        //! @return Vertical offset to apply
        private float CalculateVerticalOffset(float windowHeight)
        {
            float offset = 0f;

            // Check top boundary
            float topBoundary = _canvasCorners[1].y - padding;
            if (_windowCorners[1].y > topBoundary)
            {
                offset = topBoundary - windowHeight - _windowCorners[1].y;
            }
            // Check bottom boundary
            else
            {
                float bottomBoundary = _canvasCorners[0].y + padding;
                if (_windowCorners[0].y < bottomBoundary)
                {
                    offset = bottomBoundary + windowHeight - _windowCorners[0].y;
                }
            }

            return offset;
        }

        //! Applies the calculated repositioning offset to the window.
        //! @param offset The offset to apply for repositioning
        private void ApplyRepositioning(Vector2 offset)
        {
            // Scale offset by the RectTransform's lossy scale
            Vector3 scale = _rectTransform.lossyScale;

            // Validate scale to prevent division by zero
            if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f))
            {
                Logger.Warning("Invalid RectTransform scale detected, skipping repositioning", this);
                return;
            }

            // Apply scaled offset
            offset.x *= scale.x;
            offset.y *= scale.y;

            _rectTransform.anchoredPosition += offset;
        }

        #endregion

        #region Public Methods

        //! Forces an immediate visibility check and repositioning if necessary.
        public void ForceEnsureVisible()
        {
            _positionChecked = false;
            EnsureVisible();
        }

        //! Sets the edge padding for window boundaries.
        //! @param newPadding The padding value in pixels (clamped between 0 and 100)
        public void SetPadding(float newPadding)
        {
            padding = Mathf.Clamp(newPadding, 0f, 100f);
            if (window != null && window.activeSelf)
            {
                ForceEnsureVisible();
            }
        }

        //! Gets the current edge padding value.
        //! @return The current padding in pixels
        public float GetPadding()
        {
            return padding;
        }

        #endregion
    }
}

