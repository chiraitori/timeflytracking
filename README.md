# 🎨 TimeFly · Digital Art Focus Tracker

<p align="center">
  <strong>A native Windows 11 desktop application designed to track focus time, analyze drawing habits, and help beginners stay consistent with digital art.</strong>
</p>

---

## 💡 About The Project

> *"I created TimeFly as a personal side project because I have **zero drawing experience** and no art background. After getting a drawing tablet, I wanted to see if I could actually learn and stick with drawing consistently. I built TimeFly to hold myself accountable, track my daily focus time, and build a lasting creative habit."*

What started as an experimental Python prototype has now been completely re-engineered into a fast, native Windows 11 application using **C#**, **.NET 8**, and **WinUI 3 (Windows App SDK)**.

TimeFly runs silently in the background, automatically tracks when your drawing software and canvases are active, monitors tablet and stylus digitizer inputs, and provides clear analytics to keep your creative momentum going.

---

## ✨ Features

- **🎯 Automatic Focus & Canvas Tracking**: Automatically detects when supported drawing software is in the foreground (Krita, CLIP Studio Paint, Photoshop, Aseprite, Blender, Paint Tool SAI 2, etc.) and tracks individual canvas titles in real time.
- **🖋️ Drawing Tablet & Stylus Diagnostics**: Integrated with the OpenTabletDriver database (500+ tablet models) to accurately identify connected graphic tablets (XP-Pen, Wacom, Huion, Gaomon, Veikk, Xencelabs), stylus pressure levels (up to 16,384 levels), and active driver daemons.
- **⏱️ Smart Idle / AFK Detection**: Pauses the timer automatically when no tablet pen, mouse, or keyboard activity is detected.
- **🔥 Daily Drawing Goals & Streaks**: Set daily minute targets, track dynamic goal rings, and build daily artistic streaks.
- **📊 Studio Analytics & Heatmaps**:
  - 7-day focus distribution bar charts
  - Top artwork breakdown by time spent
  - 24-hour activity heatmaps showing when you draw most often
- **🖼️ Artwork Library**: Searchable database of all artwork files tracked, total time invested, session counts, and last modified dates.
- **📜 Detailed Session History**: View granular session history, edit custom tags/notes, and export data directly to CSV.
- **🪟 Windows 11 Native Fluent UI**: Modern Mica dark theme with docked navigation, Segoe Fluent Icons, and minimize-to-system-tray support.
- **⚡ Zero Overhead**: Standalone native .NET 8 binary with zero Python or web-wrapper dependencies.

---

## 🛠️ Tech Stack & Architecture

- **UI Framework**: WinUI 3 / Windows App SDK 1.8
- **Language & Runtime**: C# 12 / .NET 8 (Self-Contained)
- **Database**: SQLite via Microsoft.Data.Sqlite (%USERPROFILE%\.timefly\timefly.db)
- **Hardware Diagnostics**: Windows PnP SetupAPI & OpenTabletDriver definitions
- **Project Structure**:
  - TimeFly.App — WinUI 3 desktop shell and views
  - TimeFly.Core — Foreground tracking, idle hooks, title parsing, and tablet detection
  - TimeFly.Data — SQLite database persistence and analytics
  - TimeFly.Tests — Unit test suite

---

## 📥 Download & Installation

Download the latest version from the **[Releases](https://github.com/chiraitori/timeflytracking/releases)** page:

| Edition | File | Description |
| :--- | :--- | :--- |
| **Standard Installer** | `TimeFly-Setup-x64.exe` | Clean NSIS setup with Start Menu & Desktop shortcuts. Installs per-user without requiring admin privileges. |
| **Portable Edition** | `TimeFly-windows-x64-portable.zip` | Compact standalone archive with single-file executable. Just unzip and run `TimeFly.App.exe`. |

---

## 🚀 Getting Started (Developers)

### Prerequisites
- Windows 10 (version 1809+) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (if building from source)

### Building from Source

`powershell
# Clone the repository
git clone https://github.com/chiraitori/timeflytracking.git
cd timeflytracking

# Build the solution
dotnet build TimeFly.slnx -p:Platform=x64

# Run the application
dotnet run --project TimeFly.App -p:Platform=x64
`

### Publishing a Self-Contained Release

`powershell
dotnet publish TimeFly.App/TimeFly.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o dist/TimeFly
`

The compiled standalone executable will be located at dist/TimeFly/TimeFly.App.exe.

---

## 🎨 Credits & Acknowledgments

- App Icon & Artwork courtesy of Pixiv ([Artwork ID: 148639751](https://www.pixiv.net/en/artworks/148639751)).
- Tablet definitions sourced from [OpenTabletDriver](https://github.com/OpenTabletDriver/OpenTabletDriver).

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
