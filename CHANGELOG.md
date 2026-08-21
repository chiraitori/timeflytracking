# 📜 TimeFly Changelog

All notable changes to **TimeFly** will be documented in this file.

---

## [v0.3.0] - 2026-08-21

### 🎨 Studio Dark UI & Color Palette Overhaul
- **Zero "AI Purple" Policy**: Replaced cliché generic neon/indigo purple palette (`#6366F1`, `#161426`, `#262446`) with a refined **Art Studio Graphite & Electric Azure** design system (`#38BDF8` / `#0EA5E9`).
- **Surface Modernization**:
  - Deep Neutral Obsidian Background (`#0B0C0E`).
  - Subtle Graphite Cards (`#18191E`) with crisp 1px borders (`#272A32`).
  - Neutral Slate Badges (`#1E2028` with `#94A3B8` text) for software chips (Krita, Photoshop, CSP).
- **Emerald Activity Heatmap**: Replaced purple hourly gradient with a GitHub/Strava-style 4-level emerald matrix (`#0E3A2F` → `#10B981`).
- **Semantic Status System**:
  - 🟢 `DRAWING`: `#064E3B` background / `#34D399` Emerald text.
  - 🟡 `STANDBY`: `#1E2433` background / `#94A3B8` Slate text.
  - 🟠 `IDLE`: `#3A2308` background / `#FBBF24` Amber text.
  - 🔴 `PAUSED`: `#3A141A` background / `#F87171` Coral text.

### 🪟 Native Windows Fluent Icons
- Replaced all header emojis in the Kanban Board with monochrome native **Segoe Fluent Icons** (`&#xEA80;`, `&#xEDC6;`, `&#xE790;`, `&#xE73E;`).

### 🔔 Update System Polish
- **Non-overlapping Notification Bar**: Moved `UpdateNotificationBar` to a dedicated Grid row so it never covers page titles or navigation content.
- **Dual Action Buttons**: Added both **Download Update** and **Changelog** buttons in the update notification.
- **In-App Changelog Viewer**: Digital artists can now view what's new directly in the desktop app.

---

## [v0.2.0] - 2026-08-20

### 📋 Artist Project Kanban Board
- **4-Stage Art Production Pipeline**:
  - 💡 *Idea & Backlog*
  - ✏️ *Sketch & Lineart*
  - 🎨 *Color & Render*
  - ✅ *Completed*
- **Interactive Checklists**: Add sub-tasks to any project card with live checkboxes.
- **Linked Canvas Time Tracking**: Connect Kanban cards to active artwork files in Krita/Photoshop to see real accumulated drawing hours on the card.
- **Priority Tags**: High (🔴), Medium (🟡), Low (⚪) flags.

### 📦 Installer & Reliability Improvements
- **Auto-Kill Process on Update**: NSIS installer now automatically terminates any running background or tray instance of `TimeFly.App.exe` before file extraction, preventing `Error opening file for writing`.
- **Custom Branding**: Added custom branding signature (`Wet Nilou`).
- **Crash Fixes**: Resolved resource lookup crashes and thread synchronization issues when opening Settings and Kanban boards.

---

## [v0.1.0] - 2026-08-20

### 🚀 Initial Release
- **Automatic Drawing Tracker Engine**: Real-time focus time tracking for Krita, CLIP Studio Paint, Photoshop, Aseprite, Blender, SAI 2, etc.
- **Session & Focus Block Separation**: Alt-Tab grace period (up to 3 minutes) maintains consistent drawing sessions without fragmenting records.
- **Drawing Tablet Diagnostics**: Automatic detection of drawing tablets (XP-Pen, Wacom, Huion, Gaomon, etc.) via OpenTabletDriver database.
- **Studio Analytics & History**: Daily drawing targets, streaks, CSV export, and SQLite database persistence.
