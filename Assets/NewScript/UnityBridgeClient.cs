using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// UnityBridgeClient — TCP client that connects to the OPC UA bridge (Program.cs).
/// (FIXED — see change log below)
///
/// ══ FIXES IN THIS VERSION ════════════════════════════════════════════════════
///
///   FIX A — RECEIVE BUFFER OVERFLOW WITH HIGH TAG COUNTS:
///   ──────────────────────────────────────────────────────
///   The original ReadLoop used a fixed 4096-byte buffer.  When the bridge sends
///   an initial broadcast of hundreds of qualified-name tags (now "DBName.VarName"),
///   each JSON frame is longer than the old short names.  A burst of 200 tags at
///   ~50 bytes each = 10 KB minimum.  The 4096-byte buffer caused Read() to return
///   partial frames.  The StringBuilder accumulation handled partial frames
///   correctly in theory, but the thread sleep between reads (5ms in WriteLoop)
///   caused a timing hole where the StringBuilder could be cleared before the
///   rest of the burst arrived.
///   FIX: Buffer increased to 65536 bytes.  The receive loop now only clears the
///   StringBuilder tail (remaining after the last complete newline), never the
///   whole buffer, so partial frames accumulate safely even during large bursts.
///
///   FIX B — WRITE QUEUE UNBOUNDED GROWTH UNDER HIGH TAG COUNT:
///   ────────────────────────────────────────────────────────────
///   When the simulation runs offline and every SetValue() call calls bridge.Send(),
///   the sendQueue can grow without bound if the TCP connection is slow or the
///   bridge is restarting.  A growing queue delays heartbeats, causes stale echoes
///   to arrive out of order, and can eventually exhaust memory.
///   FIX: sendQueue is now capped at MaxSendQueueDepth (default 512).  When the
///   cap is reached, the OLDEST entry is dropped (not the newest), so the PLC
///   always sees the most recent state of every tag.  A warning fires once per
///   overflow event.
///
///   FIX C — RECONNECT LOOP RACES WITH ACTIVE SEND THREADS:
///   ────────────────────────────────────────────────────────
///   The old code called StartCoroutine(ConnectLoop()) from the _RECONNECT_ message
///   path inside the main-thread Update loop, but the read/write threads were still
///   alive and accessing netStream.  The new code calls CleanupConnection() first,
///   which joins both background threads with a 500ms timeout before starting a
///   new connection, preventing the "NetStream accessed after close" exception that
///   caused the bridge to silently stop processing Unity→PLC writes.
///
///   FIX D — DEFERRED RE-BROADCAST ON CONNECT (preserved from prior version):
///   ──────────────────────────────────────────────────────────────────────────
///   After connecting, IO_Router.NotifyBridgeConnected() schedules a 3-frame
///   deferred re-fire of all cached values so late-registrant callbacks receive
///   their initial values even on slow machines with large scenes.
///
///   FIX E — ISCONNECTED PROPERTY PROPERLY REFLECTS THREAD STATE:
///   ─────────────────────────────────────────────────────────────
///   `IsConnected` now returns `running && netStream != null` instead of just
///   `running`.  This ensures IO_Router.dbBridgeOnline is accurate during the
///   brief window after CleanupConnection() sets running=false but before the
///   reconnect loop establishes a new stream.
/// ════════════════════════════════════════════════════════════════════════════
/// </summary>
public class UnityBridgeClient : MonoBehaviour
{
    [Header("Legacy TCP Bridge")]
    [Tooltip("Enable only when using the old Program.cs OPC UA TCP bridge. Keep false when Unity is driven by MQTT.")]
    public bool enableTcpBridge = false;

    [Header("══ Offline / Simulation ═════════════════════════════════════════")]
    [Tooltip("TRUE = no TCP connection attempted. Use for fully offline testing. " +
             "Should match IO_Router.offlineMode.")]
    public bool offlineMode = true;

    [Header("══ Bridge Connection ═══════════════════════════════════════")]
    [Tooltip("IP of the PC running Program.cs (OPC UA bridge)")]
    public string ip   = "127.0.0.1";
    [Tooltip("TCP port — must match port in Program.cs (default 5055)")]
    public int    port = 5055;
    [Tooltip("Seconds between reconnect attempts when disconnected")]
    public float  reconnectDelay = 2f;

    [Header("══ Heartbeat ═══════════════════════════════════════════════")]
    [Tooltip("Send heartbeat toggle every N seconds. 0 = disabled.")]
    public float  heartbeatInterval = 10f;
    [Tooltip("Bool tag in TIA Portal used for heartbeat")]
    public string heartbeatTag = "Bridge.Heartbeat";   // FIX A: example qualified name

    [Header("══ Advanced ════════════════════════════════════════════════")]
    [Tooltip("Max pending writes in send queue before oldest entries are dropped. " +
             "Increase if you have many tags updating at high frequency.")]
    public int maxSendQueueDepth = 512;   // FIX B

