namespace TimeFly.Core.Models;

public sealed record TrackingSnapshot(
    string ProcessName,
    string WindowTitle,
    string AppName,
    string CanvasName,
    bool IsSelfApplication,
    bool IsTrackedApplication,
    TimeSpan IdleDuration);
