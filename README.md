<p align="center">
  <img src="src/logo/Nsm Pro App Logo.png" alt="NetMonitor Pro Logo" width="120"/>
</p>

<h1 align="center">NetMonitor Pro</h1>

<p align="center">
  <b>A feature-rich, real-time network speed monitor &amp; dashboard for Windows</b><br/>
  Built with WPF · .NET 8 · MVVM · LiveCharts · SQLite
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white" alt="Windows"/>
  <img src="https://img.shields.io/badge/UI-WPF-6A1B9A" alt="WPF"/>
  <img src="https://img.shields.io/badge/License-MIT-green" alt="MIT License"/>
</p>

---

## ✨ Overview

**NetMonitor Pro** is a sleek, always-on-top network speed overlay and full-featured dashboard application for Windows. It provides real-time upload/download speed monitoring, comprehensive network adapter details, internet speed testing, usage history tracking, and smart alerts — all wrapped in a modern dark-themed UI.

---

## 🚀 Features

### 📊 Real-Time Floating Overlay
- **Always-on-top** speed widget that displays live download (↓) and upload (↑) speeds
- **Draggable** — click and drag to reposition anywhere on screen
- **Taskbar-aware snapping** — auto-positions near the taskbar (supports all taskbar positions: top, bottom, left, right)
- **Adjustable opacity** — slider to control overlay transparency (30%–100%)
- **Click-through mode** — optional mode to pass mouse clicks through the overlay
- **Glow effects** — text glow animations for download (green) and upload (blue) indicators
- **Gradient background** — dark navy-to-deep-blue gradient with drop shadow

### 🖥️ Full Dashboard
A multi-page dashboard with sidebar navigation:

| Page | Description |
|------|-------------|
| **📊 Overview** | Real-time speed cards (Download, Upload, Session ↓, Session ↑) + live speed chart |
| **📈 Usage History** | 30-day bar chart of daily download/upload data usage |
| **🌐 Network Info** | Detailed view of all network adapters with IP, gateway, MAC, link speed |
| **⚡ Speed Test** | One-click internet speed test with download, upload, and latency results |
| **⚙ Settings** | Full configuration panel for all app behaviors and alerts |

### ⚡ Speed Test
- **Download speed** — tests against Cloudflare and OVH endpoints with real-time progress
- **Upload speed** — measures upload throughput via POST to Cloudflare
- **Latency (Ping)** — ICMP ping to Google DNS (8.8.8.8), Cloudflare (1.1.1.1), and OpenDNS (208.67.222.222)
- **Live progress bar** — shows real-time speed during test with cancel support
- **History tracking** — all speed test results are persisted to SQLite for historical review

### 🔔 Smart Alerts
- **Speed Drop Alert** — notifies when download speed falls below a configurable threshold (default: 1.0 Mbps)
- **Data Cap Alert** — warns when daily data usage exceeds a custom limit (default: 10 GB)
- **Disconnect Alert** — instant notification when network connectivity is lost
- **Rate-limited** — alerts are throttled to avoid notification spam (30-second cooldown)

### 🌐 Network Adapter Details
- Full enumeration of all network interfaces (Ethernet, Wi-Fi, Virtual, etc.)
- Displays for each adapter:
  - **Status** (Up / Down)
  - **Type** (Ethernet, Wireless80211, etc.)
  - **IPv4 Address** & **Subnet Mask**
  - **IPv6 Address**
  - **Default Gateway**
  - **DNS Servers**
  - **MAC Address**
  - **Link Speed** (Gbps / Mbps / Kbps)
  - **Bytes Sent / Received** (cumulative)

### 💾 Data Persistence (SQLite)
- **Daily Usage** — tracks download/upload bytes per day per adapter with peak speeds
- **Session Logs** — records session start/end times with average and total transfer stats
- **Process History** — per-process bandwidth usage tracking by date
- **Network Events** — logs speed drops, disconnects, and threshold alerts with severity levels
- **Speed Test History** — stores all speed test results including download, upload, latency, jitter, and packet loss

