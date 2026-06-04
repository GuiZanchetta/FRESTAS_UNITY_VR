# FRESTAS — Unity VR

Unity project for **FRESTAS**, a research initiative exploring telematic music performance through spatial audio and immersive environments. This repository contains the VR setup targeting **Apple Vision Pro**, combining **ambisonics** audio rendering with a real-time telematic audio pipeline.

---

## Overview


This Unity project handles:

- Spatial audio triggering using OSC
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
  
  
```

---

## Requirements

| Dependency | Version |
|---|---|
| Unity | TBD |
| Unity visionOS Build Support | TBD |
| Apple Vision Pro SDK (PolySpatial) | TBD |
| macOS (for Xcode build) |

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

- Amber

- Guilherme
 
- Axel


---

## License

TBD
