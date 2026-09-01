# MoDi Connect

[English](README_EN.md) | [简体中文](README.md)

> Windows 1.0 stability and release validation records are available in
> [Windows 1.0 release evidence](artifacts/release-evidence/windows-1.0/README.md).
> Automated release gates have passed, while the Win10/Win11 physical-device matrix is still being validated step by step.
>
> Android automated validation and the remaining physical-device test matrix are available in
> [Android 1.0 release evidence](artifacts/release-evidence/android-1.0/README.md).

> **Cross-device audio connectivity for Windows and Android** — making audio flow more naturally between your devices.

[![License](https://img.shields.io/badge/License-GPL--3.0--or--later-blue)](LICENSE)
[![Protocol](https://img.shields.io/badge/Protocol-0.1.1-orange)](https://github.com/LaoYinBai/MoDi-Connect-Protocol)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android-green)]()
[![网站](https://img.shields.io/badge/Website-modiconnect.cn-8a2be2)](https://modiconnect.cn)

**[网站](https://modiconnect.cn)** ·
**[Downloads](https://github.com/LaoYinBai/MoDi-Connect/releases)** ·
**[Protocol Specification](https://github.com/LaoYinBai/MoDi-Connect-Protocol)**

MoDi Connect is an open-source cross-device audio connectivity application.

It can transmit system audio or microphone audio from an Android device to a Windows PC in real time through **LAN / Wi-Fi Direct / Bluetooth / USB**.

The received audio can either be played through the PC speakers or routed into a virtual microphone for use by games, meeting apps, streaming software, and other applications.

---

## ✨ Features

- **Four connection methods**
  - **主页**: automatically discovers PCs on the local network via mDNS
  - **Direct**: pair via QR code and connect directly through Wi-Fi Direct, with no router required
  - **Bluetooth / USB**: traditional hardware links for environments without an available network

- **Four audio routes**
  - System audio → PC speakers
  - System audio + microphone → PC speakers
  - Phone microphone → PC virtual microphone
  - System audio → PC virtual microphone

- **Low-latency audio transport**
  - Opus codec
  - JitterBuffer for network jitter handling
  - FEC for packet-loss recovery

- **Modern ink-inspired UI**
  - A unified visual language across Windows and Android
  - Five-font typography system
  - Light and dark themes

- **Hot switching while streaming**
  - Switch audio routes or connection methods without restarting the stream

---

## 📸 Screenshots

| Windows · Main Interface (Dark) | Android · Main Interface |
|:---:|:---:|
| ![Windows UI](assets/screenshots/win-real-ui.png) | ![Android UI](assets/screenshots/android-main.png) |

---

## 🚀 Quick Start

### Windows

1. Download `MoDi-Win-1.0.0-setup.exe` from [发布](https://github.com/LaoYinBai/MoDi-Connect/releases).
2. Run the installer. The .NET runtime is bundled with the package, so no additional runtime installation is required.
3. If you want to use the **Virtual Microphone** routes, enable the **VB-CABLE setup guide** at the end of installation. VB-CABLE will be installed from its official distribution source.

### Android

The main screen contains:

- **Top-left connection selector**
  - `主页`
  - `Direct`
  - `Bluetooth`
  - `USB`

- **Top-right QR scanner**
  - Used for Direct mode pairing

- **Four audio route cards**
  - **Speaker** — System audio → PC speakers
  - **Mixed Audio** — System audio + microphone → PC speakers
  - **Virtual Microphone** — Phone microphone → PC input
  - **System-to-Mic** — System audio → PC input

- **Bottom navigation**
  - Audio Effects
  - **Start Streaming**
  - My

### Connecting

1. Open MoDi Connect. The app starts in **主页** mode by default and automatically discovers PCs on the local network using mDNS.

2. Select an audio route.

   For example:

   - To play phone audio through your PC speakers → **Speaker**
   - To use your phone as a microphone for games or meetings → **Virtual Microphone**

3. Tap **Start Streaming**.

4. Audio routes can be switched while streaming without interrupting the session.

The **My** page also provides access to project stories, support options, technical support, and the official website.

### Switching Connection Methods

Use the selector in the upper-left corner to switch between:

- **Direct** — Scan the QR code shown on the PC and establish a Wi-Fi Direct connection
- **Bluetooth**
- **USB**

If the connection method is changed during streaming, MoDi Connect will automatically disconnect the previous link before connecting through the new one.

---

## 💡 Common Use Cases

| Use Case | Connection | Audio Route |
|---|---|---|
| Play phone audio through PC speakers |主页| Speaker |
| Use phone microphone in games or meetings | Direct | Virtual Microphone |
| Send phone system audio into streaming software | Bluetooth | System-to-Mic |

---

## 🔧 Build from Source

### Windows

```bash
dotnet publish windows/MoDi.Desktop/MoDi.Desktop.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:DebugSymbols=false -p:DebugType=None
```

### Android

```bash
cd android
./gradlew :app:assembleRelease
```

> The application references a fixed binary release of MoDi Protocol 0.1.1.
> Before building, place the required protocol binaries under
> `third_party/modi-protocol/`.

---

## 📁 Project Structure

```text
├── windows/
│   ├── MoDi.Desktop/        # Windows app: receiving, pairing, audio and routing
│   ├── MoDi.Presentation/   # Shared ink-inspired UI, typography and design tokens
│   └── MoDi.App.Contracts/  # Platform-independent contracts
│
├── android/
│   └── app/src/main/        # Capture, encoding, connection methods and UI
│
├── content/                 # Shared Markdown content
├── third_party/modi-protocol/
│                            # MoDi Protocol 0.1.1 binaries and license files
└── scripts/                 # Validation and release scripts
```

---

## 📄 License

- **Application code in this repository**:
  [GPL-3.0-or-later](LICENSE)

- **MoDi Protocol 0.1.1**:
  Proprietary license.

  The application links against the protocol as a binary dependency.
  Usage and redistribution are governed by:

  - `Licenses/MoDi.Protocol/BINARY-REDISTRIBUTION-GRANT.txt`
  - `Licenses/MoDi.Protocol/MODI-PROTOCOL-BINARY-LINKING-EXCEPTION-1.0.txt`

---

## 🏠 Community Edition and Official Edition

> **Important: GitHub distributes the Community Edition, which does not include the official automatic update service.**

| | Community Edition | Official Edition |
|---|---|---|
| Distribution | GitHub source code + Releases | [modiconnect.cn](https://modiconnect.cn) |
| Source Code | ✅ Fully available under GPL-3.0 | Based on the same source |
| Installers | ✅ Setup.exe / APK available through Releases | Available from the official website |
| Automatic Updates | ❌ Not included | ✅ Official update service |
| Additional Services | — | See the official website |

### Community Edition

Designed for developers, enthusiasts, and open-source users.

The source code is available for building, studying, modifying, and redistributing under the applicable license.

### Official Edition

Designed for users who prefer a maintained distribution with services such as automatic updates and official technical support.

The core application functionality is the same between the two editions.

The primary differences are the official update service and additional support services.

To learn more about the Official Edition, visit:

**https://modiconnect.cn**

Official services may be paid. Please refer to the website for the latest information.

---

## ⚠️ Disclaimer

MoDi Connect is intended solely for lawful and authorized device-connectivity scenarios.

Do not use the software for unauthorized recording, surveillance, monitoring, or any activity that infringes upon the privacy or rights of others.

The developer assumes no responsibility for misuse of the software.

---

**MoDi Connect** began with a simple idea: bridge audio between devices, and make sound flow more naturally from one device to another.