    [Header("══ PLC OUTPUT Tags (Unity → PLC) ══════════════════════════")]
    public string out_BridgeOnline  = "Bridge.Online";    // FIX A: example qualified names
    public string out_BridgeOffline = "Bridge.Offline";

    [Header("══ Debug (Read Only) ════════════════════════════════════════")]
    [SerializeField] bool   dbConnected      = false;
    [SerializeField] string dbLastSent       = "—";
    [SerializeField] string dbLastReceived   = "—";
    [SerializeField] int    dbQueueDepth     = 0;
    [SerializeField] int    dbReconnectCount = 0;
    [SerializeField] string dbMode           = "Offline";
    [SerializeField] int    dbQueueDropped   = 0;   // FIX B: dropped frames counter
    [SerializeField] int    dbReceiveBytes   = 0;   // FIX A: running total bytes received

    // ── Public events ─────────────────────────────────────────────────────────
    public static event Action<string> OnMessage;
    public static event Action         OnConnected;
    public static event Action         OnDisconnected;

    // FIX E: IsConnected is true only when TCP stream is actually live
    public bool IsConnected => enableTcpBridge && !offlineMode && running && netStream != null;

    // ── Private ───────────────────────────────────────────────────────────────
    TcpClient     client;
    NetworkStream netStream;
    Thread        readThread;
    Thread        writeThread;

    volatile bool running = false;

    readonly Queue<string>  messageQueue = new Queue<string>();
    readonly object         mqLock       = new object();
    readonly Queue<byte[]>  sendQueue    = new Queue<byte[]>();
    readonly object         sqLock       = new object();

    bool  wasConnected   = false;
    float heartbeatTimer = 0f;
    bool  heartbeatState = false;
    bool  queueDropWarned = false;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (!enableTcpBridge)
        {
            offlineMode = true;
            dbMode = "Disabled (MQTT)";
            running = false;
            return;
        }

        dbMode = offlineMode ? "Offline" : "PLC";

        if (offlineMode)
        {
            Debug.Log("[BRIDGE] Offline mode — no TCP connection attempted.");
            return;
        }

        if (string.IsNullOrEmpty(ip) || port == 0)
        {
            Debug.LogError("[BRIDGE] IP or Port not set — bridge disabled.");
            return;
        }

        if (IO_Router.Instance != null && IO_Router.Instance.offlineMode)
            Debug.LogWarning("[BRIDGE] UnityBridgeClient.offlineMode=false (will connect) but " +
                             "IO_Router.offlineMode=true. Set IO_Router.offlineMode=false too " +
                             "to enable Unity→PLC sends.");

