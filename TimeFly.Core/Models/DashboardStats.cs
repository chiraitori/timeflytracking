namespace TimeFly.Core.Models;

public sealed record DashboardStats(
    long TodaySeconds,
    long AllTimeSeconds,
    int SessionsToday,
    int ActiveDays,
    int ArtworkCount);

public sealed record RecentSession(
    long Id,
    string AppName,
    string CanvasName,
    string StartTime,
    long DurationSeconds);

