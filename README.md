# Questlog — Real-Life Dungeon Master Desktop Client

A native Windows desktop console for **Real-Life Dungeon Master** built with **C#**, **.NET 10**, and **WPF**. Combines real-time OS telemetry, native Win32 window tracking, and a warm-neutral tactical command center UI.

---

## Key Features

- **Real-Time Window Telemetry**:
  - Automatically identifies foreground process and window title via Win32 `GetForegroundWindow`.
  - Classifies activity in real time (Development, Productivity, Browsing, Entertainment, Communication).
  - Detects user inactivity with Win32 `GetLastInputInfo` idle polling.
- **Calm Tactical Command Console**:
  - Implements the warm-neutral design palette (`#F4F1EA`, `#FBFAF7`, `#ECE9E1`, `#D85C32` accent).
  - Adheres strictly to **Anti-AI-Slop** design principles: crisp solid surfaces, purposeful contrast, readable typography, and no generic purple gradients or glassmorphism.
- **Zero-Dependency Native MVVM**:
  - Clean `ObservableObject`, `RelayCommand`, and `AsyncRelayCommand` implementations built on top of the standard .NET runtime — no external NuGet dependency bloat.
- **Offline Resilient Batch Syncing**:
  - Queues activity chunks locally and periodically syncs batches to the FastAPI backend daemon every 30 seconds.
- **Full View Suite**:
  - **Overview (Dashboard)**: Campaign score, streak days, productive minutes, current focus app, and quick quest completion.
  - **Daily Schedule**: AI-generated time-blocked itinerary for the day.
  - **Active Quests**: Interactive quest forge to create, manage, and complete prioritized tasks.
  - **Tactical Analytics**: Deep work percentages, category distribution, and transparent score audit trail.
  - **Window Tracker**: Live feed of active applications and durations.
  - **Dungeon Master AI**: Conversational coaching chat interface for strategic guidance.
  - **System Settings**: Backend daemon connection tester and idle threshold tuning.

---

## Tech Stack

- **Platform**: Windows 10 / 11
- **Runtime**: .NET 10.0-windows
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Architecture**: MVVM (Model-View-ViewModel) + Direct IoC Composition
- **Native Interop**: Win32 P/Invoke (`user32.dll`)

---

## Directory Structure

```
Questlog/
├── Questlog.slnx            # Solution file
├── Questlog.csproj          # .NET 10 project file
├── App.xaml / App.xaml.cs   # Application shell & dependency wiring
├── MainWindow.xaml / .cs    # Command console shell with left navigation
├── Common/                  # Native MVVM primitives (ObservableObject, RelayCommand)
├── Models/                  # Data contracts matching backend REST schemas
├── Resources/               # Design tokens & styles
│   ├── Colors.xaml          # Palette definitions & theme brushes
│   ├── Typography.xaml      # Text styles (Display, Section, Card, Body)
│   └── Controls.xaml        # Buttons, text inputs, list templates
├── Services/                # Core service layer
│   ├── ApiClient.cs         # Resilient HTTP client for FastAPI backend
│   └── ActivityTrackerService.cs # Window polling, idle detection, batch flush
├── Tracking/                # Low-level Win32 P/Invoke interop
│   ├── ForegroundWindowTracker.cs
│   └── IdleDetector.cs
├── ViewModels/              # MVVM ViewModels
│   ├── MainViewModel.cs
│   ├── DashboardViewModel.cs
│   ├── ScheduleViewModel.cs
│   ├── GoalsViewModel.cs
│   ├── AnalyticsViewModel.cs
│   ├── ActivityViewModel.cs
│   ├── CoachViewModel.cs
│   └── SettingsViewModel.cs
├── Views/                   # UserControl view implementations
│   ├── DashboardView.xaml
│   ├── ScheduleView.xaml
│   ├── GoalsView.xaml
│   ├── AnalyticsView.xaml
│   ├── ActivityView.xaml
│   ├── CoachView.xaml
│   └── SettingsView.xaml
└── README.md
```

---

## Building and Running

### Prerequisites
- Windows 10 (version 1903+) or Windows 11
- .NET 10 SDK (or compatible .NET 9/10 SDK)

### Build
From inside this directory:
```bash
dotnet build
```

### Run
```bash
dotnet run
```

### Visual Studio
You can open `Questlog.slnx` directly in Visual Studio 2022 / 2025 or JetBrains Rider and press `F5`.

---

## Backend Integration

By default, Questlog connects to the local backend daemon at:
```
http://127.0.0.1:8000
```
If the backend is not running, Questlog operates smoothly in offline mode, buffering telemetry sessions until connection is restored. You can configure and verify backend connectivity at any time from the **Settings** view inside the app.
