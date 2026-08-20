# 🎨 TimeFly · Digital Art Focus Tracker

<p align="center">
  <strong>A lightweight, native Windows 11 desktop application for digital artists to track focus time, analyze drawing habits, and build daily creative consistency.</strong>
</p>

---

## 💡 About The Project

TimeFly was built as a fun passion side project during spare time to see if tracking digital art sessions could help develop a consistent, disciplined drawing habit with a drawing tablet. 

Instead of generic time-tracking software or heavy web-wrapped tools, TimeFly is built from the ground up as a native Windows 11 application using **C#**, **.NET 8**, and **WinUI 3 (Windows App SDK)**. It runs silently in the background, automatically recognizes your active art software and canvas files, connects directly with tablet digitizers, and provides insightful studio analytics.

---

## ✨ Key Features

- **🎯 Automatic Focus Tracking**: Instantly detects when you switch into supported digital art programs (Krita, CLIP Studio Paint, Photoshop, Aseprite, Blender, Paint Tool SAI 2, and more) and tracks individual canvas titles in real time.
- **🖋️ Drawing Tablet & Stylus Diagnostics**: Integrated with the OpenTabletDriver hardware database (500+ tablet models) to accurately identify connected graphic tablets (XP-Pen, Wacom, Huion, Gaomon, Veikk, Xencelabs), stylus pressure levels (up to 16,384 levels), and active driver daemons.
- **⏱️ Smart Idle / AFK Detection**: Automatically pauses the drawing session timer when no pen, mouse, or keyboard input is detected.
- **🔥 Daily Drawing Goals & Streaks**: Set daily minute targets, track dynamic goal rings, and build daily artistic streaks.
- **📊 Studio Analytics**:
  - 7-day focus distribution bar charts
  - Top artwork breakdown by time spent
  - 24-hour activity heatmaps showing when you draw most often
- **🖼️ Artwork Library**: Searchable database of all artworks created, total time invested, session counts, and last modified dates.
- **📜 Detailed Session History**: View granular session history, edit custom tags/notes, and export data directly to CSV.
- **🪟 Windows 11 Native Fluent UI**: Designed with Mica dark theme, responsive docked navigation, Segoe Fluent Icons, and seamless minimize-to-system-tray support.
- **⚡ Zero Overhead**: Standalone native binary with zero dependency on Python runtimes or Electron wrappers.

---

## 🛠️ Architecture & Tech Stack

- **UI Framework**: WinUI 3 / Windows App SDK 1.8
- **Language & Runtime**: C# 12 / .NET 8 (Self-Contained)
- **Database**: SQLite via Microsoft.Data.Sqlite (%USERPROFILE%\.timefly\timefly.db)
- **Hardware Detection**: Windows PnP SetupAPI & OpenTabletDriver hardware mapping
- **Architecture**:
  - TimeFly.App — WinUI 3 desktop shell and view layer
  - TimeFly.Core — Foreground tracking, idle hooks, title parsing, and tablet detection
  - TimeFly.Data — SQLite database persistence and analytics queries
  - TimeFly.Tests — Unit testing suite

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 (version 1809+) or Windows 11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (if building from source)

### Building from Source

Clone the repository and build using the .NET CLI:

`powershell
# Clone the repository
git clone https://github.com/chiraitori/timeflytracking.git
cd timeflytracking

# Build the solution
dotnet build dotnet/TimeFly.slnx -p:Platform=x64

# Run the application
dotnet run --project dotnet/TimeFly.App -p:Platform=x64
`

### Publishing a Self-Contained Release

`powershell
dotnet publish dotnet/TimeFly.App/TimeFly.App.csproj -c Release -r win-x64 --self-contained true -p:Platform=x64 -o dist/TimeFly
`

The compiled standalone executable will be located at dist/TimeFly/TimeFly.App.exe.

---

## 🎨 Artwork & Icon Credits

- App Icon & Artwork courtesy of Pixiv ([Artwork ID: 148639751](https://www.pixiv.net/en/artworks/148639751)).
- Tablet definitions sourced from [OpenTabletDriver](https://github.com/OpenTabletDriver/OpenTabletDriver).

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
