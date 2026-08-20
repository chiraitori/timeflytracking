using TimeFly.Core.Services;
using TimeFly.Data;

namespace TimeFly.App.Services;

public sealed class AppServices : IDisposable
{
    public TimeFlyDatabase Database { get; } = new();
    public GearDetector GearDetector { get; } = new();
    public UpdateCheckerService UpdateChecker { get; } = new("0.2.0");
    public TrackingEngine Tracker { get; }

    public AppServices()
    {
        Tracker = new TrackingEngine(Database, new ActiveWindowService(), GearDetector);
        Tracker.Start();
    }

    public void Dispose() => Tracker.Dispose();
}
