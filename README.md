# pc-vitals-widget

A lightweight, borderless floating HUD widget for Windows that displays real-time **CPU**, **GPU**, **VRAM**, and **RAM** vitals in the corner of your screen.

![Version](https://img.shields.io/badge/version-1.0.0-2EA043?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20(x64)-0078D6?style=flat-square)
![Python](https://img.shields.io/badge/python-3.8%2B%20(stdlib%20only)-3776AB?style=flat-square)
![Backend](https://img.shields.io/badge/.NET%20Framework-4.7.2%2B-512BD4?style=flat-square)

![PC Vitals Widget Preview](assets/preview.png)

---

## Features

- **Real-Time Hardware Telemetry**
  - **CPU:** Package/core temperature (`°C`) and utilization (`%`) with individual progress bars.
  - **GPU:** Core/edge temperature (`°C`), utilization (`%`, including D3D 3D/Copy/Video engines for accurate idle & light-load readings), and dedicated **VRAM** (`GB` + `%`).
  - **RAM:** Used/total physical memory (`GB`) and utilization (`%`) via native Windows `GlobalMemoryStatusEx`.
- **Clean Floating HUD Design**
  - Borderless, rounded-corner dark overlay that stays on top without cluttering the taskbar.
  - Distinct grouped sub-cards for **CPU**, **GPU**, and **RAM** with color-coded left accent bars.
  - Dynamic thermal and load color indicators (green → yellow → orange → red).
  - **Normal** and **Compact** display modes.
- **Broad Hardware Support**
  - Powered by [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) — supports Intel and AMD CPUs as well as AMD, NVIDIA, and Intel GPUs.
- **Zero External Python Dependencies**
  - Runs using only the Python standard library (`tkinter`, `ctypes`, `subprocess`, `json`, `threading`).
- **Persistent Settings**
  - Automatically saves your preferred screen corner or custom position, opacity, compact mode, and always-on-top state in `config.json`.

---

## Requirements

- **OS:** Windows 10 or Windows 11 (64-bit)
- **Python:** Python 3.8 or newer (with `tkinter` and `pythonw.exe`, included in the official Windows installer)
- **.NET Runtime:** .NET Framework 4.7.2 or newer (pre-installed on Windows 10/11)

---

## Quick Start

1. **Clone or download the repository:**
   ```bash
   git clone https://github.com/felipematheuslo/pc-vitals-widget.git
   cd pc-vitals-widget
   ```

2. **Launch the widget:**
   - **Recommended (Full CPU + GPU + VRAM + RAM telemetry):**
     Double-click **`launch_widget.vbs`** and accept the UAC Administrator prompt.
     > **Why Administrator?** Windows restricts direct access to CPU thermal MSR sensors for standard users. Running elevated allows `LibreHardwareMonitorLib` to read accurate CPU package/core temperatures.
   - **Without Administrator (GPU + VRAM + RAM + CPU load):**
     Double-click **`pc_vitals_widget.pyw`** (or run `pythonw pc_vitals_widget.pyw`). You can elevate at any time by clicking the `⚡ Enable CPU Sensor (Admin)` prompt on the widget or via the context menu.

---

## Controls & Customization

| Action | Behavior |
| --- | --- |
| **Left-click & drag** | Move the widget anywhere on screen (position is remembered) |
| **Double-click** | Toggle between **Normal** and **Compact** mode |
| **Right-click** (or click `⋮`) | Open the settings menu (screen corner, opacity, compact mode, always on top, run as admin, close) |
| **Click `×`** or press **`Esc`** | Close the widget and cleanly stop the background sensor process |

### Color Thresholds

| Status | Temperature (`°C`) | Usage (`%`) |
| --- | --- | --- |
| Green | `< 60°C` | `< 70%` |
| Yellow | `60°C – 74°C` | `70% – 84%` |
| Orange | `75°C – 84°C` | `85% – 94%` |
| Red | `≥ 85°C` | `≥ 95%` |

---

## Architecture & Project Structure

```text
pc-vitals-widget/
├── launch_widget.vbs            # Silent UAC launcher (no console window)
├── pc_vitals_widget.pyw         # Floating tkinter HUD & RAM reader
├── config.json                  # Auto-generated user preferences (gitignored)
├── .gitignore
├── README.md
├── assets/
│   └── preview.png              # Desktop preview screenshot
└── lib/
    ├── SensorReader.cs          # C# sensor daemon source
    ├── sensor_reader.exe        # Compiled x64 sensor daemon
    ├── LibreHardwareMonitorLib.dll
    └── HidSharp.dll
```

### How It Works
1. `pc_vitals_widget.pyw` spawns `lib/sensor_reader.exe --interval 1000` as a hidden background process (`CREATE_NO_WINDOW`).
2. `sensor_reader.exe` queries hardware sensors via `LibreHardwareMonitorLib` once per second and streams newline-delimited JSON to `stdout`:
   ```json
   {"cpu_temp":54.0,"cpu_load":18.4,"cpu_name":"Intel Core i5-2310","gpu_temp":51.0,"gpu_load":4.2,"gpu_vram_used_mb":682.6,"gpu_vram_total_mb":4096.0,"gpu_name":"Radeon RX550/550 Series","is_admin":true}
   ```
3. System RAM is queried directly from Python using `kernel32.GlobalMemoryStatusEx`.
4. Closing the widget terminates the child `sensor_reader.exe` process cleanly.

---

## Building `sensor_reader.exe` from Source

No Visual Studio or .NET SDK installation is required — you can compile `lib/SensorReader.cs` using the C# compiler built into Windows:

```powershell
$lib = ".\lib"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& $csc /nologo /optimize+ /platform:x64 `
  /r:"$lib\LibreHardwareMonitorLib.dll" `
  /r:"$lib\HidSharp.dll" `
  /r:System.Management.dll `
  /out:"$lib\sensor_reader.exe" `
  "$lib\SensorReader.cs"
```

Verify the binary with a single-shot reading:
```powershell
.\lib\sensor_reader.exe --once
```

---

## Troubleshooting

- **CPU temperature shows `--°C`:**
  Launch the widget via `launch_widget.vbs` (or click the Admin prompt inside the widget) so `LibreHardwareMonitorLib` has permission to read CPU thermal sensors.
- **Nothing happens when double-clicking `launch_widget.vbs`:**
  Make sure Python is installed and `pythonw.exe` is available in your system `PATH`. You can test by running `python pc_vitals_widget.pyw` from a terminal to see any error output.
- **Configuration reset:**
  Delete `config.json` and relaunch the widget to restore default corner position and opacity.

---

## Acknowledgments

- Hardware sensor polling is powered by [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) (MPL-2.0) and [HidSharp](https://www.zer7.com/software/hidsharp).
