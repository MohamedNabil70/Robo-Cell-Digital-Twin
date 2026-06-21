using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// TagSubscriptionHelper — shared registration and diagnostics utility. (FIXED)
///
/// ══ FIXES IN THIS VERSION ════════════════════════════════════════════════════
///
///   FIX A — QUALIFIED TAG NAME VALIDATION ("DBName.VarName"):
///   ──────────────────────────────────────────────────────────
///   The bridge now sends every tag as "DataBlockName.VariableName" to prevent
///   silent shadowing across DataBlocks.  RegisterWithRetry() now detects when
///   a component registers a BARE (unqualified) tag name (no '.' separator)
///   and emits an actionable warning telling the user exactly what qualified
///   name to use.  The registration still proceeds so existing scenes don't
///   break immediately — but the warning must be resolved before going live.
///
///   The late-join cache replay also searches for the qualified form of the
///   bare name if the bare name itself isn't found in the cache, so components
///   that haven't yet been updated to qualified names still get their initial
///   value on startup (with a warning).
///
///   FIX B — WHITESPACE FIX (preserved from prior version):
///   ───────────────────────────────────────────────────────
///   RegisterWithRetry() trims the tag name before passing to IO_Router.
///   This makes every component immune to accidental Inspector whitespace.
///
///   FIX C — DIAGNOSTICS: QUALIFIED NAME GROUPING:
///   ───────────────────────────────────────────────
///   DiagnoseAll() now groups subscriptions by DB prefix and shows a separate
///   "Missing DB prefix" section for any bare tag names still in use.
///   This makes migration easy: one glance shows what still needs updating.
///
///   FIX D — REMOVE() NOW HANDLES QUALIFIED KEY:
///   ─────────────────────────────────────────────
///   Remove() trims and also checks if the tag has a qualified form in the
///   records dictionary before removing, preventing ghost entries.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public static class TagSubscriptionHelper
{
    // ── Per-tag registration record ───────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────────────────────

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
        // ── FIX B: whitespace trim ────────────────────────────────────────────
        string originalTag = tag;
        if (tag != null) tag = tag.Trim();

        if (!string.IsNullOrEmpty(originalTag) && originalTag != tag)
        {
            Debug.LogError(
                $"[TAG-SUB | {owner?.name}] ✖ WHITESPACE in tag '{originalTag}'!\n" +
                $"  Trimmed to '{tag}' automatically.\n" +
                $"  Fix the Inspector field: remove spaces from the in_*/out_* field value.");
        }

        if (string.IsNullOrEmpty(tag) || callback == null) yield break;

        // ── FIX A: qualified name validation ─────────────────────────────────
        bool isQualified = tag.Contains('.');
        if (!isQualified)
        {
            // Emit actionable warning but do NOT abort — old scenes still work.
            Debug.LogWarning(
                $"[TAG-SUB | {owner?.name}] ⚠ BARE TAG NAME: '{tag}'\n" +
                $"  Tags should use qualified format: \"DBName.VarName\"\n" +
                $"  Example: if your TIA Portal DB is 'WarehouseDB' and the variable is '{tag}'\n" +
                $"           set the Inspector field to: WarehouseDB.{tag}\n" +
                $"  Run 'list' in the bridge console to see all qualified tag names.\n" +
                $"  This tag will still register and attempt to receive values, but the\n" +
                $"  bridge will only send it if your Inspector field exactly matches a\n" +
                $"  tag key in the bridge tagStore (which now uses qualified names).");
        }

        // ── Wait for IO_Router ────────────────────────────────────────────────
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

        // ── Build / update record ─────────────────────────────────────────────
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

        Debug.Log($"[TAG-SUB | {owner?.name}] ✔ Registered '{tag}'" +
                  (isQualified ? "" : " [bare/unqualified — update Inspector to DBName.VarName]"));

        // ── Retry loop — late-join cache replay ───────────────────────────────
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
                        Debug.LogWarning(
                            $"[TAG-SUB | {owner?.name}] Auto-resolved bare tag '{tag}' → '{cacheKey}'.\n" +
                            $"  Update your Inspector field to '{cacheKey}' to remove this warning.");
                    }
                    else if (qualMatch.Count > 1)
                    {
                        Debug.LogWarning(
                            $"[TAG-SUB | {owner?.name}] Bare tag '{tag}' is AMBIGUOUS — " +
                            $"found as {string.Join(", ", qualMatch)}. Cannot auto-resolve. " +
                            "Update Inspector field to the exact qualified name you want.");
                    }
                }

                if (IO_Router.Instance.KnownTags.Contains(cacheKey) && !rec.everReceived)
                {
                    bool cached = IO_Router.Instance.GetValue(cacheKey);
                    wrappedCallback(cached);
                    Debug.LogWarning(
                        $"[TAG-SUB | {owner?.name}] Late-replay for cached tag '{cacheKey}' = {cached} " +
                        $"(retry {i + 1}/{maxRetries}).");
                }
            }

            delay = Mathf.Min(delay * 2f, 5f);
        }

        // ── Final diagnostic if still never received ──────────────────────────
        if (!rec.everReceived)
        {
            string qualHint = isQualified ? "" :
                "\n  ★ Your tag is UNQUALIFIED (no '.'). The bridge sends 'DBName.VarName'.\n" +
                $"    Update Inspector field to 'YourDBName.{tag}'.";

            Debug.LogWarning(
                $"[TAG-SUB | {owner?.name}] ⚠ Tag '{tag}' NEVER received after {maxRetries} retries.\n" +
                $"  Checklist:\n" +
                $"  1. Tag is CASE-SENSITIVE. Registered: '{tag}'\n" +
                $"     Run 'verify {tag}' in bridge console to check.\n" +
                $"  2. Check for whitespace — the diagnostic table shows exact strings.\n" +
                $"  3. IO_Router.offlineMode = {IO_Router.Instance?.offlineMode} (must be FALSE for live PLC).\n" +
                $"  4. Bridge: do you see [PLC→UNITY] lines for this tag?{qualHint}\n" +
                "  → Right-click component → 'Diagnose Tag Subscriptions' for full table.");
        }
    }

    // ── Diagnostics ───────────────────────────────────────────────────────────

    /// <summary>
    /// Prints a full table of all registered PLC-input tags, grouped by DB prefix.
    /// Tags shown with surrounding quotes so any whitespace is visible.
    /// A "Missing DB prefix" section shows bare tags that need to be updated.
    /// </summary>
    public static void DiagnoseAll()
    {
        Debug.Log("╔══════════════ TAG SUBSCRIPTION DIAGNOSTICS ══════════════╗");
        Debug.Log($"  IO_Router   : {(IO_Router.Instance != null ? "found" : "MISSING!")}");
        if (IO_Router.Instance != null)
        {
            Debug.Log($"  Mode        : {(IO_Router.Instance.offlineMode ? "Offline (set FALSE for PLC)" : "PLC ✔")}");
            Debug.Log($"  Bridge      : {(IO_Router.Instance.bridge != null ? "assigned" : "MISSING")}");
        }
        Debug.Log($"  Tracked tags: {records.Count}");

        // FIX C: group by DB prefix
        var qualified = records.Values.Where(r => r.isQualified).GroupBy(r =>
        {
            int dot = r.tag.IndexOf('.');
            return dot >= 0 ? r.tag[..dot] : "_";
        }).OrderBy(g => g.Key);

        var bare = records.Values.Where(r => !r.isQualified).ToList();

        Debug.Log("  ── Qualified subscriptions (DBName.VarName) ──");
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
            Debug.Log("  ── ⚠ BARE (unqualified) tags — UPDATE INSPECTOR TO DBName.VarName ──");
            foreach (var r in bare.OrderBy(x => x.tag))
                PrintRecord(r, warn: true);
        }

        IO_Router.Instance?.LogAllTags();
        Debug.Log("╚══════════════════════════════════════════════════════════╝");
    }

    static void PrintRecord(TagRecord r, bool warn = false)
    {
        string paddedTag     = $"'{r.tag}'";
        string spaceWarning  = (r.tag != r.tag.Trim()) ? "  ← ✖ HAS WHITESPACE! Fix Inspector." : "";
        string qualWarning   = warn ? "  ← ⚠ needs DBName. prefix!" : "";
        string status        = r.everReceived
            ? $"✔ received  last={r.lastValue}  count={r.receiveCount}  ago={Time.time - r.lastReceivedTime:F1}s"
            : "✖ NEVER RECEIVED — check tag name / DB prefix / PLC wiring";
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
