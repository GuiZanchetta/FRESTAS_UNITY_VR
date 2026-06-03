// OscSender.cs
// OSC send-only component for FRESTAS.
// Encoding and UdpClient pattern ported directly from syncmuseosc (the working
// reference in this project) — same no-arg UdpClient constructor, same
// Send(packet, length, host, port) overload, same InsertString / PadSize logic.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class OscSender : MonoBehaviour
{
    [Header("OSC Target")]
    public string targetIP   = "10.10.143.78";
    public int    targetPort = 9000;

    private UdpClient        _sender;
    private Queue<byte[]>    _queue = new Queue<byte[]>();
    private bool             _ready;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Initialize()
    {
        if (_ready) return;
        _sender = new UdpClient();          // same as syncmuseosc: no-arg, no Connect()
        _ready  = true;
        Debug.Log($"[OscSender] ready → {targetIP}:{targetPort}");
    }

    /// <summary>Enqueue an OSC float message. Call from the main thread.</summary>
    public void Send(string address, float value)
    {
        if (!_ready) { Debug.LogWarning("[OscSender] not initialized"); return; }
        byte[] packet = new byte[1000];
        int length = BuildFloatMessage(address, value, packet);
        byte[] trimmed = new byte[length];
        Array.Copy(packet, trimmed, length);
        _queue.Enqueue(trimmed);
    }

    public void SendPlay() => Send("/frestas/play", 1f);
    public void SendStop() => Send("/frestas/stop", 0f);

    public void Dispose()
    {
        if (!_ready) return;
        _ready = false;
        _queue.Clear();
        _sender?.Close();
        _sender = null;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()     => Initialize();
    void OnDestroy() => Dispose();

    void Update()
    {
        while (_queue.Count > 0)
        {
            byte[] pkt = _queue.Dequeue();
            try
            {
                // Exact same send call as syncmuseosc SendPacket()
                _sender.Send(pkt, pkt.Length, targetIP, targetPort);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OscSender] {e.Message}");
            }
        }
    }

    // ── OSC encoding — ported from syncmuseosc ────────────────────────────────

    // Builds a single-float OSC message into packet[], returns byte count.
    static int BuildFloatMessage(string address, float value, byte[] packet)
    {
        int index = InsertString(address, packet, 0, packet.Length);

        // Reserve space for the type tag (",f") before writing the argument.
        int tagIndex = index;
        index += PadSize(2 + 1);   // "," + "f" + null terminator, padded to 4

        // Float argument — big-endian (syncmuseosc writes little-endian via
        // BinaryWriter then reverses bytes; we use the same result directly).
        byte[] b = new byte[4];
        using (var ms = new MemoryStream(b))
        using (var bw = new BinaryWriter(ms))
            bw.Write(value);                // BinaryWriter is little-endian
        packet[index++] = b[3];            // MSB first → big-endian
        packet[index++] = b[2];
        packet[index++] = b[1];
        packet[index++] = b[0];

        // Write type tag into the reserved slot
        InsertString(",f", packet, tagIndex, packet.Length);

        return index;
    }

    // From syncmuseosc — writes a null-terminated, 4-byte-padded ASCII string.
    static int InsertString(string s, byte[] packet, int start, int length)
    {
        int index = start;
        foreach (char c in s)
        {
            packet[index++] = (byte)c;
            if (index == length) return index;
        }
        packet[index++] = 0;
        int pad = (s.Length + 1) % 4;
        if (pad != 0) { pad = 4 - pad; while (pad-- > 0) packet[index++] = 0; }
        return index;
    }

    // From syncmuseosc — rounds up to the next 4-byte boundary.
    static int PadSize(int rawSize)
    {
        int pad = rawSize % 4;
        return pad == 0 ? rawSize : rawSize + (4 - pad);
    }
}