### ⚙️ Configurable Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Always on Top | Keep overlay above all windows | ✅ On |
| Launch on Startup | Auto-start with Windows | ❌ Off |
| Start Minimized | Launch to tray without showing overlay | ❌ Off |
| Click-Through | Pass mouse clicks through overlay | ❌ Off |
| Network Adapter | Select which adapter to monitor (or auto-detect) | Auto |
| Update Interval | Polling frequency (250ms–5000ms) | 1000ms |
| Overlay Opacity | Background transparency | 92% |
| Alert on Disconnect | Notify on connection loss | ✅ On |
| Alert on Speed Drop | Notify when speed falls below threshold | ✅ On |
| Speed Drop Threshold | Minimum acceptable speed | 1.0 Mbps |
| Alert on Data Cap | Notify when daily usage exceeds limit | ❌ Off |
| Daily Data Cap | Maximum daily data usage | 10 GB |
| Dark Mode | Dark theme enabled | ✅ On |

### 🎨 System Tray Integration
- **Custom tray icon** — uses the app's branded PNG icon (with fallback to generated icon)
- **Double-click** to toggle overlay visibility
- **Right-click context menu**:
  - 👁 Show / Hide Overlay
  - 📊 Open Dashboard
  - ❌ Quit
- Runs minimized in the tray when overlay is hidden

---

## 🏗️ Architecture

NetMonitor Pro follows **Clean Architecture** principles with a modular project structure:

```
NetMonitorPro.sln
├── src/
│   ├── NetMonitorPro.App         # WPF Application (Views, ViewModels, Converters, DI)
│   ├── NetMonitorPro.Core        # Business logic (Services, Models, Interfaces)
│   ├── NetMonitorPro.Data        # Data persistence (SQLite Entities, Repositories)
│   ├── NetMonitorPro.Native      # Windows-specific APIs (Taskbar detection via P/Invoke)
│   └── logo/                     # App & tray icon assets
└── tests/                        # Unit tests (coming soon)
```

### Key Design Patterns
- **MVVM** — ViewModels use `CommunityToolkit.Mvvm` with `[ObservableProperty]` and `[RelayCommand]`
- **Dependency Injection** — `Microsoft.Extensions.DependencyInjection` wires all services, ViewModels, and Views
- **Event-Driven** — `NetworkMonitorService` fires `StatsUpdated` events consumed by ViewModels and AlertService
- **Interface Segregation** — core services implement interfaces (`INetworkMonitorService`, `ISettingsService`)
- **Repository Pattern** — `DatabaseService` abstracts all SQLite persistence via `sqlite-net-pcl`

### Tech Stack

| Component | Technology |
|-----------|-----------|
| **Framework** | .NET 8 (Windows) |
| **UI** | WPF (Windows Presentation Foundation) |
| **MVVM Toolkit** | CommunityToolkit.Mvvm 8.2.2 |
| **Charts** | LiveChartsCore.SkiaSharpView.WPF 2.0 |
| **System Tray** | H.NotifyIcon.Wpf 2.1.3 |
| **Database** | SQLite via sqlite-net-pcl |
| **Serialization** | Newtonsoft.Json |
| **DI Container** | Microsoft.Extensions.DependencyInjection 8.0.1 |
| **Native Interop** | P/Invoke (Shell32 APPBARDATA) |

---

## 📦 Getting Started

### Prerequisites
- **Windows 10/11**
- **.NET 8 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Visual Studio 2022** (recommended) or any .NET-compatible IDE

### Build & Run

```bash
# Clone the repository
git clone https://github.com/nikhil914/NetSpeedMeterV2.git
cd NetSpeedMeterV2

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run --project src/NetMonitorPro.App
```

### Publish (Self-Contained)

```bash
dotnet publish src/NetMonitorPro.App -c Release -r win-x64 --self-contained true -o ./publish
```

---

## 📁 Data Storage

All application data is stored in:
```
%LOCALAPPDATA%\NetMonitorPro\
├── settings.json       # User preferences (JSON)
└── netmonitor.db       # Usage history, speed tests, events (SQLite)
```

---

## 🤝 Contributing

Contributions are welcome! Feel free to:
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/awesome-feature`)
3. Commit your changes (`git commit -m 'Add awesome feature'`)
4. Push to the branch (`git push origin feature/awesome-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file for details.

---

<p align="center">
  Made with ❤️ by <a href="https://github.com/nikhil914">Nikhil</a>
</p>
