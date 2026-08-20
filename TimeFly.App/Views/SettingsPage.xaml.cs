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

    private readonly List<string> trackedApps = [];

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

        LoadTrackedApps();

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

    private void LoadTrackedApps()
    {
        trackedApps.Clear();
        try
        {
            var json = services.Database.GetSetting("tracked_apps", "[]");
            var list = JsonSerializer.Deserialize<string[]>(json);
            if (list is { Length: > 0 }) trackedApps.AddRange(list);
            else trackedApps.AddRange(["krita.exe", "CLIPStudioPaint.exe", "Photoshop.exe", "Aseprite.exe", "blender.exe", "sai2.exe"]);
        }
        catch
        {
            trackedApps.AddRange(["krita.exe", "CLIPStudioPaint.exe", "Photoshop.exe", "Aseprite.exe", "blender.exe", "sai2.exe"]);
        }
        RenderTrackedAppChips();
    }

    private void SaveTrackedApps()
    {
        services.Database.SetSetting("tracked_apps", JsonSerializer.Serialize(trackedApps));
        services.Tracker.RefreshSettings();
        RenderTrackedAppChips();
    }

    private void RenderTrackedAppChips()
    {
        TrackedAppsPanel.Children.Clear();
        foreach (var app in trackedApps)
        {
            var chip = new Border
            {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TimeFlyCardBackgroundBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TimeFlyCardBorderBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 4, 6, 4)
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var icon = new FontIcon { Glyph = "\uE790", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TimeFlyAccentBrush"] };
            var text = new TextBlock { Text = app, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            var removeBtn = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 10, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)) },
                Padding = new Thickness(4, 2, 4, 2),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                Tag = app
            };
            removeBtn.Click += (s, e) =>
            {
                if (s is Button { Tag: string targetApp })
                {
                    trackedApps.Remove(targetApp);
                    SaveTrackedApps();
                }
            };

            stack.Children.Add(icon);
            stack.Children.Add(text);
            stack.Children.Add(removeBtn);
            chip.Child = stack;
            TrackedAppsPanel.Children.Add(chip);
        }
    }

    private void AddRunning_Click(object sender, RoutedEventArgs e)
    {
        RunningProcessesFlyout.Items.Clear();
        var procs = GetRunningProcesses();
        if (procs.Count == 0)
        {
            RunningProcessesFlyout.Items.Add(new MenuFlyoutItem { Text = "No active GUI applications detected", IsEnabled = false });
        }
        else
        {
            foreach (var (exeName, title) in procs)
            {
                var already = trackedApps.Contains(exeName, StringComparer.OrdinalIgnoreCase);
                var item = new MenuFlyoutItem
                {
                    Text = $"{title} ({exeName})" + (already ? " (Tracked)" : ""),
                    Tag = exeName,
                    IsEnabled = !already
                };
                item.Click += (s, ev) =>
                {
                    if (s is MenuFlyoutItem { Tag: string exe })
                    {
                        AddTrackedApp(exe);
                    }
                };
                RunningProcessesFlyout.Items.Add(item);
            }
        }
    }

    private static List<(string ExeName, string Title)> GetRunningProcesses()
    {
        var list = new List<(string ExeName, string Title)>();
        var currentProc = Process.GetCurrentProcess();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (proc.Id == currentProc.Id) continue;
                if (!string.IsNullOrWhiteSpace(proc.MainWindowTitle) && proc.MainWindowHandle != IntPtr.Zero)
                {
                    var exeName = $"{proc.ProcessName}.exe";
                    var title = proc.MainWindowTitle.Length > 35 ? proc.MainWindowTitle[..32] + "…" : proc.MainWindowTitle;
                    list.Add((exeName, title));
                }
            }
            catch { }
            finally { proc.Dispose(); }
        }
        return list.DistinctBy(x => x.ExeName, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Title).ToList();
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string exe }) AddTrackedApp(exe);
    }

    private void AddCustomApp_Click(object sender, RoutedEventArgs e) => SubmitCustomApp();

    private void CustomAppBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) SubmitCustomApp();
    }

    private void SubmitCustomApp()
    {
        var text = CustomAppBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) text += ".exe";
        AddTrackedApp(text);
        CustomAppBox.Text = string.Empty;
    }

    private void AddTrackedApp(string exeName)
    {
        if (!trackedApps.Contains(exeName, StringComparer.OrdinalIgnoreCase))
        {
            trackedApps.Add(exeName);
            SaveTrackedApps();
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
            UpdateStatusText.Text = $"✓ You are using the latest version of TimeFly (v{services.UpdateChecker.CurrentVersionString})!";
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
