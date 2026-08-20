using System.Text.Json;
using TimeFly.Core.Models;
using TimeFly.Core.Services;
using TimeFly.Data;

namespace TimeFly.App.Services;

public sealed class TrackingEngine : IDisposable
{
    private static readonly string[] FallbackApps = ["krita.exe", "CLIPStudioPaint.exe", "Photoshop.exe", "Aseprite.exe", "blender.exe", "sai2.exe"];
    private readonly TimeFlyDatabase database;
    private readonly ActiveWindowService activeWindows;
    private readonly GearDetector gearDetector;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object gate = new();
    private Task? loopTask;
    private HashSet<string> trackedApps = new(StringComparer.OrdinalIgnoreCase);
    private int idleTimeoutSeconds;
    private bool paused;
    private bool enabled;
    private bool active;
    private bool idle;
    private string? currentApp;
    private string? currentCanvas;
    private DateTime? sessionStarted;
    private long sessionSeconds;
    private long idleSeconds;
    private DateTime lastGearScan = DateTime.MinValue;
    private GearInfo gear = new("Scanning for drawing tablet…", "Unknown", 0, false, false, new TabletDriver("None", "Scanning", 0, false), 0);

    public event EventHandler<TrackingUpdate>? SnapshotUpdated;
    public event EventHandler<SessionRecord>? SessionSaved;
    public bool IsPaused { get { lock (gate) return paused; } }
    public bool IsEnabled { get { lock (gate) return enabled; } }

    public TrackingEngine(TimeFlyDatabase database, ActiveWindowService activeWindows, GearDetector gearDetector)
    {
        this.database = database;
        this.activeWindows = activeWindows;
        this.gearDetector = gearDetector;
        RefreshSettings();
    }

    public void Start() => loopTask ??= Task.Run(RunAsync);

    public void RefreshSettings()
    {
        lock (gate)
        {
            enabled = database.GetBooleanSetting("auto_start_tracking", true);
            idleTimeoutSeconds = Math.Clamp(int.TryParse(database.GetSetting("idle_timeout_min", "3"), out var minutes) ? minutes : 3, 1, 30) * 60;
            try
            {
                var apps = JsonSerializer.Deserialize<string[]>(database.GetSetting("tracked_apps", "[]")) ?? FallbackApps;
                trackedApps = new HashSet<string>(apps.Length == 0 ? FallbackApps : apps, StringComparer.OrdinalIgnoreCase);
            }
            catch { trackedApps = new HashSet<string>(FallbackApps, StringComparer.OrdinalIgnoreCase); }
        }
    }

    public bool TogglePause()
    {
        lock (gate)
        {
            paused = !paused;
            if (paused) FlushCurrentSession();
            return paused;
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        try { loopTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        lock (gate) FlushCurrentSession();
        cancellation.Dispose();
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellation.Token)) Tick();
        }
        catch (OperationCanceledException) { }
    }

    private void Tick()
    {
        TrackingUpdate update;
        lock (gate)
        {
            if (DateTime.Now - lastGearScan >= TimeSpan.FromSeconds(5))
            {
                gear = gearDetector.Scan();
                lastGearScan = DateTime.Now;
            }

            if (!enabled)
            {
                if (active || idle || sessionSeconds > 0) FlushCurrentSession();
                var stats = database.GetAllTimeStats();
                update = new TrackingUpdate("Automatic tracking off", "Enable it in Settings", false, false, false, false, 0, 0, database.GetTodaySeconds(), 0, stats.DailyGoalMinutes, stats.StreakCount, gear);
            }
            else if (paused)
            {
                var stats = database.GetAllTimeStats();
                update = new TrackingUpdate("Tracking paused", "Resume when you're ready", false, false, true, true, 0, 0, database.GetTodaySeconds(), 0, stats.DailyGoalMinutes, stats.StreakCount, gear);
            }
            else
            {
                var snapshot = activeWindows.Capture(trackedApps);
                var isIdle = snapshot.IdleDuration.TotalSeconds >= idleTimeoutSeconds;
                if (snapshot.IsSelfApplication && currentApp is not null)
                {
                    // Opening TimeFly is an inspection of the current drawing session,
                    // not a reason to end it. Keep the previous canvas live so the UI
                    // can show its timer in real time; normal idle detection still applies.
                    if (!isIdle)
                    {
                        sessionSeconds++; active = true; idle = false;
                    }
                    else
                    {
                        active = false; idle = true; idleSeconds++;
                    }
                }
                else if (snapshot.IsTrackedApplication && !isIdle)
                {
                    if (!string.Equals(currentCanvas, snapshot.CanvasName, StringComparison.Ordinal) || !string.Equals(currentApp, snapshot.AppName, StringComparison.Ordinal))
                    {
                        FlushCurrentSession();
                        currentApp = snapshot.AppName;
                        currentCanvas = snapshot.CanvasName;
                        sessionStarted = DateTime.Now;
                    }
                    sessionSeconds++; active = true; idle = false;
                }
                else if (snapshot.IsTrackedApplication)
                {
                    if (currentApp is null)
                    {
                        currentApp = snapshot.AppName; currentCanvas = snapshot.CanvasName; sessionStarted = DateTime.Now;
                    }
                    active = false; idle = true; idleSeconds++;
                }
                else
                {
                    if (active || idle || sessionSeconds > 0) FlushCurrentSession();
                    active = false; idle = false;
                }

                var stats = database.GetAllTimeStats();
                var today = database.GetTodaySeconds() + sessionSeconds;
                var canvasToday = currentCanvas is null ? 0 : database.GetTodayCanvasSeconds(currentCanvas) + sessionSeconds;
                update = new TrackingUpdate(currentApp ?? "Ready to track", currentCanvas ?? "Switch to a drawing application", active, idle, false, true, sessionSeconds, idleSeconds, today, canvasToday, stats.DailyGoalMinutes, stats.StreakCount, gear);
            }
        }
        SnapshotUpdated?.Invoke(this, update);
    }

    private void FlushCurrentSession()
    {
        if (sessionSeconds >= 3 && currentApp is not null && currentCanvas is not null && sessionStarted is not null) SaveCurrent(DateTime.Now);
        sessionSeconds = 0; idleSeconds = 0; sessionStarted = null; currentApp = null; currentCanvas = null; active = false; idle = false;
    }

    private void SaveCurrent(DateTime end)
    {
        var tags = gear.HasTablet ? $"#{gear.PrimaryTablet.Replace(' ', '_')}" : string.Empty;
        var id = database.AddSession(currentApp!, currentCanvas!, sessionStarted!.Value, end, sessionSeconds, idleSeconds, tags);
        if (id > 0)
        {
            SessionSaved?.Invoke(this, new SessionRecord(id, currentApp!, currentCanvas!, sessionStarted.Value.ToString("O"), end.ToString("O"), sessionSeconds, idleSeconds, sessionStarted.Value.ToString("yyyy-MM-dd"), tags, string.Empty));
        }
    }
}
