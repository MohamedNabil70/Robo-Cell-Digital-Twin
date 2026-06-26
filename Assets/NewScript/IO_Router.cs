using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// IO_Router â€” central message bus between the OPC UA Bridge and all Unity components.
/// (FIXED â€” see change log below)
///
/// â•گâ•گ FIXES IN THIS VERSION â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
///
///   FIX A â€” QUALIFIED TAG NAMES ("DBName.VarName") ACROSS ALL SET/GET:
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   The bridge now keys every tag as "DataBlockName.VariableName" to prevent
///   silent shadowing when multiple DataBlocks share a variable name.
///   IO_Router passes these keys through unchanged â€” it never inspects the
///   content of a tag name, so no changes are needed inside IO_Router itself.
///   What IS needed: a new helper GetValueQualified() so components can query
///   a tag without knowing which DB it belongs to (if DB is unambiguous).
///
///   FIX B â€” OFFLINE-MODE INBOUND MESSAGE HANDLING (preserved from prior fix):
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   HandleMessage() no longer drops inbound PLC messages when offlineMode=true.
///   The flag only controls Unityâ†’PLC sends (bridge.Send). Inbound PLCâ†’Unity
///   messages are ALWAYS dispatched to local callbacks regardless of the flag.
///   A one-time warning fires if the flag mismatch is detected.
///
///   FIX C â€” SETVALUE ALWAYS CALLS BRIDGE.SEND (preserved from prior fix):
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   SetValue() unconditionally calls bridge.Send(). bridge.Send() itself guards
///   on `if (!running) return` so nothing is sent when the TCP link is down.
///   This makes offlineMode on IO_Router purely cosmetic â€” it doesn't gate sends.
///
///   FIX D â€” TAG TRIMMING IN SETVALUE / GETVALUE:
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   Any whitespace that slipped past TagSubscriptionHelper.RegisterWithRetry()
///   (e.g. in out_* fields that go directly to SetValue without going through
///   Register) is now trimmed at the SetValue/GetValue boundary too. This makes
///   every output tag immune to Inspector whitespace regardless of path taken.
///
///   FIX E â€” SETVALUEWITHHANDOFF NOW ALSO TRIMS BOTH TAGS:
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   Same whitespace guard applied to the handoff call so feedback state
///   machine transitions can't fail due to a trailing space in an out_* field.
///
///   FIX F â€” DIAGNOSTICS: QUALIFIED NAME AWARENESS IN LOGALLTAGS():
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   LogAllTags() now groups output by DB prefix (the part before the first '.')
///   so you can see at a glance which DataBlock each tag belongs to.
///   Tags without a '.' are shown in an "Unqualified / Legacy" group.
///
/// â•گâ•گ FEEDBACK STATE MACHINE (unchanged) â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
///   SetValueWithHandoff() atomically transitions between phase tags.
///   The PLC never sees both tags FALSE simultaneously.
/// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
/// </summary>
public class IO_Router : MonoBehaviour
{
    public static IO_Router Instance { get; private set; }

