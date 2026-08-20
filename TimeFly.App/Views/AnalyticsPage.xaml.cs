using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TimeFly.App.Services;
using TimeFly.App.ViewModels;
using TimeFly.Core.Models;
using TimeFly.Core.Services;

namespace TimeFly.App.Views;

public sealed partial class AnalyticsPage : Page
{
    private readonly AppServices services;
    private readonly ObservableCollection<BarRow> dailyRows = [];
    private readonly ObservableCollection<BarRow> projectRows = [];
    private readonly ObservableCollection<HourCell> hourRows = [];
    private AllTimeStats allTime = new(0, 0, 0, 0, 120, 0);
    private IReadOnlyList<DailyStat> daily = [];
    private IReadOnlyList<ProjectRecord> projects = [];
    private IReadOnlyList<long> hourly = [];
    private bool subscribed;
    private bool showingLive;

    public AnalyticsPage(AppServices services)
    {
        this.services = services;
        InitializeComponent();
        DailyList.ItemsSource = dailyRows;
        ProjectsList.ItemsSource = projectRows;
        HourlyGrid.ItemsSource = hourRows;
        Loaded += AnalyticsPage_Loaded;
        Unloaded += AnalyticsPage_Unloaded;
    }

    private void AnalyticsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed)
        {
            services.Tracker.SnapshotUpdated += Tracker_SnapshotUpdated;
            services.Tracker.SessionSaved += Tracker_SessionSaved;
            subscribed = true;
        }
        Refresh();
    }

    private void AnalyticsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed) return;
        services.Tracker.SnapshotUpdated -= Tracker_SnapshotUpdated;
        services.Tracker.SessionSaved -= Tracker_SessionSaved;
        subscribed = false;
    }

    private void Tracker_SnapshotUpdated(object? sender, TrackingUpdate update) => DispatcherQueue.TryEnqueue(() =>
    {
        var isLive = update.IsActive || update.IsIdle;
        if (!isLive && !showingLive) return;
        showingLive = isLive;
        if (isLive) ApplyLiveUpdate(update); else Refresh();
    });
    private void Tracker_SessionSaved(object? sender, SessionRecord session) => DispatcherQueue.TryEnqueue(Refresh);
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        var db = services.Database;
        allTime = db.GetAllTimeStats();
        daily = db.GetDailyStats(7);
        projects = db.GetProjects(7);
        hourly = db.GetHourlyActivity();
        Render(allTime.TotalSeconds, allTime.TotalSessions, allTime.ActiveDays, allTime.StreakCount, daily, projects, hourly);
    }

    private void ApplyLiveUpdate(TrackingUpdate update)
    {
        var hasLive = update.IsActive || update.IsIdle;
        var liveSeconds = hasLive ? update.SessionSeconds : 0;
        var todayKey = DateTime.Today.ToString("yyyy-MM-dd");
        var liveDaily = daily.Select(x => x.Date == todayKey ? x with { TotalSeconds = update.TodaySeconds } : x).ToList();
        var liveProjects = projects.Select(x => string.Equals(x.CanvasName, update.CanvasName, StringComparison.OrdinalIgnoreCase) ? x with { TotalDurationSeconds = x.TotalDurationSeconds + liveSeconds } : x).ToList();
        if (hasLive && liveProjects.All(x => !string.Equals(x.CanvasName, update.CanvasName, StringComparison.OrdinalIgnoreCase)))
            liveProjects.Add(new ProjectRecord(-1, update.CanvasName, update.AppName, liveSeconds, DateTime.Now.ToString("O"), DateTime.Now.ToString("O"), 0, "", "#6366f1"));
        liveProjects = liveProjects.OrderByDescending(x => x.TotalDurationSeconds).Take(7).ToList();
        var liveHourly = hourly.ToArray();
        if (hasLive && liveHourly.Length == 24) liveHourly[DateTime.Now.Hour] += liveSeconds;
        var todayBaseline = daily.FirstOrDefault(x => x.Date == todayKey)?.TotalSeconds ?? 0;
        var activeDays = allTime.ActiveDays + (hasLive && todayBaseline == 0 ? 1 : 0);
        Render(allTime.TotalSeconds + liveSeconds, allTime.TotalSessions + (hasLive && liveSeconds > 0 ? 1 : 0), activeDays, update.StreakCount, liveDaily, liveProjects, liveHourly);
    }

    private void Render(long totalSeconds, int sessionCount, int activeDays, int streakCount, IReadOnlyList<DailyStat> dailyStats, IReadOnlyList<ProjectRecord> projectStats, IReadOnlyList<long> hourlyStats)
    {
        TotalText.Text = DurationFormatter.Compact(totalSeconds);
        SessionCountText.Text = sessionCount.ToString("N0");
        ActiveDaysText.Text = activeDays.ToString("N0");
        StreakText.Text = $"{streakCount} days";

        var dailyMax = Math.Max(1, dailyStats.Count == 0 ? 1 : dailyStats.Max(x => x.TotalSeconds));
        SyncBars(dailyRows, dailyStats.Select(x => new BarData(DateTime.TryParse(x.Date, out var date) ? date.ToString("ddd") : x.Date, DurationFormatter.Compact(x.TotalSeconds), x.TotalSeconds * 100d / dailyMax)).ToList());

        var projectMax = Math.Max(1, projectStats.Count == 0 ? 1 : projectStats.Max(x => x.TotalDurationSeconds));
        SyncBars(projectRows, projectStats.Select(x => new BarData(x.CanvasName, DurationFormatter.Compact(x.TotalDurationSeconds), x.TotalDurationSeconds * 100d / projectMax)).ToList());

        var hourMax = Math.Max(1, hourlyStats.Count == 0 ? 1 : hourlyStats.Max());
        var desiredHours = hourlyStats.Select((seconds, hour) => new HourData($"{hour:00}:00", seconds == 0 ? "—" : DurationFormatter.Compact(seconds), HeatBrush(seconds / (double)hourMax))).ToList();
        for (var index = 0; index < desiredHours.Count; index++)
        {
            if (index >= hourRows.Count) hourRows.Add(new HourCell(desiredHours[index]));
            else hourRows[index].Update(desiredHours[index]);
        }
        while (hourRows.Count > desiredHours.Count) hourRows.RemoveAt(hourRows.Count - 1);
    }

    private static void SyncBars(ObservableCollection<BarRow> target, IReadOnlyList<BarData> desired)
    {
        for (var index = 0; index < desired.Count; index++)
        {
            if (index >= target.Count) target.Add(new BarRow(desired[index]));
            else target[index].Update(desired[index]);
        }
        while (target.Count > desired.Count) target.RemoveAt(target.Count - 1);
    }

    private static SolidColorBrush HeatBrush(double intensity)
    {
        var value = (byte)(35 + Math.Clamp(intensity, 0, 1) * 105); return new SolidColorBrush(Windows.UI.Color.FromArgb(255, (byte)(45 + value / 4), (byte)(40 + value / 5), value));
    }

    private sealed record BarData(string Label, string Duration, double Percent);
    private sealed record HourData(string Hour, string Duration, SolidColorBrush Brush);

    private sealed class BarRow : ObservableObject
    {
        private string label;
        private string duration;
        private double percent;
        public string Label { get => label; private set { if (Set(ref label, value)) Raise(nameof(Name)); } }
        public string Name => Label;
        public string Duration { get => duration; private set => Set(ref duration, value); }
        public double Percent { get => percent; private set => Set(ref percent, value); }
        public BarRow(BarData data) { label = data.Label; duration = data.Duration; percent = data.Percent; }
        public void Update(BarData data) { Label = data.Label; Duration = data.Duration; Percent = data.Percent; }
    }

    private sealed class HourCell : ObservableObject
    {
        private string hour;
        private string duration;
        private SolidColorBrush brush;
        public string Hour { get => hour; private set => Set(ref hour, value); }
        public string Duration { get => duration; private set => Set(ref duration, value); }
        public SolidColorBrush Brush { get => brush; private set => Set(ref brush, value); }
        public HourCell(HourData data) { hour = data.Hour; duration = data.Duration; brush = data.Brush; }
        public void Update(HourData data) { Hour = data.Hour; Duration = data.Duration; Brush = data.Brush; }
    }
}
