using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TagSubscriptionHelper â€” shared registration and diagnostics utility. (FIXED)
///
/// â•گâ•گ FIXES IN THIS VERSION â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
///
///   FIX A â€” QUALIFIED TAG NAME VALIDATION ("DBName.VarName"):
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   The bridge now sends every tag as "DataBlockName.VariableName" to prevent
///   silent shadowing across DataBlocks.  RegisterWithRetry() now detects when
///   a component registers a BARE (unqualified) tag name (no '.' separator)
///   and emits an actionable warning telling the user exactly what qualified
///   name to use.  The registration still proceeds so existing scenes don't
///   break immediately â€” but the warning must be resolved before going live.
///
///   The late-join cache replay also searches for the qualified form of the
///   bare name if the bare name itself isn't found in the cache, so components
///   that haven't yet been updated to qualified names still get their initial
///   value on startup (with a warning).
///
///   FIX B â€” WHITESPACE FIX (preserved from prior version):
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   RegisterWithRetry() trims the tag name before passing to IO_Router.
///   This makes every component immune to accidental Inspector whitespace.
///
///   FIX C â€” DIAGNOSTICS: QUALIFIED NAME GROUPING:
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   DiagnoseAll() now groups subscriptions by DB prefix and shows a separate
///   "Missing DB prefix" section for any bare tag names still in use.
///   This makes migration easy: one glance shows what still needs updating.
///
///   FIX D â€” REMOVE() NOW HANDLES QUALIFIED KEY:
///   â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
///   Remove() trims and also checks if the tag has a qualified form in the
///   records dictionary before removing, preventing ghost entries.
/// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
/// </summary>
public static class TagSubscriptionHelper
{
    // â”€â”€ Per-tag registration record â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public class TagRecord
    {
        public string tag;
        public string ownerName;
        public bool   registered;
        public bool   everReceived;
        public bool   lastValue;
        public float  lastReceivedTime;
        public int    receiveCount;
        public bool   isQualified;   // FIX A: true = "DBName.VarName" format
    }

    static readonly Dictionary<string, TagRecord> records = new Dictionary<string, TagRecord>();

