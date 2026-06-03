// OscSender.cs — visionOS-safe UDP/OSC sender for Unity 6 / IL2CPP
//
// Root cause of sem_open failed on visionOS:
//   System.Threading.Semaphore (the heavyweight class) calls sem_open()
//   internally. UdpClient's legacy synchronous path can trigger this via
//   the socket layer's internal locking. visionOS sandbox blocks named
//   POSIX semaphores entirely.
//
// Fix applied here:
//   • UdpClient constructed + connected on the main thread in Initialize(),
//     before any background work begins — avoids the runtime's lazy socket
//     init being triggered from a thread-pool context.
//   • All synchronization uses SemaphoreSlim (managed, no sem_open) and
//     async/await (state-machine continuations, not blocked OS threads).
//   • Send path is fully async: UdpClient.SendAsync, never UdpClient.Send.

using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class OscSender : MonoBehaviour
{
    [Header("OSC Target")]
    public string targetIP   = "127.0.0.1";
    public int    targetPort = 9000;

    private UdpClient               _udp;
    private ConcurrentQueue<byte[]> _queue;
    private SemaphoreSlim           _signal;   // managed — no sem_open
    private CancellationTokenSource _cts;
    private Task                    _sendLoop;
    private bool                    _ready;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Construct and connect the socket on the calling (main) thread, then
    /// start the async send loop. Safe to call from Awake or Start.
    /// </summary>
    public void Initialize()
    {
        if (_ready) return;

        // Socket work done here, on the main thread, before any Task or
        // ThreadPool code runs. This is the key visionOS guard.
        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.SendBufferSize    = 65536;
        _udp.Client.SendTimeout       = 0;      // async path ignores this, but be explicit
        _udp.Connect(targetIP, targetPort);

        _queue  = new ConcurrentQueue<byte[]>();
        _signal = new SemaphoreSlim(0);
        _cts    = new CancellationTokenSource();

        // Task.Run is fine here: the UdpClient object is fully initialized
        // before the loop touches it, so the runtime never needs to call
        // sem_open for lazy socket setup.
        _sendLoop = Task.Run(() => SendLoopAsync(_cts.Token));

        _ready = true;
        Debug.Log($"[OscSender] ready → {targetIP}:{targetPort}");
    }

    /// <summary>
    /// Enqueue an OSC message with a single float argument.
    /// Thread-safe; returns immediately — never blocks.
    /// </summary>
    public void Send(string address, float value)
    {
        if (!_ready) { Debug.LogWarning("[OscSender] not initialized"); return; }
        _queue.Enqueue(BuildOscFloat(address, value));
        _signal.Release();
    }

    // Convenience wrappers used by PlayStopTimeline
    public void SendPlay() => Send("/frestas/play", 1f);
    public void SendStop() => Send("/frestas/stop", 0f);

    public void Dispose()
    {
        if (!_ready) return;
        _ready = false;

        // Cancel first so WaitAsync throws OperationCanceledException.
        _cts.Cancel();
        // Close socket — unblocks any in-flight SendAsync with a SocketException.
        _udp.Close();
        // Dispose signal — any remaining WaitAsync throws ObjectDisposedException.
        _signal.Dispose();
        _cts.Dispose();
        // _sendLoop exits on its own; we don't await here (OnDestroy is sync).
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()     => Initialize();
    void OnDestroy() => Dispose();

    // ── Async send loop ───────────────────────────────────────────────────────

    private async Task SendLoopAsync(CancellationToken ct)
    {
        while (true)
        {
            // SemaphoreSlim.WaitAsync — managed wait, no POSIX named semaphore.
            try   { await _signal.WaitAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException)    { break; }

            if (!_queue.TryDequeue(out byte[] packet)) continue;

            try
            {
                // SendAsync never blocks the calling thread.
                await _udp.SendAsync(packet, packet.Length);
            }
            catch (ObjectDisposedException)    { break; }          // socket closed in Dispose
            catch (SocketException se) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning($"[OscSender] socket error {se.SocketErrorCode}: {se.Message}");
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                Debug.LogWarning($"[OscSender] send error: {e.Message}");
            }
            catch { break; }
        }
    }

    // ── OSC encoding ──────────────────────────────────────────────────────────

    // Packet layout: <address>\0[pad] <,f>\0[pad] <float32 big-endian>
    static byte[] BuildOscFloat(string address, float value)
    {
        byte[] addr = OscString(address);
        byte[] tags = OscString(",f");
        byte[] arg  = FloatToOscBytes(value);

        byte[] pkt  = new byte[addr.Length + tags.Length + arg.Length];
        Buffer.BlockCopy(addr, 0, pkt, 0,                         addr.Length);
        Buffer.BlockCopy(tags, 0, pkt, addr.Length,               tags.Length);
        Buffer.BlockCopy(arg,  0, pkt, addr.Length + tags.Length, arg.Length);
        return pkt;
    }

    // OSC string: ASCII, null-terminated, zero-padded to next 4-byte boundary
    static byte[] OscString(string s)
    {
        byte[] raw = Encoding.ASCII.GetBytes(s + "\0");
        int    rem = raw.Length % 4;
        if (rem != 0) Array.Resize(ref raw, raw.Length + (4 - rem));
        return raw;
    }

    // OSC float32: big-endian IEEE 754 (always 4 bytes, already aligned)
    static byte[] FloatToOscBytes(float v)
    {
        byte[] b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }
}
