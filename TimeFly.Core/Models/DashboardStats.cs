namespace TimeFly.Core.Models;

public sealed record DashboardStats(
    long TodaySeconds,
    long AllTimeSeconds,
    int SessionsToday,
    int FocusBlocksToday,
    int ActiveDays,
    int ArtworkCount,
    double FocusRatioToday);

public sealed record RecentSession(
    long Id,
    string AppName,
    string CanvasName,
    string StartTime,
    long DurationSeconds,
    long ElapsedSeconds = 0,
    int FocusBlocks = 1);

