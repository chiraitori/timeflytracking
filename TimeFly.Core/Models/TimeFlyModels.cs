namespace TimeFly.Core.Models;

public sealed record SessionRecord(
    long Id,
    string AppName,
    string CanvasName,
    string StartTime,
    string EndTime,
    long DurationSeconds,
    long IdleSeconds,
    long ElapsedSeconds,
    int FocusBlocks,
    string Date,
    string Tags,
    string Notes)
{
    public double FocusRatio => ElapsedSeconds > 0 ? Math.Clamp((double)DurationSeconds / ElapsedSeconds * 100d, 0, 100) : 100d;
}

public sealed record ProjectRecord(
    long Id,
    string CanvasName,
    string AppName,
    long TotalDurationSeconds,
    long TotalElapsedSeconds,
    string FirstWorked,
    string LastWorked,
    int SessionCount,
    int FocusBlocksCount,
    string Tags,
    string ColorTag,
    bool IsUnsaved = false)
{
    public double FocusRatio => TotalElapsedSeconds > 0 ? Math.Clamp((double)TotalDurationSeconds / TotalElapsedSeconds * 100d, 0, 100) : 100d;
}

public sealed record DailyStat(string Date, long TotalSeconds, long IdleSeconds, int SessionCount, int FocusBlocks = 0);
public sealed record AllTimeStats(long TotalSeconds, int TotalSessions, int TotalFocusBlocks, int ActiveDays, int ArtworkCount, int DailyGoalMinutes, int StreakCount);
public sealed record TabletDriver(string Brand, string ProcessName, int ProcessId, bool IsRunning);
public sealed record GearInfo(string PrimaryTablet, string Manufacturer, int MaxPressure, bool HasTablet, bool HasStylus, TabletDriver Driver, int SupportedTabletCount);
public sealed record TrackingUpdate(
    string AppName,
    string CanvasName,
    bool IsActive,
    bool IsIdle,
    bool IsPaused,
    bool IsEnabled,
    long SessionSeconds,
    long IdleSeconds,
    long ElapsedSeconds,
    int FocusBlocks,
    long TodaySeconds,
    long TodayCanvasSeconds,
    int DailyGoalMinutes,
    int StreakCount,
    GearInfo Gear)
{
    public double LiveFocusRatio => ElapsedSeconds > 0 ? Math.Clamp((double)SessionSeconds / ElapsedSeconds * 100d, 0, 100) : 100d;
}
