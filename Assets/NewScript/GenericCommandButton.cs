using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// GenericCommandButton — UI button that sends a PLC Bool tag.
///
/// ══ BUTTON MODES ═══════════════════════════════════════════════════════════
///   Toggle:    Flips state on each press (ON→OFF, OFF→ON). Uses onClick event.
///   Latched:   Turns ON when pressed, STAYS ON until externally deactivated.
///              Uses onClick event. Does NOT toggle off on subsequent presses.
///   Momentary: ON while pointer is held down, OFF when released.
///              AUTO-HANDLED via IPointerDownHandler/IPointerUpHandler.
///              ⚠ Do NOT rely on onClick for Momentary mode — it's ignored.
///   Reset:     Turns OFF when pressed. Uses onClick event.
///
/// ══ IMPORTANT FOR MOMENTARY MODE ═══════════════════════════════════════════
///   Momentary mode does NOT use the Button.onClick event due to Unity's
///   event ordering (onClick fires AFTER OnPointerUp, which would re-set
///   the state to TRUE after we already set it to FALSE).
///   
///   Instead, Momentary mode uses:
///   - IPointerDownHandler.OnPointerDown → state = TRUE
///   - IPointerUpHandler.OnPointerUp → state = FALSE
///   - IPointerExitHandler.OnPointerExit → state = FALSE (if pointer leaves button)
///
///   You can still wire onClick to OnPress() for other modes — it will be
///   automatically ignored when buttonMode is Momentary.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class GenericCommandButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    /// <summary>
    /// Defines how the button behaves when pressed.
    /// </summary>
    public enum ButtonMode { Toggle, Latched, Momentary, Reset }

    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    [Tooltip("TRUE = button uses IO_Router.SimulateInput() — fires local callbacks " +
             "WITHOUT sending to PLC bridge. Use for offline testing.\n" +
             "FALSE = button uses IO_Router.SetValue() — sends to PLC bridge as well.")]
    public bool offlineMode = true;

    [Header("══ PLC Tag ══════════════════════════════════════════════════")]
    [Tooltip("Exact PLC Bool tag name. Must match variable in TIA Portal DB.\n" +
             "⚠ Case-sensitive — 'Car1_CycleStart' ≠ 'car1_cyclestart'.")]
    public string tag = "";

    [Header("══ Button Behaviour ══════════════════════════════════════════")]
    [Tooltip("Toggle: flips each press (ON↔OFF)\n" +
             "Latched: turns ON and STAYS ON until externally deactivated\n" +
             "Momentary: TRUE while held, FALSE on release (AUTO-HANDLED — no onClick needed)\n" +
             "Reset: turns OFF (companion to Latched buttons)")]
    public ButtonMode buttonMode = ButtonMode.Toggle;

    [Header("══ Current State (Read Only) ════════════════════════════════")]
    public bool state = false;

    [Header("══ PLC Read-back ════════════════════════════════════════════")]
    [Tooltip("If true, mirrors the PLC tag value back onto this button's visual state.\n" +
             "For Latched buttons, this allows the PLC to reset the state.")]
    public bool syncFromPLC = true;

    [Header("══ Additional OUTPUT Tags (Unity → PLC) ═══════════════════")]
    [Tooltip("Pulse TRUE when button is pressed (any mode)")]
    public string out_ButtonPressed = "";
    [Tooltip("Mirrors current button state continuously")]
    public string out_ButtonState   = "";

    [Header("══ UI Elements (optional) ═══════════════════════════════════")]
    [Tooltip("Text label — shows tag name and current state")]
    public Text  label;
    [Tooltip("Image indicator — green = ON, red = OFF")]
    public Image indicator;
    public Color colorOn  = new Color(0.2f, 0.85f, 0.3f);
    public Color colorOff = new Color(0.85f, 0.2f, 0.2f);

    [Header("══ Debug (Read Only) ════════════════════════════════════════")]
    [SerializeField] string dbMode = "Offline";
    [SerializeField] bool   dbIsPointerDown = false;
    [SerializeField] float  dbLastStateChangeTime = 0f;

    // ── Private ───────────────────────────────────────────────────────────────
    float writeTime = -10f;
    const float WRITE_LOCKOUT = 2.0f;

    // Momentary state tracking
    bool pointerIsDown = false;
    bool momentarySentOnPress = false;

    System.Action<bool> plcCallback;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        dbMode = offlineMode ? "Offline" : "PLC";
        UpdateUI();

        ValidateModeConfiguration();

        if (syncFromPLC && !string.IsNullOrEmpty(tag))
        {
            plcCallback = v =>
            {
                if (Time.time - writeTime < WRITE_LOCKOUT) return;
                state = v;
                UpdateUI();
            };

            Debug.Log($"[BUTTON] Registering PLC sync tag '{tag}' " +
                      "(must match TIA Portal DB variable name EXACTLY, case-sensitive).");
            StartCoroutine(TagSubscriptionHelper.RegisterWithRetry(tag, plcCallback, this));
        }

        SetOutput(out_ButtonState, state);
        Debug.Log($"[BUTTON:{(string.IsNullOrEmpty(tag) ? "?" : tag)}] Started in {dbMode} mode, buttonMode={buttonMode}.");
    }

    void ValidateModeConfiguration()
    {
        switch (buttonMode)
        {
            case ButtonMode.Momentary:
                Debug.Log($"[BUTTON:{name}] ★ MOMENTARY MODE ACTIVE ★\n" +
                          "  Press handling: IPointerDownHandler (NOT onClick)\n" +
                          "  Release handling: IPointerUpHandler + IPointerExitHandler\n" +
                          "  → State will be TRUE while held, FALSE when released.\n" +
                          "  → You can still wire onClick to OnPress() — it will be ignored for Momentary mode.");
                break;

            case ButtonMode.Latched:
                if (!syncFromPLC && string.IsNullOrEmpty(tag))
                {
                    Debug.LogWarning(
                        $"[BUTTON:{name}] Latched mode requires either:\n" +
                        "  1. syncFromPLC = TRUE (so PLC can reset the state), OR\n" +
                        "  2. A companion Reset button that writes FALSE to the same tag.");
                }
                break;

            case ButtonMode.Reset:
                Debug.Log($"[BUTTON:{name}] Reset mode active — will set state to FALSE when pressed.");
                break;
        }
    }

    void OnDestroy()
    {
        if (syncFromPLC && !string.IsNullOrEmpty(tag) && plcCallback != null)
            IO_Router.Instance?.Unregister(tag, plcCallback);
        TagSubscriptionHelper.Remove(tag);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ══ POINTER EVENT HANDLERS — Used for MOMENTARY mode ══════════════════════
    // ══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Called when pointer presses DOWN on this button.
    /// ONLY used for Momentary mode — other modes use onClick/OnPress().
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        // Only handle Momentary mode through pointer events
        if (buttonMode != ButtonMode.Momentary) return;

        pointerIsDown = true;
        dbIsPointerDown = true;
        momentarySentOnPress = true;
        state = true;
        dbLastStateChangeTime = Time.time;
        
        Send();
        UpdateUI();
        Debug.Log($"[BUTTON:{tag}] ★ MOMENTARY PRESS → TRUE (pointer down)");
    }

    /// <summary>
    /// Called when pointer is RELEASED.
    /// For Momentary mode: sets state to FALSE.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (buttonMode != ButtonMode.Momentary) return;
        HandleMomentaryRelease("pointer up");
    }

    /// <summary>
    /// Called when pointer LEAVES the button area while pressed.
    /// For Momentary mode: sets state to FALSE (handles drag-out scenario).
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        if (buttonMode != ButtonMode.Momentary) return;
        if (!pointerIsDown) return;
        HandleMomentaryRelease("pointer exit (dragged out)");
    }

    /// <summary>
    /// Central release handler for Momentary mode.
    /// Called by both OnPointerUp and OnPointerExit.
    /// </summary>
    void HandleMomentaryRelease(string reason)
    {
        if (!pointerIsDown) return;
        
        pointerIsDown = false;
        dbIsPointerDown = false;

        if (momentarySentOnPress)
        {
            state = false;
            dbLastStateChangeTime = Time.time;
            Send();
            UpdateUI();
            Debug.Log($"[BUTTON:{tag}] ★ MOMENTARY RELEASE → FALSE ({reason})");
        }
        momentarySentOnPress = false;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ══ ONCLICK HANDLER — Used for Toggle, Latched, Reset modes ══════════════
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wire this to Button.OnClick for Toggle, Latched, and Reset modes.
    /// 
    /// ⚠ For MOMENTARY mode, this method does NOTHING — the press/release
    ///   is handled automatically by IPointerDownHandler/IPointerUpHandler.
    ///   You can still wire onClick to this method; it will be safely ignored.
    /// </summary>
    public void OnPress()
    {
        // ═══ CRITICAL: For Momentary mode, IGNORE onClick ═══
        // Unity's event order: OnPointerUp fires BEFORE onClick.
        // If we handled Momentary in OnPress (called by onClick), the sequence would be:
        //   1. Our OnPointerUp → state = FALSE
        //   2. Button's onClick → OnPress() → state = TRUE
        // Result: Button stays TRUE forever. WRONG!
        // 
        // Solution: Handle Momentary in OnPointerDown/OnPointerUp, ignore onClick.
        if (buttonMode == ButtonMode.Momentary)
        {
            Debug.LogWarning($"[BUTTON:{tag}] OnPress() called via onClick but mode is MOMENTARY — ignored. " +
                             "Momentary mode is auto-handled by pointer events.");
            return;
        }

        switch (buttonMode)
        {
            case ButtonMode.Toggle:
                state = !state;
                Debug.Log($"[BUTTON:{tag}] Toggle → {state}");
                break;

            case ButtonMode.Latched:
                if (state)
                {
                    Debug.Log($"[BUTTON:{tag}] Latched — already ON, press ignored. " +
                              "Use a Reset button or PLC input to deactivate.");
                    UpdateUI();
                    return;
                }
                state = true;
                Debug.Log($"[BUTTON:{tag}] Latched → ON (will stay ON until externally reset)");
                break;

            case ButtonMode.Reset:
                state = false;
                Debug.Log($"[BUTTON:{tag}] Reset → OFF");
                break;
        }

        dbLastStateChangeTime = Time.time;
        Send();
        UpdateUI();
    }

    /// <summary>
    /// Manual release for backward compatibility or special cases.
    /// For normal Momentary mode, this is auto-handled by IPointerUpHandler.
    /// </summary>
    public void OnRelease()
    {
        if (buttonMode != ButtonMode.Momentary) return;
        HandleMomentaryRelease("manual OnRelease call");
    }

    // Legacy helpers for backward compat
    public void Toggle()   { buttonMode = ButtonMode.Toggle;  OnPress(); }
    public void SetTrue()  { buttonMode = ButtonMode.Latched; OnPress(); }
    public void SetFalse() { buttonMode = ButtonMode.Reset;   OnPress(); }

    // ─────────────────────────────────────────────────────────────────────────
    void Send()
    {
        if (string.IsNullOrEmpty(tag))
        {
            Debug.LogWarning("[BUTTON] tag is empty — assign a PLC tag in the Inspector.");
            return;
        }

        writeTime = Time.time;

        if (IO_Router.Instance == null)
        {
            Debug.LogWarning($"[BUTTON] IO_Router not found — cannot send {tag}={state}");
            return;
        }

        if (offlineMode)
        {
            IO_Router.Instance.SimulateInput(tag, state);
            Debug.Log($"[BUTTON:{tag}] SimulateInput({state}) [OFFLINE]");
        }
        else
        {
            IO_Router.Instance.SetValue(tag, state);
            Debug.Log($"[BUTTON:{tag}] SetValue({state}) [PLC]");
        }

        SetOutput(out_ButtonState, state);
        
        // Pulse on press action (for all modes including Reset)
        bool shouldPulse = state || buttonMode == ButtonMode.Reset;
        if (shouldPulse)
        {
            SetOutput(out_ButtonPressed, true);
            StartCoroutine(PulsePressed());
        }
    }

    System.Collections.IEnumerator PulsePressed()
    {
        yield return new WaitForSeconds(0.1f);
        SetOutput(out_ButtonPressed, false);
    }

    void UpdateUI()
    {
        if (label != null)
        {
            string tagDisplay = string.IsNullOrEmpty(tag) ? "?" : tag;
            string modeStr    = offlineMode ? "[SIM]" : "[PLC]";
            string modeLabel  = buttonMode.ToString().ToUpper();
            label.text = $"{modeStr} {tagDisplay}: {(state ? "ON" : "OFF")} [{modeLabel}]";
        }
        if (indicator != null)
            indicator.color = state ? colorOn : colorOff;
    }

    void SetOutput(string t, bool v)
    { 
        if (!string.IsNullOrEmpty(t)) 
        {
            if (IO_Router.Instance == null)
            {
                Debug.LogWarning($"[BUTTON] IO_Router not found — cannot set output '{t}'={v}");
                return;
            }
            IO_Router.Instance.SetValue(t, v); 
        }
    }

    // ── Editor helpers ────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Force State ON")]
    void EditorForceOn() { if (Application.isPlaying) { state = true; Send(); UpdateUI(); } }

    [ContextMenu("Force State OFF")]
    void EditorForceOff() { if (Application.isPlaying) { state = false; Send(); UpdateUI(); } }

    [ContextMenu("Reset to Default State")]
    void EditorResetState() { if (Application.isPlaying) { state = false; UpdateUI(); } }
#endif
}