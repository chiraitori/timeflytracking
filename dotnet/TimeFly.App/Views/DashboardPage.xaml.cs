using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TimeFly.App.Services;
using TimeFly.Core.Models;
using TimeFly.Core.Services;

namespace TimeFly.App.Views;

public sealed partial class DashboardPage : Page
{
    private readonly AppServices services;
    private bool subscribed;

    public DashboardPage(AppServices services)
    {
        this.services = services; InitializeComponent(); DateText.Text = DateTime.Today.ToString("dddd, MMMM d, yyyy");
        Loaded += DashboardPage_Loaded; Unloaded += DashboardPage_Unloaded;
    }

    private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed) { services.Tracker.SnapshotUpdated += Tracker_SnapshotUpdated; services.Tracker.SessionSaved += Tracker_SessionSaved; subscribed = true; }
        PauseButtonText.Text = services.Tracker.IsPaused ? "Resume" : "Pause"; LoadDashboard();
    }

    private void DashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed) return; services.Tracker.SnapshotUpdated -= Tracker_SnapshotUpdated; services.Tracker.SessionSaved -= Tracker_SessionSaved; subscribed = false;
    }

    private void Tracker_SnapshotUpdated(object? sender, TrackingUpdate update) => DispatcherQueue.TryEnqueue(() => ApplyUpdate(update));
    private void Tracker_SessionSaved(object? sender, SessionRecord e) => DispatcherQueue.TryEnqueue(LoadDashboard);

    private void ApplyUpdate(TrackingUpdate x)
    {
        TrackingStatusText.Text = x.AppName; ActiveCanvasText.Text = x.CanvasName; LiveTimerText.Text = TimeSpan.FromSeconds(x.SessionSeconds).ToString(@"hh\:mm\:ss");
        CanvasTodayText.Text = $"{DurationFormatter.Compact(x.TodayCanvasSeconds)} on this artwork today";
        StatusPillText.Text = !x.IsEnabled ? "OFF" : x.IsPaused ? "PAUSED" : x.IsIdle ? "IDLE" : x.IsActive ? "DRAWING" : "STANDBY";
        PauseButtonText.Text = x.IsPaused ? "Resume" : "Pause";
        PauseButton.IsEnabled = x.IsEnabled;
        var goalSeconds = Math.Max(60, x.DailyGoalMinutes * 60L); var percent = Math.Clamp(x.TodaySeconds * 100d / goalSeconds, 0, 100);
        GoalProgress.Value = percent; GoalPercentText.Text = $"{percent:0}%"; TodayTimeText.Text = $"{DurationFormatter.Compact(x.TodaySeconds)} of {DurationFormatter.Compact(goalSeconds)}";
        StreakText.Text = $"{x.StreakCount} day{(x.StreakCount == 1 ? "" : "s")}";
        TabletNameText.Text = x.Gear.PrimaryTablet; TabletDetailsText.Text = x.Gear.HasTablet ? $"{x.Gear.Manufacturer} · {x.Gear.MaxPressure:N0} pressure levels · {x.Gear.Driver.ProcessName}" : x.Gear.Driver.IsRunning ? $"No physical tablet present · {x.Gear.Driver.ProcessName} driver is still running" : "No physical drawing tablet detected";
        TabletStateText.Text = x.Gear.HasTablet ? "CONNECTED" : "NOT FOUND"; TabletStateText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(x.Gear.HasTablet ? Windows.UI.Color.FromArgb(255, 101, 214, 173) : Windows.UI.Color.FromArgb(255, 248, 197, 85));
    }

    private void LoadDashboard()
    {
        var db = services.Database; var stats = db.GetDashboardStats(); SessionsText.Text = stats.SessionsToday.ToString(); AllTimeText.Text = $"{DurationFormatter.Compact(stats.AllTimeSeconds)} all time";
        var sessions = db.GetSessions(20, fromDate: DateTime.Today, toDate: DateTime.Today).Select(x => new SessionRow(x.CanvasName, $"{x.AppName} · {FormatTimestamp(x.StartTime)} · {(string.IsNullOrWhiteSpace(x.Tags) ? "No tags" : x.Tags)}", DurationFormatter.Compact(x.DurationSeconds))).ToList();
        RecentSessionsList.ItemsSource = sessions; EmptySessionsText.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) => PauseButtonText.Text = services.Tracker.TogglePause() ? "Resume" : "Pause";
    private void RefreshButton_Click(object sender, RoutedEventArgs e) => LoadDashboard();

    private async void ManualButton_Click(object sender, RoutedEventArgs e)
    {
        var app = new ComboBox { Header = "Software / medium", ItemsSource = new[] { "Krita", "Clip Studio Paint", "Photoshop", "Paper / Traditional", "Aseprite", "Blender" }, SelectedIndex = 0 };
        var canvas = new TextBox { Header = "Canvas / project", PlaceholderText = "character_design.kra or Sketchbook #4" };
        var duration = new NumberBox { Header = "Duration (minutes)", Minimum = 1, Maximum = 1440, Value = 60, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var tags = new TextBox { Header = "Tags", PlaceholderText = "#sketch, #lineart" };
        var content = new StackPanel { Spacing = 12 }; content.Children.Add(app); content.Children.Add(canvas); content.Children.Add(duration); content.Children.Add(tags);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Log drawing session", Content = content, PrimaryButtonText = "Save session", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        var now = DateTime.Now; var seconds = Math.Max(1, (long)duration.Value) * 60;
        services.Database.AddSession(app.SelectedItem?.ToString() ?? "Krita", string.IsNullOrWhiteSpace(canvas.Text) ? "Manual Artwork" : canvas.Text.Trim(), now.AddSeconds(-seconds), now, seconds, tags: tags.Text.Trim(), notes: "Manually logged session"); LoadDashboard();
    }

    private static string FormatTimestamp(string value) => DateTime.TryParse(value, out var timestamp) ? timestamp.ToString("HH:mm") : value;
    private sealed record SessionRow(string CanvasName, string Details, string Duration);
}