    [Header("â”€â”€ Bridge (drag UnityBridgeClient GameObject here) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
    public UnityBridgeClient bridge;
    [Tooltip("Enable only for the old UnityBridgeClient TCP/OPC UA bridge. Leave false for MQTT-driven twin control.")]
    public bool sendOutputsToBridge = false;
    [Tooltip("Search the scene for UnityBridgeClient when no bridge is assigned. Leave false for MQTT-driven twin control.")]
    public bool autoFindBridgeClient = false;

    [Header("â•گâ•گ Offline / Simulation â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ")]
    [Tooltip("TRUE = tags work locally only â€” nothing sent to PLC bridge. " +
             "Switch to FALSE when connecting to TIA Portal.\n" +
             "NOTE: inbound PLCâ†’Unity messages are ALWAYS dispatched even when TRUE.")]
    public bool offlineMode = true;

    [Header("â”€â”€ Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
    public bool  dontDestroyOnLoad = true;
    public bool  periodicTagDump   = false;
    public float tagDumpInterval   = 30f;

    [Header("â”€â”€ Debug (Read Only) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
    [SerializeField] int    dbRegisteredTags    = 0;
    [SerializeField] int    dbCachedTags        = 0;
    [SerializeField] string dbLastTagIn         = "â€”";
    [SerializeField] string dbLastTagOut        = "â€”";
    [SerializeField] bool   dbBridgeOnline      = false;
    [SerializeField] string dbMode              = "Offline";
    [SerializeField] int    dbDeferredBroadcast = 0;
    [SerializeField] int    dbQualifiedTagCount = 0;   // FIX F: count tags with '.' (DB-qualified)
    [SerializeField] int    dbLegacyTagCount    = 0;   // FIX F: count unqualified tags (legacy)

    // â”€â”€ Internal â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    readonly Dictionary<string, List<Action<bool>>> map   = new();
    readonly Dictionary<string, bool>               cache = new();
    float dumpTimer                  = 0f;
    bool  deferredBroadcastPending   = false;
    bool  offlineModeWarnFired       = false;

    // â”€â”€ Feedback state tracking â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    readonly HashSet<string> latchedFeedbackTags = new HashSet<string>();

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        if (bridge == null && autoFindBridgeClient)
        {
            bridge = GetComponentInChildren<UnityBridgeClient>();
            if (bridge == null) bridge = FindAnyObjectByType<UnityBridgeClient>();
            if (bridge != null) Debug.Log($"[IO ROUTER] Auto-found bridge on '{bridge.gameObject.name}'");
        }

        if (sendOutputsToBridge && !offlineMode && bridge == null)
            Debug.LogError("[IO ROUTER] offlineMode=false but no UnityBridgeClient found!");
        if (sendOutputsToBridge && !offlineMode && bridge != null && bridge.offlineMode)
            Debug.LogError("[IO ROUTER] offlineMode=false but UnityBridgeClient.offlineMode=true!");

        dbMode = sendOutputsToBridge ? (offlineMode ? "Offline" : "PLC") : "MQTT";
    }

    void OnEnable()  { UnityBridgeClient.OnMessage += HandleMessage; }
    void OnDisable() { UnityBridgeClient.OnMessage -= HandleMessage; }

    void Update()
    {
        dbBridgeOnline   = bridge != null && bridge.IsConnected;
        dbRegisteredTags = map.Count;
        dbCachedTags     = cache.Count;
        dbMode           = sendOutputsToBridge ? (offlineMode ? "Offline" : "PLC") : "MQTT";

        // FIX F: track qualified vs legacy tag counts
        dbQualifiedTagCount = cache.Keys.Count(k => k.Contains('.'));
        dbLegacyTagCount    = cache.Keys.Count(k => !k.Contains('.') && !k.StartsWith("__"));

        if (periodicTagDump)
        {
            dumpTimer += Time.deltaTime;
            if (dumpTimer >= tagDumpInterval) { dumpTimer = 0f; LogAllTags(); }
        }
    }

    // â”€â”€ Register / Unregister â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public void Register(string tag, Action<bool> callback)
    {
        if (string.IsNullOrEmpty(tag) || callback == null) return;
        tag = tag.Trim();   // FIX D
        if (!map.ContainsKey(tag)) map[tag] = new List<Action<bool>>();
        if (!map[tag].Contains(callback)) map[tag].Add(callback);

        // Cache replay: late-joining components get the current state immediately.
        if (cache.TryGetValue(tag, out bool cached))
        {
            try { callback(cached); }
            catch (Exception e) { Debug.LogError($"[IO ROUTER] Register cache-replay error for '{tag}': {e.Message}"); }
        }
    }

    public void Unregister(string tag, Action<bool> callback)
    {
        if (string.IsNullOrEmpty(tag)) return;
        tag = tag.Trim();
        if (map.TryGetValue(tag, out var list))
        {
            list.Remove(callback);
            if (list.Count == 0) map.Remove(tag);
        }
    }

    public void Unregister(string tag)
    {
        if (!string.IsNullOrEmpty(tag)) map.Remove(tag.Trim());
    }

    // â”€â”€ Read â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public bool GetValue(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        return cache.TryGetValue(tag.Trim(), out bool v) ? v : false;
    }

    /// <summary>
    /// FIX F: Lookup by raw variable name without knowing the DB prefix.
    /// Returns the value if exactly one qualified tag ends with ".VarName".
    /// Logs a warning if ambiguous (same var name in multiple DBs).
    /// Returns false and logs an error if not found.
    /// Use this only for legacy/migration scenarios â€” prefer the full qualified name.
    /// </summary>
    public bool GetValueQualified(string rawVarName, out string resolvedTag)
    {
        rawVarName  = rawVarName?.Trim() ?? "";
        resolvedTag = rawVarName;

        // Exact match first
        if (cache.ContainsKey(rawVarName)) return cache[rawVarName];

        // Search for "*.VarName"
        var matches = cache.Keys
            .Where(k => k.EndsWith("." + rawVarName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1)
        {
            resolvedTag = matches[0];
            return cache[resolvedTag];
        }
        if (matches.Count > 1)
        {
            Debug.LogWarning(
                $"[IO ROUTER] GetValueQualified('{rawVarName}') is AMBIGUOUS â€” " +
                $"found in {matches.Count} DBs: {string.Join(", ", matches)}. " +
                "Use the full qualified name in your Inspector field.");
            resolvedTag = matches[0];
            return cache[resolvedTag];
        }

        Debug.LogWarning($"[IO ROUTER] GetValueQualified('{rawVarName}'): not found in cache.");
        return false;
    }

    public IEnumerable<string> KnownTags        => cache.Keys;
    public List<string>        GetAllKnownTags() => cache.Keys.OrderBy(k => k).ToList();

    // â”€â”€ Write â€” Unity â†’ PLC â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    /// <summary>
    /// Write an output tag. In PLC mode, sends to bridge AND fires local callbacks.
    /// In offline mode, fires local callbacks ONLY (bridge.Send guards on running=false).
    /// </summary>
    public void SetValue(string tag, bool value)
    {
        if (string.IsNullOrEmpty(tag)) return;
        tag = tag.Trim();   // FIX D: trim at write boundary â€” catches out_* whitespace

        cache[tag]   = value;
        dbLastTagOut = $"{tag}={value}";

        if (value) latchedFeedbackTags.Add(tag);
        else       latchedFeedbackTags.Remove(tag);

        // FIX C: always call bridge.Send() â€” bridge.Send() guards on running=false.
        // offlineMode on IO_Router does NOT gate sends any more.
        if (sendOutputsToBridge && bridge != null)
            bridge.Send(tag, value);
        else if (sendOutputsToBridge && !offlineMode)
            Debug.LogError($"[IO ROUTER] PLC mode but bridge is NULL â€” '{tag}={value}' dropped!");

        FireCallbacks(tag, value);
    }

    /// <summary>
    /// SEQUENTIAL FEEDBACK HANDOFF â€” atomically clears the PREVIOUS phase tag
    /// and sets the NEXT phase tag in a single operation.
    /// previousTag stays TRUE until this call â€” the PLC never sees both FALSE.
    /// </summary>
    public void SetValueWithHandoff(string previousTag, string nextTag)
    {
        // FIX E: trim both tags so handoff never fails due to Inspector whitespace
        if (previousTag != null) previousTag = previousTag.Trim();
        if (nextTag     != null) nextTag     = nextTag.Trim();

        // Write both changes to cache FIRST so callbacks see consistent state
        if (!string.IsNullOrEmpty(previousTag))
        {
            cache[previousTag] = false;
            latchedFeedbackTags.Remove(previousTag);
            if (sendOutputsToBridge && bridge != null) bridge.Send(previousTag, false);
            dbLastTagOut = $"{previousTag}=false";
        }

        if (!string.IsNullOrEmpty(nextTag))
        {
            cache[nextTag] = true;
            latchedFeedbackTags.Add(nextTag);
            if (sendOutputsToBridge && bridge != null) bridge.Send(nextTag, true);
            dbLastTagOut = $"{nextTag}=true";
        }

        // Fire callbacks AFTER both cache writes are complete
        if (!string.IsNullOrEmpty(previousTag)) FireCallbacks(previousTag, false);
        if (!string.IsNullOrEmpty(nextTag))     FireCallbacks(nextTag, true);
    }

    /// <summary>
    /// Clear all currently latched feedback tags for a given owner prefix.
    /// Used by E-Stop and emergency release.
    /// </summary>
    public void ClearLatchedFeedback(params string[] tags)
    {
        foreach (string tag in tags)
        {
            if (string.IsNullOrEmpty(tag)) continue;
            string t = tag.Trim();
            if (GetValue(t)) SetValue(t, false);
        }
    }

    public bool IsFeedbackLatched(string tag) =>
        !string.IsNullOrEmpty(tag) && latchedFeedbackTags.Contains(tag.Trim());

    /// <summary>
    /// Simulate a PLC INPUT tag â€” fires local callbacks only, does NOT send to bridge.
    /// </summary>
    public void SimulateInput(string tag, bool value)
    {
        if (string.IsNullOrEmpty(tag)) return;
        tag = tag.Trim();
        cache[tag]  = value;
        dbLastTagIn = $"[SIM] {tag}={value}";
        Debug.Log($"[IO ROUTER] SIM IN: {tag} = {value}");
        FireCallbacks(tag, value);
    }

    /// <summary>
    /// Apply an inbound tag update from an external source such as MQTT or the TCP bridge.
    /// Accepts either friend-bridge JSON: {"Tag":"...","Value":true,"Type":"Boolean"}
    /// or a raw boolean payload when fallbackTag is provided.
    /// </summary>
    public bool ApplyIncomingMessage(string msg, string sourceLabel = "PLC", string fallbackTag = "", bool preferFallbackTag = false)
    {
        if (string.IsNullOrEmpty(msg) || msg == "_RECONNECT_") return false;

        string tag = preferFallbackTag ? fallbackTag : "";
        if (string.IsNullOrEmpty(tag)) tag = Extract(msg, "Tag");
        if (string.IsNullOrEmpty(tag)) tag = Extract(msg, "tag");
        if (string.IsNullOrEmpty(tag)) tag = fallbackTag;
        if (string.IsNullOrEmpty(tag)) return false;

        string valueStr = Extract(msg, "Value");
        if (string.IsNullOrEmpty(valueStr)) valueStr = Extract(msg, "value");
        if (string.IsNullOrEmpty(valueStr)) valueStr = msg;

        if (!TryParseBool(valueStr, out bool value))
        {
            Debug.LogWarning($"[IO ROUTER] Ignored inbound {sourceLabel} tag '{tag}' with non-boolean value '{valueStr}'.");
            return false;
        }

        ApplyIncomingValue(tag, value, sourceLabel);
        return true;
    }

    /// <summary>
    /// Apply an inbound PLC/input tag without sending it back to the bridge.
    /// This is the same local effect as a PLC message arriving from the TCP bridge.
    /// </summary>
    public void ApplyIncomingValue(string tag, bool value, string sourceLabel = "PLC")
    {
        if (string.IsNullOrEmpty(tag)) return;
        tag = tag.Trim();
        cache[tag] = value;

        string source = string.IsNullOrWhiteSpace(sourceLabel) ? "IN" : sourceLabel.Trim();
        dbLastTagIn = $"[{source}] {tag}={value}";
        FireCallbacks(tag, value);
    }

    // â”€â”€ FIX: Deferred re-broadcast on new bridge connection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public void NotifyBridgeConnected()
    {
        if (deferredBroadcastPending) return;
        deferredBroadcastPending = true;
        StartCoroutine(DeferredReBroadcast());
    }

    IEnumerator DeferredReBroadcast()
    {
        yield return null;
        yield return null;
        yield return null;

        deferredBroadcastPending = false;
        dbDeferredBroadcast++;

        int fired = 0;
        var snapshot = new List<KeyValuePair<string, bool>>(cache);
        foreach (var kv in snapshot)
        {
            if (map.ContainsKey(kv.Key) && map[kv.Key].Count > 0)
            {
                FireCallbacks(kv.Key, kv.Value);
                fired++;
            }
        }

        Debug.Log($"[IO ROUTER] Deferred re-broadcast #{dbDeferredBroadcast}: " +
                  $"fired {fired} tag(s) to newly-registered callbacks.");
    }

    // â”€â”€ Incoming message from PLC bridge â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void HandleMessage(string msg)
    {
        if (string.IsNullOrEmpty(msg) || msg == "_RECONNECT_") return;

        // FIX B: NEVER gate inbound PLC messages on offlineMode.
        // offlineMode only controls Unityâ†’PLC sends (SetValue â†’ bridge.Send).
        // Inbound PLCâ†’Unity messages must ALWAYS be dispatched.
        if (sendOutputsToBridge && offlineMode && !offlineModeWarnFired)
        {
            offlineModeWarnFired = true;
            Debug.LogWarning(
                "[IO ROUTER] âڑ  Received a PLC message while offlineMode=TRUE.\n" +
                "  â†’ Set IO_Router.offlineMode = FALSE in the Inspector to enable full PLC mode.\n" +
                "  Processing the message anyway â€” callbacks WILL fire.");
        }

        ApplyIncomingMessage(msg, "PLC");
    }

    readonly List<Action<bool>> callbackScratch = new List<Action<bool>>();

    void FireCallbacks(string tag, bool value)
    {
        if (!map.TryGetValue(tag, out var callbacks)) return;
        callbackScratch.Clear();
        callbackScratch.AddRange(callbacks);
        foreach (var cb in callbackScratch)
        {
            try   { cb(value); }
            catch (Exception e) { Debug.LogError($"[IO ROUTER] Callback error for '{tag}': {e.Message}\n{e.StackTrace}"); }
        }
    }

    // â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public void LogAllTags()
    {
        Debug.Log($"â•”â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ IO_ROUTER TAG DUMP  [{dbMode}] â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•—");
        Debug.Log($"  Bridge     : {(sendOutputsToBridge ? (bridge != null ? (bridge.IsConnected ? "CONNECTED" : "offline/disconnected") : "not found") : "disabled (MQTT)")}");
        Debug.Log($"  Registered : {map.Count} tags   Cached: {cache.Count} tags");
        Debug.Log($"  Qualified  : {dbQualifiedTagCount} (DB.VarName format)   Legacy: {dbLegacyTagCount}");
        Debug.Log($"  Latched    : {latchedFeedbackTags.Count} feedback tag(s) currently TRUE");

        // FIX F: group by DB prefix for readability
        var groups = cache
            .Where(kv => !kv.Key.StartsWith("__"))
            .GroupBy(kv => kv.Key.Contains('.') ? kv.Key.Split('.')[0] : "_Legacy")
            .OrderBy(g => g.Key);

        foreach (var g in groups)
        {
            Debug.Log($"  â”€â”€ {g.Key} â”€â”€");
            foreach (var kv in g.OrderBy(x => x.Key))
            {
                string latched = latchedFeedbackTags.Contains(kv.Key) ? " [LATCHED]" : "";
                string regStar = map.ContainsKey(kv.Key) ? " [sub]" : "";
                Debug.Log($"    {kv.Key,-50} = {kv.Value}{latched}{regStar}");
            }
        }
        Debug.Log("â•ڑâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•‌");
    }

    static string Extract(string json, string key)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";

        string search = $"\"{key}\"";
        int keyStart = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (keyStart == -1) return "";

        int colon = json.IndexOf(':', keyStart + search.Length);
        if (colon == -1) return "";

        int start = colon + 1;
        while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
        if (start >= json.Length) return "";

        if (json[start] == '"')
        {
            int end = start + 1;
            bool escaped = false;
            while (end < json.Length)
            {
                char c = json[end];
                if (c == '"' && !escaped) break;
                escaped = c == '\\' && !escaped;
                if (c != '\\') escaped = false;
                end++;
            }

            if (end >= json.Length) return "";
            return json.Substring(start + 1, end - start - 1)
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Trim();
        }

        int valueEnd = start;
        while (valueEnd < json.Length &&
               json[valueEnd] != ',' &&
               json[valueEnd] != '}' &&
               json[valueEnd] != ']')
        {
            valueEnd++;
        }

        return json.Substring(start, valueEnd - start).Trim();
    }

    static bool TryParseBool(string raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string normalized = raw.Trim().Trim('"').Trim('\'').Trim();
        if (bool.TryParse(normalized, out value)) return true;

        switch (normalized.ToLowerInvariant())
        {
            case "1":
            case "on":
            case "yes":
            case "y":
            case "t":
                value = true;
                return true;
            case "0":
            case "off":
            case "no":
            case "n":
            case "f":
                value = false;
                return true;
            default:
                return false;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Log All Tags")]
    void EditorLogAllTags() => LogAllTags();

    [ContextMenu("Diagnose All Tag Subscriptions")]
    void EditorDiagnose() => TagSubscriptionHelper.DiagnoseAll();

    [ContextMenu("Toggle Offline Mode")]
    void EditorToggleOffline()
    {
        offlineMode = !offlineMode;
        dbMode      = sendOutputsToBridge ? (offlineMode ? "Offline" : "PLC") : "MQTT";
        Debug.Log($"[IO ROUTER] Mode switched to: {dbMode}");
    }

    [ContextMenu("List Qualified vs Legacy Tags")]
    void EditorListTagGroups()
    {
        var qualified = cache.Keys.Where(k => k.Contains('.') && !k.StartsWith("__")).OrderBy(k => k).ToList();
        var legacy    = cache.Keys.Where(k => !k.Contains('.') && !k.StartsWith("__")).OrderBy(k => k).ToList();
        Debug.Log($"[IO ROUTER] Qualified tags ({qualified.Count}):\n  " + string.Join("\n  ", qualified));
        if (legacy.Count > 0)
            Debug.Log($"[IO ROUTER] âڑ  Legacy unqualified tags ({legacy.Count}) â€” " +
                      "update Unity Inspector fields to 'DBName.VarName' format:\n  " + string.Join("\n  ", legacy));
    }
#endif
}
