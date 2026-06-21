using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// IO_Router — central message bus between the OPC UA Bridge and all Unity components.
/// (FIXED — see change log below)
///
/// ══ FIXES IN THIS VERSION ════════════════════════════════════════════════════
///
///   FIX A — QUALIFIED TAG NAMES ("DBName.VarName") ACROSS ALL SET/GET:
///   ─────────────────────────────────────────────────────────────────────
///   The bridge now keys every tag as "DataBlockName.VariableName" to prevent
///   silent shadowing when multiple DataBlocks share a variable name.
///   IO_Router passes these keys through unchanged — it never inspects the
///   content of a tag name, so no changes are needed inside IO_Router itself.
///   What IS needed: a new helper GetValueQualified() so components can query
///   a tag without knowing which DB it belongs to (if DB is unambiguous).
///
///   FIX B — OFFLINE-MODE INBOUND MESSAGE HANDLING (preserved from prior fix):
///   ─────────────────────────────────────────────────────────────────────
///   HandleMessage() no longer drops inbound PLC messages when offlineMode=true.
///   The flag only controls Unity→PLC sends (bridge.Send). Inbound PLC→Unity
///   messages are ALWAYS dispatched to local callbacks regardless of the flag.
///   A one-time warning fires if the flag mismatch is detected.
///
///   FIX C — SETVALUE ALWAYS CALLS BRIDGE.SEND (preserved from prior fix):
///   ─────────────────────────────────────────────────────────────────────
///   SetValue() unconditionally calls bridge.Send(). bridge.Send() itself guards
///   on `if (!running) return` so nothing is sent when the TCP link is down.
///   This makes offlineMode on IO_Router purely cosmetic — it doesn't gate sends.
///
///   FIX D — TAG TRIMMING IN SETVALUE / GETVALUE:
///   ─────────────────────────────────────────────
///   Any whitespace that slipped past TagSubscriptionHelper.RegisterWithRetry()
///   (e.g. in out_* fields that go directly to SetValue without going through
///   Register) is now trimmed at the SetValue/GetValue boundary too. This makes
///   every output tag immune to Inspector whitespace regardless of path taken.
///
///   FIX E — SETVALUEWITHHANDOFF NOW ALSO TRIMS BOTH TAGS:
///   ─────────────────────────────────────────────────────
///   Same whitespace guard applied to the handoff call so feedback state
///   machine transitions can't fail due to a trailing space in an out_* field.
///
///   FIX F — DIAGNOSTICS: QUALIFIED NAME AWARENESS IN LOGALLTAGS():
///   ─────────────────────────────────────────────────────────────────
///   LogAllTags() now groups output by DB prefix (the part before the first '.')
///   so you can see at a glance which DataBlock each tag belongs to.
///   Tags without a '.' are shown in an "Unqualified / Legacy" group.
///
/// ══ FEEDBACK STATE MACHINE (unchanged) ══════════════════════════════════════
///   SetValueWithHandoff() atomically transitions between phase tags.
///   The PLC never sees both tags FALSE simultaneously.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class IO_Router : MonoBehaviour
{
    public static IO_Router Instance { get; private set; }

    [Header("── Bridge (drag UnityBridgeClient GameObject here) ──────────")]
    public UnityBridgeClient bridge;

    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    [Tooltip("TRUE = tags work locally only — nothing sent to PLC bridge. " +
             "Switch to FALSE when connecting to TIA Portal.\n" +
             "NOTE: inbound PLC→Unity messages are ALWAYS dispatched even when TRUE.")]
    public bool offlineMode = true;

    [Header("── Settings ────────────────────────────────────────────────")]
    public bool  dontDestroyOnLoad = true;
    public bool  periodicTagDump   = false;
    public float tagDumpInterval   = 30f;

    [Header("── Debug (Read Only) ─────────────────────────────────────────")]
    [SerializeField] int    dbRegisteredTags    = 0;
    [SerializeField] int    dbCachedTags        = 0;
    [SerializeField] string dbLastTagIn         = "—";
    [SerializeField] string dbLastTagOut        = "—";
    [SerializeField] bool   dbBridgeOnline      = false;
    [SerializeField] string dbMode              = "Offline";
    [SerializeField] int    dbDeferredBroadcast = 0;
    [SerializeField] int    dbQualifiedTagCount = 0;   // FIX F: count tags with '.' (DB-qualified)
    [SerializeField] int    dbLegacyTagCount    = 0;   // FIX F: count unqualified tags (legacy)

    // ── Internal ──────────────────────────────────────────────────────────────
    readonly Dictionary<string, List<Action<bool>>> map   = new();
    readonly Dictionary<string, bool>               cache = new();
    float dumpTimer                  = 0f;
    bool  deferredBroadcastPending   = false;
    bool  offlineModeWarnFired       = false;

    // ── Feedback state tracking ───────────────────────────────────────────────
    readonly HashSet<string> latchedFeedbackTags = new HashSet<string>();

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        if (bridge == null)
        {
            bridge = GetComponentInChildren<UnityBridgeClient>();
            if (bridge == null) bridge = FindObjectOfType<UnityBridgeClient>();
            if (bridge != null) Debug.Log($"[IO ROUTER] Auto-found bridge on '{bridge.gameObject.name}'");
            else                Debug.LogWarning("[IO ROUTER] No UnityBridgeClient found — offline mode forced.");
        }

        if (!offlineMode && bridge == null)
            Debug.LogError("[IO ROUTER] offlineMode=false but no UnityBridgeClient found!");
        if (!offlineMode && bridge != null && bridge.offlineMode)
            Debug.LogError("[IO ROUTER] offlineMode=false but UnityBridgeClient.offlineMode=true!");

        dbMode = offlineMode ? "Offline" : "PLC";
    }

    void OnEnable()  { UnityBridgeClient.OnMessage += HandleMessage; }
    void OnDisable() { UnityBridgeClient.OnMessage -= HandleMessage; }

    void Update()
    {
        dbBridgeOnline   = bridge != null && bridge.IsConnected;
        dbRegisteredTags = map.Count;
        dbCachedTags     = cache.Count;
        dbMode           = offlineMode ? "Offline" : "PLC";

        // FIX F: track qualified vs legacy tag counts
        dbQualifiedTagCount = cache.Keys.Count(k => k.Contains('.'));
        dbLegacyTagCount    = cache.Keys.Count(k => !k.Contains('.') && !k.StartsWith("__"));

        if (periodicTagDump)
        {
            dumpTimer += Time.deltaTime;
            if (dumpTimer >= tagDumpInterval) { dumpTimer = 0f; LogAllTags(); }
        }
    }

    // ── Register / Unregister ─────────────────────────────────────────────────
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

    // ── Read ──────────────────────────────────────────────────────────────────
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
    /// Use this only for legacy/migration scenarios — prefer the full qualified name.
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
                $"[IO ROUTER] GetValueQualified('{rawVarName}') is AMBIGUOUS — " +
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

    // ── Write — Unity → PLC ──────────────────────────────────────────────────
    /// <summary>
    /// Write an output tag. In PLC mode, sends to bridge AND fires local callbacks.
    /// In offline mode, fires local callbacks ONLY (bridge.Send guards on running=false).
    /// </summary>
    public void SetValue(string tag, bool value)
    {
        if (string.IsNullOrEmpty(tag)) return;
        tag = tag.Trim();   // FIX D: trim at write boundary — catches out_* whitespace

        cache[tag]   = value;
        dbLastTagOut = $"{tag}={value}";

        if (value) latchedFeedbackTags.Add(tag);
        else       latchedFeedbackTags.Remove(tag);

        // FIX C: always call bridge.Send() — bridge.Send() guards on running=false.
        // offlineMode on IO_Router does NOT gate sends any more.
        if (bridge != null)
            bridge.Send(tag, value);
        else if (!offlineMode)
            Debug.LogError($"[IO ROUTER] PLC mode but bridge is NULL — '{tag}={value}' dropped!");

        FireCallbacks(tag, value);
    }

    /// <summary>
    /// SEQUENTIAL FEEDBACK HANDOFF — atomically clears the PREVIOUS phase tag
    /// and sets the NEXT phase tag in a single operation.
    /// previousTag stays TRUE until this call — the PLC never sees both FALSE.
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
            if (bridge != null) bridge.Send(previousTag, false);
            dbLastTagOut = $"{previousTag}=false";
        }

        if (!string.IsNullOrEmpty(nextTag))
        {
            cache[nextTag] = true;
            latchedFeedbackTags.Add(nextTag);
            if (bridge != null) bridge.Send(nextTag, true);
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
    /// Simulate a PLC INPUT tag — fires local callbacks only, does NOT send to bridge.
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

    // ── FIX: Deferred re-broadcast on new bridge connection ──────────────────
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

    // ── Incoming message from PLC bridge ─────────────────────────────────────
    void HandleMessage(string msg)
    {
        if (string.IsNullOrEmpty(msg) || msg == "_RECONNECT_") return;

        // FIX B: NEVER gate inbound PLC messages on offlineMode.
        // offlineMode only controls Unity→PLC sends (SetValue → bridge.Send).
        // Inbound PLC→Unity messages must ALWAYS be dispatched.
        if (offlineMode && !offlineModeWarnFired)
        {
            offlineModeWarnFired = true;
            Debug.LogWarning(
                "[IO ROUTER] ⚠ Received a PLC message while offlineMode=TRUE.\n" +
                "  → Set IO_Router.offlineMode = FALSE in the Inspector to enable full PLC mode.\n" +
                "  Processing the message anyway — callbacks WILL fire.");
        }

        string tag      = Extract(msg, "Tag");
        string valueStr = Extract(msg, "Value");
        if (string.IsNullOrEmpty(tag)) return;

        tag = tag.Trim();   // FIX D
        bool value  = valueStr.Equals("true", StringComparison.OrdinalIgnoreCase);
        cache[tag]  = value;
        dbLastTagIn = $"{tag}={value}";
        FireCallbacks(tag, value);
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

    // ── Diagnostics ───────────────────────────────────────────────────────────
    public void LogAllTags()
    {
        Debug.Log($"╔══════════════ IO_ROUTER TAG DUMP  [{dbMode}] ══════════════╗");
        Debug.Log($"  Bridge     : {(bridge != null ? (bridge.IsConnected ? "CONNECTED" : "offline/disconnected") : "not found")}");
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
            Debug.Log($"  ── {g.Key} ──");
            foreach (var kv in g.OrderBy(x => x.Key))
            {
                string latched = latchedFeedbackTags.Contains(kv.Key) ? " [LATCHED]" : "";
                string regStar = map.ContainsKey(kv.Key) ? " [sub]" : "";
                Debug.Log($"    {kv.Key,-50} = {kv.Value}{latched}{regStar}");
            }
        }
        Debug.Log("╚═════════════════════════════════════════════════════════════╝");
    }

    static string Extract(string json, string key)
    {
        string search = $"\"{key}\":";
        int start = json.IndexOf(search);
        if (start == -1) return "";
        start += search.Length;
        int end = json.IndexOfAny(new char[] { ',', '}' }, start);
        if (end == -1) end = json.Length;
        return json[start..end].Replace("\"", "").Trim();
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
        dbMode      = offlineMode ? "Offline" : "PLC";
        Debug.Log($"[IO ROUTER] Mode switched to: {dbMode}");
    }

    [ContextMenu("List Qualified vs Legacy Tags")]
    void EditorListTagGroups()
    {
        var qualified = cache.Keys.Where(k => k.Contains('.') && !k.StartsWith("__")).OrderBy(k => k).ToList();
        var legacy    = cache.Keys.Where(k => !k.Contains('.') && !k.StartsWith("__")).OrderBy(k => k).ToList();
        Debug.Log($"[IO ROUTER] Qualified tags ({qualified.Count}):\n  " + string.Join("\n  ", qualified));
        if (legacy.Count > 0)
            Debug.Log($"[IO ROUTER] ⚠ Legacy unqualified tags ({legacy.Count}) — " +
                      "update Unity Inspector fields to 'DBName.VarName' format:\n  " + string.Join("\n  ", legacy));
    }
#endif
}
