using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// Minimal, dependency-free OSC (Open Sound Control) sender over UDP.
/// Builds OSC 1.0 wire-format packets by hand so no third-party package is
/// required (IL2CPP / visionOS friendly). Supports int, float and string args.
/// </summary>
public class OscSender : MonoBehaviour
{
    [Header("Target")]
    public string remoteIp = "127.0.0.1";
    public int port = 9000;

    private UdpClient udpClient;

    void OnEnable()
    {
        OpenSocket();
    }

    void OnDisable()
    {
        CloseSocket();
    }

    private void OpenSocket()
    {
        try
        {
            udpClient = new UdpClient();
            udpClient.Connect(remoteIp, port);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OscSender] Failed to open UDP socket to {remoteIp}:{port} — {e.Message}");
            CloseSocket();
        }
    }

    private void CloseSocket()
    {
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
    }

    /// <summary>
    /// Send an OSC message. Supported argument types: int, float, string.
    /// </summary>
    public void Send(string address, params object[] args)
    {
        if (string.IsNullOrEmpty(address))
        {
            Debug.LogWarning("[OscSender] Send called with empty address.");
            return;
        }

        if (udpClient == null)
        {
            // Socket may have failed to open (e.g. ip/port changed at runtime); try once more.
            OpenSocket();
            if (udpClient == null) return;
        }

        try
        {
            byte[] packet = BuildPacket(address, args);
            udpClient.Send(packet, packet.Length);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OscSender] Send to {remoteIp}:{port} failed — {e.Message}");
        }
    }

    private static byte[] BuildPacket(string address, object[] args)
    {
        var buffer = new List<byte>();

        // 1) Address pattern (null-terminated, padded to 4 bytes).
        WriteString(buffer, address);

        // 2) Type tag string: ',' + one tag char per arg.
        var typeTag = new StringBuilder(",");
        if (args != null)
        {
            foreach (object arg in args)
            {
                switch (arg)
                {
                    case int _:    typeTag.Append('i'); break;
                    case float _:  typeTag.Append('f'); break;
                    case string _: typeTag.Append('s'); break;
                    default:
                        Debug.LogWarning($"[OscSender] Unsupported OSC arg type: {arg?.GetType().Name ?? "null"} — skipped.");
                        break;
                }
            }
        }
        WriteString(buffer, typeTag.ToString());

        // 3) Arguments (big-endian for numbers, padded strings).
        if (args != null)
        {
            foreach (object arg in args)
            {
                switch (arg)
                {
                    case int i:    WriteInt32BE(buffer, i); break;
                    case float f:  WriteFloat32BE(buffer, f); break;
                    case string s: WriteString(buffer, s); break;
                }
            }
        }

        return buffer.ToArray();
    }

    private static void WriteString(List<byte> buffer, string value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        buffer.AddRange(bytes);
        buffer.Add(0); // null terminator
        // Pad with zeros to a multiple of 4 bytes (terminator counts toward length).
        int pad = 4 - ((bytes.Length + 1) % 4);
        if (pad < 4)
        {
            for (int i = 0; i < pad; i++) buffer.Add(0);
        }
    }

    private static void WriteInt32BE(List<byte> buffer, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        buffer.AddRange(bytes);
    }

    private static void WriteFloat32BE(List<byte> buffer, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        buffer.AddRange(bytes);
    }
}
