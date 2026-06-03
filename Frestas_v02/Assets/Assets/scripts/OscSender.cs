// OscSender.cs — FRESTAS telematic OSC sender
//
// visionOS sandboxes System.Net.Sockets: UdpClient internally calls
// nw_socket_copy_info / getsockopt(TCP_INFO) via Apple Network.framework,
// which fails with EOPNOTSUPP (102) on UDP sockets.  Every managed socket
// path hits this — UdpClient, Socket, async or sync.
//
// Fix: on UNITY_VISIONOS use P/Invoke to raw POSIX BSD socket() + sendto().
// DllImport("__Internal") resolves to statically-linked system symbols already
// in the process — no separate .dylib needed, pure C#.  BSD sockets bypass
// Network.framework entirely: no nw_socket, no sem_open, no TCP_INFO.
// The #else branch keeps managed UdpClient working in the Unity Editor.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
#if UNITY_VISIONOS
using System.Runtime.InteropServices;
#endif

[DisallowMultipleComponent]
public class OscSender : MonoBehaviour
{
    [Header("OSC Target")]
    public string targetIP   = "10.10.143.78";
    public int    targetPort = 9000;

    private UdpClient        _sender;
    private Queue<byte[]>    _queue = new Queue<byte[]>();
    private bool             _ready;

    // ── Platform layer ────────────────────────────────────────────────────────

#if UNITY_VISIONOS

    // BSD sockaddr_in for Apple/visionOS (includes sin_len — BSD extension)
    [StructLayout(LayoutKind.Sequential)]
    struct sockaddr_in
    {
        public byte   sin_len;    // sizeof(sockaddr_in) = 16, required on BSD
        public byte   sin_family; // AF_INET = 2
        public ushort sin_port;   // network byte order (big-endian)
        public uint   sin_addr;   // network byte order (big-endian)
        public ulong  sin_zero;   // 8-byte padding
    }

    const int AF_INET     = 2;
    const int SOCK_DGRAM  = 2;
    const int IPPROTO_UDP = 17;

    [DllImport("__Internal")] static extern int    socket(int domain, int type, int protocol);
    [DllImport("__Internal")] static extern IntPtr sendto(int sockfd, byte[] buf, IntPtr len,
                                                          int flags, ref sockaddr_in to, int addrlen);
    [DllImport("__Internal")] static extern int    close(int fd);

    int         _fd   = -1;
    sockaddr_in _addr;

    void PlatformInit()
    {
        _fd = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
        if (_fd < 0)
        {
            Debug.LogError("[OscSender] socket() failed — verify Network Client entitlement in Xcode");
            return;
        }
        _addr = new sockaddr_in
        {
            sin_len    = 16,
            sin_family = AF_INET,
            sin_port   = Htons((ushort)targetPort),
            sin_addr   = ParseIPv4(targetIP),
        };
        _ready = true;
        Debug.Log($"[OscSender] BSD socket fd={_fd} → {targetIP}:{targetPort}");
    }

    void PlatformSend(byte[] pkt)
    {
        var addr = _addr;   // copy: sendto needs ref but must not mutate the stored value
        IntPtr n = sendto(_fd, pkt, (IntPtr)pkt.Length, 0, ref addr, 16);
        if (n.ToInt64() < 0) Debug.LogWarning($"[OscSender] sendto() returned {n}");
    }

    void PlatformDispose()
    {
        if (_fd >= 0) { close(_fd); _fd = -1; }
    }

    // Big-endian port for sockaddr_in on little-endian ARM
    static ushort Htons(ushort v) => (ushort)((v >> 8) | (v << 8));

    // Dotted-decimal → network-order uint32 (LSB = first octet on LE ARM)
    static uint ParseIPv4(string ip)
    {
        string[] o = ip.Split('.');
        return byte.Parse(o[0])
             | ((uint)byte.Parse(o[1]) << 8)
             | ((uint)byte.Parse(o[2]) << 16)
             | ((uint)byte.Parse(o[3]) << 24);
    }

#else

    // ── Editor / non-visionOS: managed UdpClient ──────────────────────────────

    UdpClient  _sender;
    IPEndPoint _endpoint;

    void PlatformInit()
    {
        _sender   = new UdpClient();
        _endpoint = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
        _ready    = true;
        Debug.Log($"[OscSender] UdpClient → {targetIP}:{targetPort}");
    }

    void PlatformSend(byte[] pkt)
    {
        try   { _sender.Send(pkt, pkt.Length, _endpoint); }
        catch (Exception e) { Debug.LogWarning($"[OscSender] {e.Message}"); }
    }

    void PlatformDispose()
    {
        _sender?.Close();
        _sender = null;
    }

#endif

    // ── Shared: public API + Unity lifecycle ──────────────────────────────────

    public void Initialize()
    {
        if (_ready) return;
        _sender = new UdpClient();          // same as syncmuseosc: no-arg, no Connect()
        _ready  = true;
        Debug.Log($"[OscSender] ready → {targetIP}:{targetPort}");
    }

    public void Send(string address, float value)
    {
        if (!_ready) { Debug.LogWarning("[OscSender] not initialized"); return; }
        byte[] packet = new byte[1000];
        int    length = BuildFloatMessage(address, value, packet);
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
        PlatformDispose();
    }

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

    // ── OSC encoding (from syncmuseosc) ───────────────────────────────────────

    static int BuildFloatMessage(string address, float value, byte[] packet)
    {
        int index    = InsertString(address, packet, 0, packet.Length);
        int tagIndex = index;
        index += PadSize(3);    // reserve space for ",f\0" padded to 4 bytes

        // Float big-endian — same byte-reversal as syncmuseosc BinaryWriter path
        byte[] b = new byte[4];
        using (var ms = new MemoryStream(b))
        using (var bw = new BinaryWriter(ms))
            bw.Write(value);
        packet[index++] = b[3];
        packet[index++] = b[2];
        packet[index++] = b[1];
        packet[index++] = b[0];

        InsertString(",f", packet, tagIndex, packet.Length);
        return index;
    }

    static int InsertString(string s, byte[] packet, int start, int length)
    {
        int index = start;
        foreach (char c in s) { packet[index++] = (byte)c; if (index == length) return index; }
        packet[index++] = 0;
        int pad = (s.Length + 1) % 4;
        if (pad != 0) { pad = 4 - pad; while (pad-- > 0) packet[index++] = 0; }
        return index;
    }

    static int PadSize(int rawSize)
    {
        int pad = rawSize % 4;
        return pad == 0 ? rawSize : rawSize + (4 - pad);
    }
}