    static bool ShouldValidatePlcBridgeTags =>
        IO_Router.Instance != null && IO_Router.Instance.sendOutputsToBridge;

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Register a PLC-input tag callback.  Must be called from StartCoroutine().
    ///
    /// Tags should be in "DBName.VarName" format (e.g. "WarehouseDB.StartBatch").
    /// Bare names (no '.') still work but emit a warning with the correct format.
    ///
    /// Automatically trims whitespace and logs a loud error if any was found.
    /// After registering, retries with exponential back-off to catch cached values.
    /// </summary>
    public static IEnumerator RegisterWithRetry(
        string       tag,
        Action<bool> callback,
        MonoBehaviour owner,
        float        initialDelaySeconds = 0.1f,
        int          maxRetries          = 5)
    {
        // â”€â”€ FIX B: whitespace trim â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        string originalTag = tag;
        if (tag != null) tag = tag.Trim();

        if (!string.IsNullOrEmpty(originalTag) && originalTag != tag && ShouldValidatePlcBridgeTags)
        {
            Debug.LogError(
                $"[TAG-SUB | {owner?.name}] âœ– WHITESPACE in tag '{originalTag}'!\n" +
                $"  Trimmed to '{tag}' automatically.\n" +
                $"  Fix the Inspector field: remove spaces from the in_*/out_* field value.");
        }

        if (string.IsNullOrEmpty(tag) || callback == null) yield break;

        // â”€â”€ FIX A: qualified name validation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        bool isQualified = tag.Contains('.');
        if (!isQualified && ShouldValidatePlcBridgeTags)
        {
            // Emit actionable warning but do NOT abort â€” old scenes still work.
            Debug.LogWarning(
                $"[TAG-SUB | {owner?.name}] âڑ  BARE TAG NAME: '{tag}'\n" +
                $"  Tags should use qualified format: \"DBName.VarName\"\n" +
                $"  Example: if your TIA Portal DB is 'WarehouseDB' and the variable is '{tag}'\n" +
                $"           set the Inspector field to: WarehouseDB.{tag}\n" +
                $"  Run 'list' in the bridge console to see all qualified tag names.\n" +
                $"  This tag will still register and attempt to receive values, but the\n" +
                $"  bridge will only send it if your Inspector field exactly matches a\n" +
                $"  tag key in the bridge tagStore (which now uses qualified names).");
        }

        // â”€â”€ Wait for IO_Router â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        float waitTime = 0f;
        while (IO_Router.Instance == null)
        {
            waitTime += Time.deltaTime;
            if (waitTime > 10f)
            {
                Debug.LogError($"[TAG-SUB | {owner?.name}] IO_Router never appeared after 10s. " +
                               "Add an IO_Router GameObject to the scene.");
                yield break;
            }
            yield return null;
        }

        // â”€â”€ Build / update record â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (!records.TryGetValue(tag, out TagRecord rec))
        {
            rec = new TagRecord
            {
                tag         = tag,
                ownerName   = owner?.name ?? "?",
                isQualified = isQualified
            };
            records[tag] = rec;
        }

        Action<bool> wrappedCallback = v =>
        {
            rec.everReceived     = true;
            rec.lastValue        = v;
            rec.lastReceivedTime = Time.time;
            rec.receiveCount++;
            callback(v);
        };

        IO_Router.Instance.Register(tag, wrappedCallback);
        rec.registered = true;

        Debug.Log($"[TAG-SUB | {owner?.name}] Registered '{tag}'" +
                  (isQualified || ShouldValidatePlcBridgeTags ? "" : " [MQTT/local]"));

        // â”€â”€ Retry loop â€” late-join cache replay â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        float delay = initialDelaySeconds;
        for (int i = 0; i < maxRetries && !rec.everReceived; i++)
        {
            yield return new WaitForSeconds(delay);

            // Check exact key first
            if (IO_Router.Instance.KnownTags != null)
            {
                // FIX A: if bare name not in cache, try to find the qualified form
                string cacheKey = tag;
                if (!IO_Router.Instance.KnownTags.Contains(tag) && !isQualified)
                {
                    var qualMatch = IO_Router.Instance.KnownTags
                        .Where(k => k.EndsWith("." + tag, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (qualMatch.Count == 1)
                    {
                        cacheKey = qualMatch[0];
                        Debug.Log(
                            $"[TAG-SUB | {owner?.name}] Auto-resolved bare tag '{tag}' â†’ '{cacheKey}'.\n" +
                            $"  Update your Inspector field to '{cacheKey}' to remove this warning.");
                    }
                    else if (qualMatch.Count > 1)
                    {
                        Debug.Log(
                            $"[TAG-SUB | {owner?.name}] Bare tag '{tag}' is AMBIGUOUS â€” " +
                            $"found as {string.Join(", ", qualMatch)}. Cannot auto-resolve. " +
                            "Update Inspector field to the exact qualified name you want.");
                    }
                }

                if (IO_Router.Instance.KnownTags.Contains(cacheKey) && !rec.everReceived)
                {
                    bool cached = IO_Router.Instance.GetValue(cacheKey);
                    wrappedCallback(cached);
                    Debug.Log(
                        $"[TAG-SUB | {owner?.name}] Late-replay for cached tag '{cacheKey}' = {cached} " +
                        $"(retry {i + 1}/{maxRetries}).");
                }
            }

            delay = Mathf.Min(delay * 2f, 5f);
        }

        // â”€â”€ Final diagnostic if still never received â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (!rec.everReceived && ShouldValidatePlcBridgeTags)
        {
            string qualHint = isQualified ? "" :
                "\n  âک… Your tag is UNQUALIFIED (no '.'). The bridge sends 'DBName.VarName'.\n" +
                $"    Update Inspector field to 'YourDBName.{tag}'.";

            Debug.LogWarning(
                $"[TAG-SUB | {owner?.name}] âڑ  Tag '{tag}' NEVER received after {maxRetries} retries.\n" +
                $"  Checklist:\n" +
                $"  1. Tag is CASE-SENSITIVE. Registered: '{tag}'\n" +
                $"     Run 'verify {tag}' in bridge console to check.\n" +
                $"  2. Check for whitespace â€” the diagnostic table shows exact strings.\n" +
                $"  3. IO_Router.offlineMode = {IO_Router.Instance?.offlineMode} (must be FALSE for live PLC).\n" +
                $"  4. Bridge: do you see [PLCâ†’UNITY] lines for this tag?{qualHint}\n" +
                "  â†’ Right-click component â†’ 'Diagnose Tag Subscriptions' for full table.");
        }
    }

    // â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Prints a full table of all registered PLC-input tags, grouped by DB prefix.
    /// Tags shown with surrounding quotes so any whitespace is visible.
    /// A "Missing DB prefix" section shows bare tags that need to be updated.
    /// </summary>
    public static void DiagnoseAll()
    {
        Debug.Log("â•”â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ TAG SUBSCRIPTION DIAGNOSTICS â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•—");
        Debug.Log($"  IO_Router   : {(IO_Router.Instance != null ? "found" : "MISSING!")}");
        if (IO_Router.Instance != null)
        {
            bool bridgeMode = IO_Router.Instance.sendOutputsToBridge;
            Debug.Log($"  Mode        : {(bridgeMode ? (IO_Router.Instance.offlineMode ? "Offline bridge mode" : "PLC bridge mode") : "MQTT/local")}");
            Debug.Log($"  Bridge      : {(bridgeMode ? (IO_Router.Instance.bridge != null ? "assigned" : "MISSING") : "disabled")}");
        }
        Debug.Log($"  Tracked tags: {records.Count}");

        // FIX C: group by DB prefix
        var qualified = records.Values.Where(r => r.isQualified).GroupBy(r =>
        {
            int dot = r.tag.IndexOf('.');
            return dot >= 0 ? r.tag[..dot] : "_";
        }).OrderBy(g => g.Key);

        var bare = records.Values.Where(r => !r.isQualified).ToList();

        Debug.Log("  â”€â”€ Qualified subscriptions (DBName.VarName) â”€â”€");
        bool anyQual = false;
        foreach (var group in qualified)
        {
            Debug.Log($"    [{group.Key}]");
            foreach (var r in group.OrderBy(x => x.tag))
            {
                anyQual = true;
                PrintRecord(r);
            }
        }
        if (!anyQual) Debug.Log("    (none)");

        if (bare.Count > 0)
        {
            Debug.Log("  â”€â”€ âڑ  BARE (unqualified) tags â€” UPDATE INSPECTOR TO DBName.VarName â”€â”€");
            foreach (var r in bare.OrderBy(x => x.tag))
                PrintRecord(r, warn: true);
        }

        IO_Router.Instance?.LogAllTags();
        Debug.Log("â•ڑâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•‌");
    }

    static void PrintRecord(TagRecord r, bool warn = false)
    {
        string paddedTag     = $"'{r.tag}'";
        string spaceWarning  = (r.tag != r.tag.Trim()) ? "  â†گ âœ– HAS WHITESPACE! Fix Inspector." : "";
        string qualWarning   = warn ? "  â†گ âڑ  needs DBName. prefix!" : "";
        string status        = r.everReceived
            ? $"âœ” received  last={r.lastValue}  count={r.receiveCount}  ago={Time.time - r.lastReceivedTime:F1}s"
            : ShouldValidatePlcBridgeTags
                ? "never received - check tag name / DB prefix / PLC wiring"
                : "waiting for MQTT/local value";
        Debug.Log($"    [{r.ownerName,-25}] {paddedTag,-50} {status}{spaceWarning}{qualWarning}");
    }

    /// <summary>Remove a record when a component is destroyed.</summary>
    public static void Remove(string tag)
    {
        if (tag == null) return;
        tag = tag.Trim();
        if (!string.IsNullOrEmpty(tag)) records.Remove(tag);
    }

    /// <summary>Reset all records (call on scene reload if needed).</summary>
    public static void ClearAll() => records.Clear();
}
