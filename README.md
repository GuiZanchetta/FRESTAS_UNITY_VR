# FRESTAS — Unity VR

Unity project for **FRESTAS**, a research initiative exploring telematic music performance through spatial audio and immersive environments. This repository contains the VR setup targeting **Apple Vision Pro**, combining **ambisonics** audio rendering with a real-time telematic audio pipeline.

---

## Overview

FRESTAS investigates new forms of networked musical performance where performers and audiences occupy spatially distinct physical locations yet share a common virtual acoustic space. The system bridges live ambisonics capture, transmission, and reproduction inside a VR environment, enabling presence-driven listening experiences that would be impossible in a conventional concert hall.

This Unity project handles:

- Spatial audio rendering (B-format ambisonics decoding) on Apple Vision Pro
- Scene and environment setup for the immersive listening context
- Integration with the telematic audio pipeline (network audio receive/playback)
- VR interaction and presence design for visionOS

---

## System Architecture

```
[Remote Performer / Venue]
        |
  Ambisonics Capture
  (microphone array)
        |
  Network Transmission
  (low-latency audio protocol)
        |
[Apple Vision Pro — this repo]
  Unity visionOS build
  Ambisonics decode & binaural render
  Spatial scene / visual context
```

---

## Requirements

| Dependency | Version |
|---|---|
| Unity | TBD |
| Unity visionOS Build Support | TBD |
| Apple Vision Pro SDK (PolySpatial) | TBD |
| Ambisonics plugin | TBD |
| macOS (for Xcode build) | TBD |

> Fill in specific versions once the Unity project is initialized.

---

## Getting Started

1. **Clone the repository**
   ```
   git clone <repo-url>
   cd FRESTAS_UNITY_VR
   ```

2. **Open in Unity Hub** — select the correct Unity version with visionOS Build Support installed.

3. **Install packages** — Unity Package Manager will restore dependencies from `Packages/manifest.json` on first open.

4. **Configure audio pipeline** — see `docs/audio-setup.md` (TBD) for network audio source configuration.

5. **Build for visionOS** — target platform: visionOS, deploy via Xcode to a connected Apple Vision Pro.

---

## Project Structure

```
Assets/
  Scenes/          # Unity scenes
  Scripts/         # C# runtime code
  Audio/           # Ambisonics IRs, audio assets
  Prefabs/         # Reusable VR objects
Packages/          # Unity package manifest
ProjectSettings/   # Unity project settings
```

---

## Research Context

FRESTAS is a research project exploring telematic performance and spatial audio. This repository covers the VR/XR component of the system. Companion repositories handle audio capture, transmission, and server-side processing.

---

## Contributors



---

## License

TBD


using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetworkHud : MonoBehaviour
{
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7776;
   
     void SetTransport()
{
    var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
    transport.SetConnectionData(address, port);
}

    void OnGUI()
    {
        if (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) return;

        if (GUILayout.Button("Host"))
        {
            SetTransport();
            NetworkManager.Singleton.StartHost();
        }
        if (GUILayout.Button("Client"))
        {
            SetTransport();
            NetworkManager.Singleton.StartClient();
        }
    }

    void SetTransport()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(address, port);
        // Host will listen on 7776; client will connect to address:7776
    }
}



----
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Minimal world-space connection UI for Netcode for GameObjects,
/// compatible with visionOS (PolySpatial). OnGUI does not render on
/// Vision Pro, so this builds a world-space Canvas with Host/Client
/// buttons and a status label at runtime.
/// Attach to any GameObject in the scene (e.g. the NetworkManager).
/// </summary>
public class VisionNetworkUI : MonoBehaviour
{
    [Header("Connection")]
    [Tooltip("127.0.0.1 for editor tests; your Mac's LAN IP for the Vision Pro build")]
    [SerializeField] private string address = "127.0.0.1";
    [SerializeField] private ushort port = 7776;

    [Header("Fully immersive (VR/Metal) mode")]
    [Tooltip("On the Vision Pro build, start as client automatically — no UI input needed")]
    [SerializeField] private bool autoStartClientOnDevice = true;
    [SerializeField] private float autoStartDelay = 2f;

    [Header("UI Placement (meters, relative to world origin)")]
    [SerializeField] private Vector3 panelPosition = new Vector3(0f, 1.4f, 1.2f);

    private Text statusText;
    private GameObject hostButton;
    private GameObject clientButton;

    void Start()
    {
        BuildUI();
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

#if UNITY_VISIONOS && !UNITY_EDITOR
        if (autoStartClientOnDevice)
            StartCoroutine(AutoStartClient());
#endif
    }

    private IEnumerator AutoStartClient()
    {
        statusText.text = $"Auto-connecting to {address}:{port} in {autoStartDelay:0}s";
        yield return new WaitForSeconds(autoStartDelay);
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
            StartClient();
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // ---------------- Network ----------------

    private void StartHost()
    {
        SetTransport();
        NetworkManager.Singleton.StartHost();
        statusText.text = $"Hosting on port {port}";
        ToggleButtons(false);
    }

    private void StartClient()
    {
        SetTransport();
        NetworkManager.Singleton.StartClient();
        statusText.text = $"Connecting to {address}:{port} ...";
        ToggleButtons(false);
    }

    private void SetTransport()
    {
        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        transport.SetConnectionData(address, port);
    }

    private void OnClientConnected(ulong clientId)
    {
        statusText.text = NetworkManager.Singleton.IsHost
            ? $"Host | clients: {NetworkManager.Singleton.ConnectedClientsList.Count}"
            : $"Connected (id {NetworkManager.Singleton.LocalClientId})";
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsConnectedClient && !NetworkManager.Singleton.IsHost)
        {
            statusText.text = "Disconnected";
            ToggleButtons(true);
        }
    }

    private void ToggleButtons(bool visible)
    {
        hostButton.SetActive(visible);
        clientButton.SetActive(visible);
    }

    // ---------------- UI construction ----------------

    private void BuildUI()
    {
        var canvasGO = new GameObject("NetCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rt = (RectTransform)canvas.transform;
        rt.sizeDelta = new Vector2(600, 420);
        canvasGO.transform.position = panelPosition;
        canvasGO.transform.localScale = Vector3.one * 0.001f; // 600 px -> 0.6 m wide

        CreatePanel(canvas.transform);

        statusText = CreateText(canvas.transform, "Disconnected", 40,
            new Vector2(0, 140), new Vector2(560, 80));

        hostButton = CreateButton(canvas.transform, $"Host  :{port}",
            new Vector2(0, 20), StartHost);

        clientButton = CreateButton(canvas.transform, "Connect as Client",
            new Vector2(0, -120), StartClient);

        EnsureEventSystem();
    }

    private void CreatePanel(Transform parent)
    {
        var go = new GameObject("Panel", typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
    }

    private GameObject CreateButton(Transform parent, string label, Vector2 pos,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(420, 110);
        rt.anchoredPosition = pos;
        go.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.9f, 0.95f);
        go.GetComponent<Button>().onClick.AddListener(onClick);
        CreateText(go.transform, label, 40, Vector2.zero, new Vector2(420, 110));
        return go;
    }

    private Text CreateText(Transform parent, string content, int size,
        Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject("Text", typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = pos;
        var t = go.GetComponent<Text>();
        t.text = content;
        t.font = GetFont();
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        return t;
    }

    private static Font GetFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return f;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }
}
