using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace realvirtual
{
    [ExecuteInEditMode]
    [MovedFrom(false, sourceClassName: "rvUIMenuButton")]
    public class rvUIMenuButton : rvUIContent
    {

    public bool IsToggle = true;
    
    
    public Color colorOff;
    public Color colorOn;

    public rvUIIcon icon;
    
    public string text;
    public bool hideText = false;
    public string tooltip;
    
    public UnityEvent OnClick = new UnityEvent();
    public UnityEvent OnToggleOn = new UnityEvent();
    public UnityEvent OnToggleOff = new UnityEvent();
    

    private Image targetGraphic;
    private Toggle toggle;

    void OnValidate()
    {
        // Defer layout refresh to avoid Unity error about SendMessage during OnValidate
        #if UNITY_EDITOR
        EditorApplication.delayCall += () =>
        {
            if (this != null)
                RefreshLayout();
        };
        #else
        RefreshLayout();
        #endif
    }

    public bool IsOn()
    {
        return toggle.isOn;
    }

    public Color GetCurrentColor()
    {
        return IsOn() ? colorOn : colorOff;
    }

    void Start()
    {
        Init();
    }
    
    void Init()
    {
        RefreshLayout();
    }
    

    public override void RefreshLayout()
    {
        toggle = GetComponentInChildren<Toggle>(true);
        toggle.onValueChanged.RemoveListener(OnValueChanged);
        toggle.onValueChanged.AddListener(OnValueChanged);
        
        targetGraphic = GetComponent<Image>();
        
        RefreshVisuals();
        RefreshText();
        RefreshTooltip();
    }
    
    public void SetTooltip(string tooltip)
    {
        this.tooltip = tooltip;
        RefreshTooltip();
    }
    
    void RefreshTooltip()
    {
        rvUITooltipGenerator tooltipGenerator = GetComponent<rvUITooltipGenerator>();
        tooltipGenerator.text = tooltip;

        if (string.IsNullOrEmpty(tooltip))
        {
            tooltipGenerator.enabled = false;
        }else{
            tooltipGenerator.enabled = true;
        }
    }
    
    void RefreshText()
    {
        rvUIText textComponent = GetComponentInChildren<rvUIText>(true);

        if (hideText)
        {
            textComponent?.SetText("");
            textComponent?.gameObject.SetActive(false);
        }else{
            textComponent?.SetText(text);
            textComponent?.gameObject.SetActive(true);
        }
        
        rvUISizeLink sizeLink = GetComponentInChildren<rvUISizeLink>();
        sizeLink?.Refresh();
    }
    
    void RefreshVisuals()
    {
        targetGraphic = GetComponent<Image>();
        targetGraphic.color = GetCurrentColor();
        
    }

    [Button]
    public void ToggleOn()
    {
        toggle.isOn = true;
    }

    [Button]
    public void ToggleOff()
    {
        toggle.isOn = false;
    }

 
    void OnValueChanged(bool value)
    {
        RefreshVisuals();
        OnClick.Invoke();

        if (value)
        {
            OnToggleOn.Invoke();
        }
        else
        {
            OnToggleOff.Invoke();
        }
        
        if(!IsToggle && value)
        {
            ToggleOff();
        }
    }


    public void SetSpriteIcon(Sprite icon)
    {
        this.icon.ApplySprite(icon);
    }
    
    public void SetMaterialIcon(string icon)
    {
        this.icon.ApplyMaterialIcon(icon);
    }

    public void SetText(string text)
    {
        this.text = text;
        RefreshText();
    }
}
}
