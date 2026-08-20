using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TimeFly.App.Services;
using TimeFly.App.ViewModels;
using TimeFly.Core.Models;
using TimeFly.Core.Services;

namespace TimeFly.App.Views;

public sealed partial class ArtworksPage : Page
{
    private readonly AppServices services;
    private readonly ObservableCollection<ArtworkRow> artworkRows = [];
    private IReadOnlyList<ProjectRecord> projects = [];
    private TrackingUpdate? latestUpdate;
    private bool subscribed;
    private bool showingLive;

    public ArtworksPage(AppServices services)
    {
        this.services = services;
        InitializeComponent();
        ArtworkGrid.ItemsSource = artworkRows;
        Loaded += ArtworksPage_Loaded;
        Unloaded += ArtworksPage_Unloaded;
    }

    private void ArtworksPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed)
        {
            services.Tracker.SnapshotUpdated += Tracker_SnapshotUpdated;
            services.Tracker.SessionSaved += Tracker_SessionSaved;
            subscribed = true;
        }
        Refresh();
    }

    private void ArtworksPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed) return;
        services.Tracker.SnapshotUpdated -= Tracker_SnapshotUpdated;
        services.Tracker.SessionSaved -= Tracker_SessionSaved;
        subscribed = false;
    }

    private void Tracker_SnapshotUpdated(object? sender, TrackingUpdate update) => DispatcherQueue.TryEnqueue(() =>
    {
        latestUpdate = update;
        var isLive = IsLive(update);
        if (!isLive && !showingLive) return;
        showingLive = isLive;
        if (isLive) SyncRows(); else Refresh();
    });

    private void Tracker_SessionSaved(object? sender, SessionRecord session) => DispatcherQueue.TryEnqueue(Refresh);
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (IsLoaded) Refresh(); }

    private void CleanLibrary_Click(object sender, RoutedEventArgs e)
    {
        services.Database.CleanAndConsolidateDatabase();
        Refresh();
    }

    private async void DeleteArtwork_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string canvasName } && !string.IsNullOrWhiteSpace(canvasName))
        {
            var dialog = new ContentDialog
            {
                Title = "Delete Artwork History",
                Content = $"Are you sure you want to delete all tracking history for \"{canvasName}\"? This action cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                services.Database.DeleteProject(canvasName);
                Refresh();
            }
        }
    }

    private void Refresh()
    {
        projects = services.Database.GetProjects(200, SearchBox.Text);
        SyncRows();
    }

    private void SyncRows()
    {
        var desired = projects.Select(x => CreateData(x, latestUpdate)).ToList();
        if (latestUpdate is { } live && IsLive(live) && desired.All(x => !string.Equals(x.Name, live.CanvasName, StringComparison.OrdinalIgnoreCase)) && MatchesSearch(live))
            desired.Insert(0, new ArtworkRowData(live.CanvasName, live.AppName, DurationFormatter.Compact(live.SessionSeconds), "LIVE session", "LIVE · tracking now"));

        for (var index = 0; index < desired.Count; index++)
        {
            var data = desired[index];
            var existingIndex = FindRow(data.Name, data.App, index);
            if (existingIndex < 0) artworkRows.Insert(index, new ArtworkRow(data));
            else
            {
                if (existingIndex != index) artworkRows.Move(existingIndex, index);
                artworkRows[index].Update(data);
            }
        }
        while (artworkRows.Count > desired.Count) artworkRows.RemoveAt(artworkRows.Count - 1);

        EmptyText.Visibility = artworkRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SubtitleText.Text = IsLive(latestUpdate) ? $"LIVE now · {artworkRows.Count} tracked artwork{(artworkRows.Count == 1 ? "" : "s")}" : $"{artworkRows.Count} tracked artwork{(artworkRows.Count == 1 ? "" : "s")}";
    }

    private int FindRow(string name, string app, int start)
    {
        for (var index = start; index < artworkRows.Count; index++)
            if (string.Equals(artworkRows[index].Name, name, StringComparison.OrdinalIgnoreCase) && string.Equals(artworkRows[index].App, app, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }

    private static ArtworkRowData CreateData(ProjectRecord project, TrackingUpdate? update)
    {
        var isLive = IsLive(update) && string.Equals(project.CanvasName, update!.CanvasName, StringComparison.OrdinalIgnoreCase);
        var total = project.TotalDurationSeconds + (isLive ? update!.SessionSeconds : 0);
        var sessions = $"{project.SessionCount} session{(project.SessionCount == 1 ? "" : "s")}" + (isLive ? " · LIVE" : "");
        return new ArtworkRowData(project.CanvasName, project.AppName, DurationFormatter.Compact(total), sessions, isLive ? "LIVE · tracking now" : $"Last modified: {FormatDate(project.LastWorked)}");
    }

    private bool MatchesSearch(TrackingUpdate update)
    {
        var search = SearchBox.Text.Trim();
        return search.Length == 0 || update.CanvasName.Contains(search, StringComparison.OrdinalIgnoreCase) || update.AppName.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLive(TrackingUpdate? update) => update is { IsActive: true } or { IsIdle: true };
    private static string FormatDate(string value) => DateTime.TryParse(value, out var date) ? date.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture) : value;
    private sealed record ArtworkRowData(string Name, string App, string Total, string Sessions, string LastWorked);

    private sealed class ArtworkRow : ObservableObject
    {
        private string name;
        private string app;
        private string total;
        private string sessions;
        private string lastWorked;
        public string Name { get => name; private set => Set(ref name, value); }
        public string App { get => app; private set => Set(ref app, value); }
        public string Total { get => total; private set => Set(ref total, value); }
        public string Sessions { get => sessions; private set => Set(ref sessions, value); }
        public string LastWorked { get => lastWorked; private set => Set(ref lastWorked, value); }
        public ArtworkRow(ArtworkRowData data) { name = data.Name; app = data.App; total = data.Total; sessions = data.Sessions; lastWorked = data.LastWorked; }
        public void Update(ArtworkRowData data) { Name = data.Name; App = data.App; Total = data.Total; Sessions = data.Sessions; LastWorked = data.LastWorked; }
    }
}
