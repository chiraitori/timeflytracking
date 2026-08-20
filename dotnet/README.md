# TimeFly for .NET

This directory contains the native C#/.NET WinUI 3 edition.

Projects:

- `TimeFly.App` — WinUI 3 desktop interface and native title bar
- `TimeFly.Core` — active-window, idle-time, title parsing, and tablet detection
- `TimeFly.Data` — SQLite persistence compatible with the Python app
- `TimeFly.Tests` — compatibility and parsing tests

## Build and run

```powershell
dotnet build TimeFly.slnx -p:Platform=x64
dotnet run --project TimeFly.App -p:Platform=x64
```

The app reads the same `%USERPROFILE%\.timefly\timefly.db` file as the Python version.

## Included features

- automatic foreground drawing-app tracking with AFK detection and session checkpoints
- pause/resume and manual session logging
- daily goal, streak, weekly/project/hourly analytics
- searchable artwork library and session history
- tag/note editing, deletion, and CSV export
- configurable tracked apps, tablet/driver diagnostics, and native system tray behavior
- single-instance startup with no console window or Python runtime

The self-contained release is written to `dist\TimeFly\TimeFly.App.exe`.