        StartCoroutine(ConnectLoop());
    }

    void OnDestroy()
    {
        running = false;
        CleanupConnection();
    }

    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!enableTcpBridge) return;
        if (offlineMode) return;

        dbConnected  = running;
        dbQueueDepth = sendQueue.Count;
        dbMode       = "PLC";

        // Fire connection events
        bool nowConnected = IsConnected;
        if (nowConnected && !wasConnected)
        {
            wasConnected = true;
            OnConnected?.Invoke();
            SetRouterOutput(out_BridgeOnline,  true);
            SetRouterOutput(out_BridgeOffline, false);
            Debug.Log("[BRIDGE] ✔ Connected — replaying cached tags.");
            ReplayCachedValues();

            // FIX D: schedule deferred re-broadcast
            IO_Router.Instance?.NotifyBridgeConnected();
        }
        else if (!nowConnected && wasConnected)
        {
            wasConnected = false;
            OnDisconnected?.Invoke();
            SetRouterOutput(out_BridgeOnline,  false);
            SetRouterOutput(out_BridgeOffline, true);
            Debug.LogWarning("[BRIDGE] ✖ Disconnected.");
        }

        // Dispatch messages on main thread
        lock (mqLock)
        {
            while (messageQueue.Count > 0)
            {
                string msg = messageQueue.Dequeue();
                dbLastReceived = msg.Length > 80 ? msg[..80] + "…" : msg;
                if (msg == "_RECONNECT_")
                {
                    // FIX C: clean up old threads before reconnecting
                    CleanupConnection();
                    StartCoroutine(ConnectLoop());
                    continue;
                }
                OnMessage?.Invoke(msg);
            }
        }

        // Heartbeat
        if (running && heartbeatInterval > 0f && !string.IsNullOrEmpty(heartbeatTag))
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= heartbeatInterval)
            {
                heartbeatTimer = 0f;
                heartbeatState = !heartbeatState;
                Send(heartbeatTag, heartbeatState);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    IEnumerator ConnectLoop()
    {
        if (!enableTcpBridge) yield break;

        while (!running)
        {
            if (TryConnect()) yield break;
            dbReconnectCount++;
            Debug.LogWarning($"[BRIDGE] Cannot reach {ip}:{port} — retry in {reconnectDelay}s ({dbReconnectCount})");
            yield return new WaitForSeconds(reconnectDelay);
        }
    }

    bool TryConnect()
    {
        try
        {
            client    = new TcpClient(ip, port);
            netStream = client.GetStream();
            running   = true;
            lock (sqLock) { sendQueue.Clear(); }
            queueDropWarned = false;

            readThread  = new Thread(ReadLoop)  { IsBackground = true, Name = "BridgeRead"  };
            writeThread = new Thread(WriteLoop) { IsBackground = true, Name = "BridgeWrite" };
            readThread.Start();
            writeThread.Start();
            Debug.Log($"[BRIDGE] Connected to {ip}:{port}");
            return true;
        }
        catch (Exception e) { Debug.LogWarning($"[BRIDGE] Connect failed: {e.Message}"); return false; }
    }

    // FIX C: cleanly stop background threads before reconnecting
    void CleanupConnection()
    {
        running = false;
        try { client?.Close(); } catch { }
        netStream = null;

        // Give threads a moment to exit naturally
        if (readThread  != null && readThread.IsAlive)  readThread.Join(500);
        if (writeThread != null && writeThread.IsAlive) writeThread.Join(500);
        readThread  = null;
        writeThread = null;
    }

    // ── Background READ ───────────────────────────────────────────────────────
    // FIX A: 65536-byte buffer handles large initial broadcasts
    void ReadLoop()
    {
        byte[]        buf = new byte[65536];
        StringBuilder sb  = new StringBuilder();

        while (running)
        {
            try
            {
                int bytes = netStream!.Read(buf, 0, buf.Length);
                if (bytes == 0) break;

                dbReceiveBytes += bytes;
                sb.Append(Encoding.UTF8.GetString(buf, 0, bytes));
                string all = sb.ToString();
                int nl;
                while ((nl = all.IndexOf('\n')) >= 0)
                {
                    string line = all[..nl].Trim();
                    all = all[(nl + 1)..];
                    if (!string.IsNullOrEmpty(line))
                        lock (mqLock) { messageQueue.Enqueue(line); }
                }

                // FIX A: only keep the incomplete tail — don't clear everything
                sb.Clear();
                sb.Append(all);
            }
            catch { break; }
        }

        running = false;
        lock (mqLock) { messageQueue.Enqueue("_RECONNECT_"); }
    }

    // ── Background WRITE ──────────────────────────────────────────────────────
    // FIX B: drops oldest entries when queue overflows instead of growing unbounded
    void WriteLoop()
    {
        while (running)
        {
            byte[] data = null;
            lock (sqLock)
            {
                if (sendQueue.Count > 0) data = sendQueue.Dequeue();
            }

            if (data != null)
            {
                try { netStream!.Write(data, 0, data.Length); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BRIDGE] Write failed ({e.Message}) — reconnecting.");
                    running = false;
                    lock (mqLock) { messageQueue.Enqueue("_RECONNECT_"); }
                    break;
                }
            }
            else
            {
                Thread.Sleep(5);
            }
        }
    }

    // ── Send ──────────────────────────────────────────────────────────────────
    public void Send(string tag, bool value)
    {
        if (!enableTcpBridge)
        {
            return;
        }

        if (!running)
        {
            if (!offlineMode)
                Debug.LogWarning($"[BRIDGE] Not connected — dropped: {tag}={value}. " +
                                 $"Make sure Program.cs is running at {ip}:{port}.");
            return;
        }

        try
        {
            string json = $"{{\"Tag\":\"{tag}\",\"Value\":{value.ToString().ToLower()}}}";
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");

            lock (sqLock)
            {
                // FIX B: drop oldest entry when queue is full
                if (sendQueue.Count >= maxSendQueueDepth)
                {
                    sendQueue.Dequeue();
                    dbQueueDropped++;
                    if (!queueDropWarned)
                    {
                        queueDropWarned = true;
                        Debug.LogWarning(
                            $"[BRIDGE] Send queue full ({maxSendQueueDepth} entries) — " +
                            "dropping oldest entries to prevent backlog. " +
                            "Increase maxSendQueueDepth or reduce tag update frequency.");
                    }
                }
                sendQueue.Enqueue(data);
            }

            dbLastSent = json.Length > 80 ? json[..80] + "…" : json;
        }
        catch (Exception e) { Debug.LogWarning($"[BRIDGE] Send enqueue failed: {e.Message}"); }
    }

    // ── Replay on reconnect ───────────────────────────────────────────────────
    void ReplayCachedValues()
    {
        if (IO_Router.Instance == null) return;
        List<string> tags = IO_Router.Instance.GetAllKnownTags();
        int count = 0;
        foreach (string tag in tags)
        {
            Send(tag, IO_Router.Instance.GetValue(tag));
            count++;
        }
        Debug.Log($"[BRIDGE] Replayed {count} cached values to bridge.");
    }

    void SetRouterOutput(string tag, bool v)
    { if (!string.IsNullOrEmpty(tag)) IO_Router.Instance?.SetValue(tag, v); }
}
