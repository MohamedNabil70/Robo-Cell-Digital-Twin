using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace realvirtual
{
    public class TooltipController : MonoBehaviour
    {
        public GameObject CanvasParent;

        private GameObject currentUIobj;
        private GameObject lastUIobj;
        private IUItooltip currTipInterface;
        private Vector3[] cornersTipInterface = new Vector3[4];

        private GameObject _globalTooltip;
        private RealvirtualTooltip _realvirtualTooltip;

        private string currTooltipText = "";
        private UI.Tooltipposition currPos;

        private bool _tooltipActive = false;
        private bool _tooltipCreated = false;
        private bool _toolTipfound = false;
        private IRaycaster raycaster;

        void Awake()
        {
            raycaster = GetComponent<IRaycaster>();
        }

        void Update()
        {
            var raysastResults = raycaster.UIRaycast();
            _resetTooltip();

            if (raysastResults.Count > 0)
            {
                _toolTipfound = false;
                foreach (var obj in raysastResults)
                {
                    if (obj.gameObject.GetComponent<IUItooltip>() != null)
                    {
                        CheckTooltipObj(obj.gameObject);
                        _globalTooltip.SetActive(false);
                        currentUIobj = obj.gameObject;
                        currTipInterface = obj.gameObject.GetComponent<IUItooltip>();
                        currTooltipText = "";
                        _toolTipfound = true;
                        break;
                    }
                }

                if (_toolTipfound)
                {
                    currTipInterface.ShowTooltip(ref currTooltipText, ref currPos, ref cornersTipInterface);

                    if (currTooltipText != "" && _realvirtualTooltip != null)
                    {
                        _realvirtualTooltip.SetTooltip(currTooltipText);
                        _globalTooltip.SetActive(true);

                        if (lastUIobj != currentUIobj)
                        {
                            // Use the new positioning system with the target's RectTransform
                            RectTransform targetRect = currentUIobj.GetComponent<RectTransform>();
                            if (targetRect != null)
                            {
                                _realvirtualTooltip.PositionRelativeTo(targetRect, RealvirtualTooltip.Placement.Auto);
                            }
                            else
                            {
                                // Fallback: use the corners provided by the interface
                                _realvirtualTooltip.PositionRelativeTo(cornersTipInterface, currPos);
                            }
                            lastUIobj = currentUIobj;
                        }
                        _tooltipActive = true;
                    }
                }
            }
            else
            {
                if (_tooltipActive)
                {
                    _resetTooltip();
                    currTipInterface.HideTooltip();
                }
            }
        }

        private void _resetTooltip()
        {
            if (_tooltipCreated)
            {
                _globalTooltip.SetActive(false);
                currTooltipText = "";
                _tooltipActive = false;
                lastUIobj = null;
            }
        }

        private void CheckTooltipObj(GameObject currObj)
        {
            if (!_tooltipCreated)
            {
                // Create tooltip - it has its own ScreenSpaceOverlay canvas, so no parenting needed
                _globalTooltip = UI.CreateTooltip(Vector3.zero, Quaternion.identity, null);
                _tooltipCreated = true;
            }
            _realvirtualTooltip = _globalTooltip.GetComponent<RealvirtualTooltip>();
        }
    }
}