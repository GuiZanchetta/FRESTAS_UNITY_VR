// OscSender.cs — visionOS / IL2CPP safe, zero thread-pool usage
//
// Why sem_open persists after the previous fix:
//   Task.Run() schedules work on the .NET thread pool. The first time any
//   thread-pool thread is created, Mono/CoreCLR initialises its internal
//   worker semaphore via sem_open() — which visionOS blocks unconditionally.
//   UdpClient.SendAsync() hits the same path: its I/O completions are posted
//   back through the thread pool. Moving the UdpClient constructor to the
//   main thread does not help because the sem_open call happens later, on
//   the first Task.Run / first async completion.
//
// Fix: remove every source of thread-pool usage.
//   • No Task.Run, no async/await, no coroutine that yields to a thread.
//   • Socket is opened with Blocking = false (non-blocking POSIX sendto).
//   • The queue is drained in Update() — always on the main thread.
//   • For a 24-byte OSC packet on loopback or LAN, a non-blocking sendto()
//     returns in < 1 µs; it never stalls a frame.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class OscSender : MonoBehaviour
{
    [Header("OSC Target")]
    public string targetIP   = "127.0.0.1";
    public int    targetPort = 9000;

    // Plain Queue — only ever touched on the main thread, no lock needed.
    private readonly Queue<byte[]> _queue = new();
    private Socket   _socket;
    private EndPoint _remote;
    private bool     _ready;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Initialize()
    {
        if (_ready) return;

        // Raw Socket instead of UdpClient — no managed wrapper, no lazy
        // thread-pool initialization hidden inside.
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            Blocking = false   // sendto() returns immediately — never stalls
        };
        _remote = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);

        _ready = true;
        Debug.Log($"[OscSender] ready → {targetIP}:{targetPort}");
    }

    /// <summary>
    /// Enqueue an OSC message with one float argument.
    /// Call only from the main thread (matches all visionOS input callbacks).
    /// </summary>
    public void Send(string address, float value)
    {
        if (!_ready) { Debug.LogWarning("[OscSender] not initialized"); return; }
        _queue.Enqueue(BuildOscFloat(address, value));
    }

    // Convenience wrappers for PlayStopTimeline
    public void SendPlay() => Send("/frestas/play", 1f);
    public void SendStop() => Send("/frestas/stop", 0f);

    public void Dispose()
    {
        if (!_ready) return;
        _ready = false;
        _queue.Clear();
        try { _socket?.Close(); } catch { /* ignore */ }
        _socket = null;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()     => Initialize();
    void OnDestroy() => Dispose();

    // Drain the queue every frame — all on the main thread, zero thread-pool.
    void Update()
    {
        while (_queue.Count > 0)
        {
            byte[] pkt = _queue.Dequeue();
            try
            {
                _socket.SendTo(pkt, SocketFlags.None, _remote);
            }
            catch (SocketException se) when (se.SocketErrorCode == SocketError.WouldBlock)
            {
                // Kernel send buffer momentarily full (extremely rare for UDP).
                // Re-enqueue at the head next frame rather than dropping.
                // Since Queue has no AddFirst, we rebuild — acceptable because
                // this path is never taken in normal operation.
                RebuildWithHead(pkt);
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OscSender] send error: {e.Message}");
            }
        }
    }

    // Re-insert a packet at the front of the queue (only on WouldBlock).
    void RebuildWithHead(byte[] head)
    {
        var tmp = new Queue<byte[]>(_queue.Count + 1);
        tmp.Enqueue(head);
        while (_queue.Count > 0) tmp.Enqueue(_queue.Dequeue());
        _queue.Clear();
        foreach (var p in tmp) _queue.Enqueue(p);
    }

    // ── OSC encoding ──────────────────────────────────────────────────────────

    // Layout: <address>\0[pad] <,f>\0[pad] <float32 big-endian>
    static byte[] BuildOscFloat(string address, float value)
    {
        byte[] addr = OscString(address);
        byte[] tags = OscString(",f");
        byte[] arg  = FloatToOscBytes(value);

        byte[] pkt = new byte[addr.Length + tags.Length + arg.Length];
        Buffer.BlockCopy(addr, 0, pkt, 0,                         addr.Length);
        Buffer.BlockCopy(tags, 0, pkt, addr.Length,               tags.Length);
        Buffer.BlockCopy(arg,  0, pkt, addr.Length + tags.Length, arg.Length);
        return pkt;
    }

    // OSC string: ASCII, null-terminated, zero-padded to 4-byte boundary
    static byte[] OscString(string s)
    {
        byte[] raw = Encoding.ASCII.GetBytes(s + "\0");
        int    rem = raw.Length % 4;
        if (rem != 0) Array.Resize(ref raw, raw.Length + (4 - rem));
        return raw;
    }

    // OSC float32: big-endian IEEE 754
    static byte[] FloatToOscBytes(float v)
    {
        byte[] b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }
}
