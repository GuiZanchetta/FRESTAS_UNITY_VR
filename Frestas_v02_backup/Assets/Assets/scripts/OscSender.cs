using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

/// <summary>
/// Lightweight, dependency-free OSC 1.0 sender over UDP.
/// Attach to a single GameObject in the scene; reachable from anywhere via OscSender.Instance.
/// </summary>
public class OscSender : MonoBehaviour
{
    public static OscSender Instance;

    [Header("Receiver")]
    public string targetIp = "192.168.0.100"; // set to your LAN receiver (TouchDesigner / Max / VJ tool)
    public int port = 8000;

    [Header("Debug")]
    public bool logToConsole = false;

    private UdpClient client;
    private IPEndPoint endPoint;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        OpenSocket();
    }

    void OpenSocket()
    {
        try
        {
            endPoint = new IPEndPoint(IPAddress.Parse(targetIp), port);
            client = new UdpClient();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OscSender] Failed to open socket to {targetIp}:{port} - {e.Message}");
        }
    }

    /// <summary>
    /// Encode and send an OSC message. Supported arg types: int, float, string.
    /// </summary>
    public void Send(string address, params object[] args)
    {
        if (client == null || endPoint == null)
            return;

        try
        {
            byte[] packet = BuildMessage(address, args);
            client.Send(packet, packet.Length, endPoint);

            if (logToConsole)
                Debug.Log($"[OscSender] {address} {string.Join(" ", args)} -> {targetIp}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[OscSender] Send failed for {address}: {e.Message}");
        }
    }

    static byte[] BuildMessage(string address, object[] args)
    {
        var bytes = new List<byte>();

        // 1. Address pattern, null-terminated, padded to 4 bytes.
        AppendString(bytes, address);

        // 2. Type-tag string starting with ','.
        var tags = new StringBuilder(",");
        foreach (var arg in args)
        {
            if (arg is int) tags.Append('i');
            else if (arg is float) tags.Append('f');
            else if (arg is string) tags.Append('s');
            else throw new ArgumentException($"Unsupported OSC arg type: {arg?.GetType()}");
        }
        AppendString(bytes, tags.ToString());

        // 3. Arguments, in order.
        foreach (var arg in args)
        {
            if (arg is int i) AppendInt(bytes, i);
            else if (arg is float f) AppendFloat(bytes, f);
            else if (arg is string s) AppendString(bytes, s);
        }

        return bytes.ToArray();
    }

    static void AppendString(List<byte> bytes, string value)
    {
        byte[] str = Encoding.ASCII.GetBytes(value);
        bytes.AddRange(str);
        bytes.Add(0); // null terminator
        Pad(bytes);
    }

    static void AppendInt(List<byte> bytes, int value)
    {
        byte[] b = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(b); // OSC is big-endian
        bytes.AddRange(b);
    }

    static void AppendFloat(List<byte> bytes, float value)
    {
        byte[] b = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(b); // OSC is big-endian
        bytes.AddRange(b);
    }

    static void Pad(List<byte> bytes)
    {
        while (bytes.Count % 4 != 0)
            bytes.Add(0);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        client?.Close();
        client = null;
    }
}
