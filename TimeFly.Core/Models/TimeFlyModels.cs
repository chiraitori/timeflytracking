namespace TimeFly.Core.Models;

public sealed record SessionRecord(long Id, string AppName, string CanvasName, string StartTime, string EndTime, long DurationSeconds, long IdleSeconds, string Date, string Tags, string Notes);
public sealed record ProjectRecord(long Id, string CanvasName, string AppName, long TotalDurationSeconds, string FirstWorked, string LastWorked, int SessionCount, string Tags, string ColorTag);
public sealed record DailyStat(string Date, long TotalSeconds, long IdleSeconds, int SessionCount);
public sealed record AllTimeStats(long TotalSeconds, int TotalSessions, int ActiveDays, int ArtworkCount, int DailyGoalMinutes, int StreakCount);
public sealed record TabletDriver(string Brand, string ProcessName, int ProcessId, bool IsRunning);
public sealed record GearInfo(string PrimaryTablet, string Manufacturer, int MaxPressure, bool HasTablet, bool HasStylus, TabletDriver Driver, int SupportedTabletCount);
public sealed record TrackingUpdate(string AppName, string CanvasName, bool IsActive, bool IsIdle, bool IsPaused, bool IsEnabled, long SessionSeconds, long IdleSeconds, long TodaySeconds, long TodayCanvasSeconds, int DailyGoalMinutes, int StreakCount, GearInfo Gear);
