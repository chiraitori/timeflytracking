using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TimeFly.App.Services;
using TimeFly.App.ViewModels;
using TimeFly.Core.Models;
using TimeFly.Core.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace TimeFly.App.Views;

public sealed partial class HistoryPage : Page
{
    private readonly AppServices services;
    private readonly ObservableCollection<SessionRow> sessionRows = [];
    private IReadOnlyList<SessionRecord> sessions = [];
    private TrackingUpdate? latestUpdate;
    private bool subscribed;
    private bool showingLive;

    public HistoryPage(AppServices services)
    {
        this.services = services;
        InitializeComponent();
        SessionsList.ItemsSource = sessionRows;
        DateFilter.SelectedIndex = 0;
        Loaded += HistoryPage_Loaded;
        Unloaded += HistoryPage_Unloaded;
    }

    private void HistoryPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed)
        {
            services.Tracker.SnapshotUpdated += Tracker_SnapshotUpdated;
            services.Tracker.SessionSaved += Tracker_SessionSaved;
            subscribed = true;
        }
        Refresh();
    }

    private void HistoryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed) return;
        services.Tracker.SnapshotUpdated -= Tracker_SnapshotUpdated;
        services.Tracker.SessionSaved -= Tracker_SessionSaved;
        subscribed = false;
    }

    private void Tracker_SnapshotUpdated(object? sender, TrackingUpdate update) => DispatcherQueue.TryEnqueue(() =>
    {
        latestUpdate = update;
        var isLive = update.IsActive || update.IsIdle;
        if (!isLive && !showingLive) return;
        showingLive = isLive;
        if (isLive) SyncRows(); else Refresh();
    });

    private void Tracker_SessionSaved(object? sender, SessionRecord session) => DispatcherQueue.TryEnqueue(Refresh);
    private void Filter_Changed(object sender, object e) { if (IsLoaded) Refresh(); }

    private void Refresh()
    {
        DateTime? from = DateFilter.SelectedIndex switch { 1 => DateTime.Today, 2 => DateTime.Today.AddDays(-6), 3 => DateTime.Today.AddDays(-29), _ => null };
        DateTime? to = DateFilter.SelectedIndex == 1 ? DateTime.Today : null;
        sessions = services.Database.GetSessions(500, search: SearchBox.Text, fromDate: from, toDate: to);
        SyncRows();
    }

    private void SyncRows()
    {
        var desired = new List<SessionRowData>(sessions.Count + 1);
        if (latestUpdate is { } live && ShouldShowLive(live))
        {
            var state = live.IsIdle ? "IDLE" : "LIVE";
            desired.Add(new SessionRowData("live", state, null, live.AppName, live.CanvasName, DurationFormatter.Compact(live.SessionSeconds), $"Now · {state}", live.IsIdle ? "Waiting for input" : "Tracking now"));
        }
        desired.AddRange(sessions.Select(x => new SessionRowData($"db:{x.Id}", x.Id.ToString(), x.Id, x.AppName, x.CanvasName, DurationFormatter.Compact(x.DurationSeconds), FormatDate(x.StartTime), string.Join(" · ", new[] { x.Tags, x.Notes }.Where(v => !string.IsNullOrWhiteSpace(v))))));

        for (var index = 0; index < desired.Count; index++)
        {
            var data = desired[index];
            var existingIndex = FindRow(data.Key, index);
            if (existingIndex < 0) sessionRows.Insert(index, new SessionRow(data));
            else
            {
                if (existingIndex != index) sessionRows.Move(existingIndex, index);
                sessionRows[index].Update(data);
            }
        }
        while (sessionRows.Count > desired.Count) sessionRows.RemoveAt(sessionRows.Count - 1);

        var hasLive = desired.Count > sessions.Count;
        CountText.Text = hasLive ? $"LIVE now · {sessions.Count} saved session{(sessions.Count == 1 ? "" : "s")}" : $"{sessions.Count} recorded session{(sessions.Count == 1 ? "" : "s")}";
        EmptyText.Visibility = sessionRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private int FindRow(string key, int start)
    {
        for (var index = start; index < sessionRows.Count; index++) if (sessionRows[index].Key == key) return index;
        return -1;
    }

    private bool ShouldShowLive(TrackingUpdate update)
    {
        if ((!update.IsActive && !update.IsIdle) || string.IsNullOrWhiteSpace(update.CanvasName)) return false;
        var search = SearchBox.Text.Trim();
        return search.Length == 0 || update.CanvasName.Contains(search, StringComparison.OrdinalIgnoreCase) || update.AppName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SessionsList.SelectedItem is not SessionRow row) { ShowMessage("Choose a session first", "Select the row you want to delete.", InfoBarSeverity.Warning); return; }
        if (row.DatabaseId is not long id) { ShowMessage("Session is live", "Pause tracking or switch apps before editing the active session.", InfoBarSeverity.Informational); return; }
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Delete session?", Content = $"Session #{id} for {row.Canvas} will be removed from totals.", PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && services.Database.DeleteSession(id)) { Refresh(); ShowMessage("Session deleted", $"Session #{id} was removed.", InfoBarSeverity.Success); }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SessionsList.SelectedItem is not SessionRow row) { ShowMessage("Choose a session first", "Select a row to edit tags and notes.", InfoBarSeverity.Warning); return; }
        if (row.DatabaseId is not long id) { ShowMessage("Session is live", "Pause tracking or switch apps before editing the active session.", InfoBarSeverity.Informational); return; }
        var source = sessions.First(x => x.Id == id); var tags = new TextBox { Header = "Tags", Text = source.Tags }; var notes = new TextBox { Header = "Notes", Text = source.Notes, AcceptsReturn = true, Height = 100, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 12 }; panel.Children.Add(tags); panel.Children.Add(notes);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = $"Edit session #{id}", Content = panel, PrimaryButtonText = "Save", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) { services.Database.UpdateSessionNotes(id, notes.Text.Trim(), tags.Text.Trim()); Refresh(); ShowMessage("Changes saved", "Tags and notes updated.", InfoBarSeverity.Success); }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker { SuggestedFileName = $"timefly_sessions_{DateTime.Today:yyyy-MM-dd}" }; picker.FileTypeChoices.Add("CSV file", [".csv"]);
        var window = App.MainWindow; if (window is null) return; InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        var file = await picker.PickSaveFileAsync(); if (file is null) return;
        try { var ok = services.Database.ExportToCsv(file.Path); ShowMessage(ok ? "Export complete" : "Nothing to export", ok ? file.Path : "No sessions were found.", ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning); }
        catch (Exception ex) { ShowMessage("Export failed", ex.Message, InfoBarSeverity.Error); }
    }

    private void ShowMessage(string title, string message, InfoBarSeverity severity) { MessageBar.Title = title; MessageBar.Message = message; MessageBar.Severity = severity; MessageBar.IsOpen = true; }
    private static string FormatDate(string value) => DateTime.TryParse(value, out var date) ? date.ToString("yyyy-MM-dd HH:mm") : value;
    private sealed record SessionRowData(string Key, string Id, long? DatabaseId, string App, string Canvas, string Duration, string Started, string Metadata);

    private sealed class SessionRow : ObservableObject
    {
        private string id;
        private string app;
        private string canvas;
        private string duration;
        private string started;
        private string metadata;
        public string Key { get; }
        public long? DatabaseId { get; }
        public string Id { get => id; private set => Set(ref id, value); }
        public string App { get => app; private set => Set(ref app, value); }
        public string Canvas { get => canvas; private set => Set(ref canvas, value); }
        public string Duration { get => duration; private set => Set(ref duration, value); }
        public string Started { get => started; private set => Set(ref started, value); }
        public string Metadata { get => metadata; private set => Set(ref metadata, value); }
        public SessionRow(SessionRowData data) { Key = data.Key; DatabaseId = data.DatabaseId; id = data.Id; app = data.App; canvas = data.Canvas; duration = data.Duration; started = data.Started; metadata = data.Metadata; }
        public void Update(SessionRowData data) { Id = data.Id; App = data.App; Canvas = data.Canvas; Duration = data.Duration; Started = data.Started; Metadata = data.Metadata; }
    }
}
