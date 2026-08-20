using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TimeFly.App.Services;

namespace TimeFly.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly AppServices services;
    private bool isLoaded;

    public SettingsPage(AppServices services)
    {
        this.services = services;
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        isLoaded = false;
        var db = services.Database;
        GoalBox.Value = db.GetDailyGoalMinutes();
        IdleBox.Value = int.TryParse(db.GetSetting("idle_timeout_min", "3"), out var idle) ? idle : 3;

        try
        {
            var apps = JsonSerializer.Deserialize<string[]>(db.GetSetting("tracked_apps", "[]")) ?? [];
            AppsBox.Text = apps.Length > 0 ? string.Join(", ", apps) : "krita.exe, CLIPStudioPaint.exe, Photoshop.exe, Aseprite.exe, blender.exe, sai2.exe";
        }
        catch
        {
            AppsBox.Text = "krita.exe, CLIPStudioPaint.exe, Photoshop.exe, Aseprite.exe, blender.exe, sai2.exe";
        }

        TrayToggle.IsOn = db.GetBooleanSetting("minimize_to_tray");
        DatabasePathText.Text = $"SQLite Database: {db.DatabasePath}";
        isLoaded = true;

        ScanGear();
    }

    private void GoalBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!isLoaded || double.IsNaN(args.NewValue)) return;
        services.Database.SetDailyGoalMinutes((int)args.NewValue);
    }

    private void IdleBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!isLoaded || double.IsNaN(args.NewValue)) return;
        services.Database.SetSetting("idle_timeout_min", ((int)args.NewValue).ToString());
        services.Tracker.RefreshSettings();
    }

    private void AppsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!isLoaded) return;
        var apps = AppsBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (apps.Length > 0)
        {
            services.Database.SetSetting("tracked_apps", JsonSerializer.Serialize(apps));
            services.Tracker.RefreshSettings();
        }
    }

    private void TrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!isLoaded) return;
        services.Database.SetSetting("minimize_to_tray", TrayToggle.IsOn.ToString().ToLowerInvariant());
    }

    private async void SyncDb_Click(object sender, RoutedEventArgs e)
    {
        SyncButtonText.Text = "Syncing...";
        var result = await services.GearDetector.SyncOnlineAsync();
        ScanGear();
        SyncButtonText.Text = result.Success ? "Synced!" : "Failed";
        await Task.Delay(2000);
        SyncButtonText.Text = "Sync DB";
    }

    private void Rescan_Click(object sender, RoutedEventArgs e) => ScanGear();

    private async void ScanGear()
    {
        TabletModelText.Text = "Scanning connected tablet...";
        TabletStylusText.Text = "Scanning stylus digitizer...";
        TabletDriverText.Text = "Scanning driver process...";
        TabletDatabaseText.Text = "Loading OpenTabletDriver dataset...";

        var gear = await Task.Run(services.GearDetector.Scan);

        var hasStylusText = gear.HasStylus ? "Yes (HID Digitizer Active)" : "Standard Pointer";
        var driverProcess = gear.Driver.IsRunning ? $"{gear.Driver.ProcessName}.exe ({gear.Driver.Brand})" : "Not running";

        TabletModelText.Text = gear.PrimaryTablet;
        TabletStylusText.Text = $"{hasStylusText} · {gear.MaxPressure:N0} mức lực nhấn";
        TabletDriverText.Text = driverProcess;
        TabletDatabaseText.Text = $"OpenTabletDriver Engine ({gear.SupportedTabletCount:N0} dòng bảng vẽ hỗ trợ)";
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtnText.Text = "Checking...";
        UpdateStatusText.Visibility = Visibility.Visible;
        UpdateStatusText.Text = "Checking for latest release on GitHub...";

        var result = await Task.Run(services.UpdateChecker.CheckForUpdatesAsync);
        CheckUpdateBtnText.Text = "Check for Updates";

        if (result.IsUpdateAvailable)
        {
            UpdateStatusText.Text = $"🎉 New update available: {result.TagName}! Click 'GitHub Releases' to download.";
            UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 101, 214, 173));

            var dialog = new ContentDialog
            {
                Title = $"New Update Available: {result.TagName}",
                Content = $"A new version of TimeFly ({result.TagName}) is available on GitHub with new features and improvements!\n\nWould you like to open the download page?",
                PrimaryButtonText = "Download Update",
                CloseButtonText = "Later",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(result.ReleaseUrl));
            }
        }
        else if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            UpdateStatusText.Text = $"Could not check for updates: {result.ErrorMessage}";
            UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 248, 113, 113));
        }
        else
        {
            UpdateStatusText.Text = $"✓ You are using the latest version of TimeFly (v0.1.0)!";
            UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 101, 214, 173));
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo { FileName = Path.GetDirectoryName(services.Database.DatabasePath)!, UseShellExecute = true });

    private async void GitHub_Click(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/chiraitori/timeflytracking/releases"));

    private async void Author_Click(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/chiraitori"));

    private async void ArtworkCredit_Click(object sender, RoutedEventArgs e) =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.pixiv.net/en/artworks/148639751"));
}
