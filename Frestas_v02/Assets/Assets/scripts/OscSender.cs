using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

// Sends OSC messages over UDP. Attach to a shared GameObject and reference
// it from PlayStopTimeline to trigger Reaper transport remotely.
public class OscSender : MonoBehaviour
{
    [Header("OSC Target (Reaper machine)")]
    public string targetIP   = "127.0.0.1";
    public int    targetPort = 9000;

    private UdpClient _udp;
    private IPEndPoint _remote;

    void Start()
    {
        _udp    = new UdpClient();
        _remote = new IPEndPoint(IPAddress.Parse(targetIP), targetPort);
        Debug.Log($"[OscSender] → {targetIP}:{targetPort}");
    }

    public void SendPlay()  => Send("/frestas/play");
    public void SendStop()  => Send("/frestas/stop");

    void Send(string address)
    {
        try
        {
            byte[] msg = BuildOsc(address);
            _udp.Send(msg, msg.Length, _remote);
            Debug.Log($"[OscSender] sent {address}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OscSender] send error: {e.Message}");
        }
    }

    // Minimal OSC bundle: address string + type tag "," — no arguments
    static byte[] BuildOsc(string address)
    {
        return Concat(OscString(address), OscString(","));
    }

    static byte[] OscString(string s)
    {
        byte[] raw = Encoding.ASCII.GetBytes(s + "\0");
        int pad = (4 - raw.Length % 4) % 4;
        Array.Resize(ref raw, raw.Length + pad);
        return raw;
    }

    static byte[] Concat(byte[] a, byte[] b)
    {
        byte[] out_ = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, out_, 0, a.Length);
        Buffer.BlockCopy(b, 0, out_, a.Length, b.Length);
        return out_;
    }

    void OnDestroy() => _udp?.Close();
}
