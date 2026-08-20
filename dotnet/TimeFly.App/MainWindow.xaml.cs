using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System.Runtime.InteropServices;
using TimeFly.App.Services;
using TimeFly.App.Views;
using WinRT.Interop;

namespace TimeFly.App;

public sealed partial class MainWindow : Window
{
    private readonly AppServices services = new();
    private readonly Dictionary<string, Page> pages;
    private readonly IntPtr windowHandle;
    private AppWindow? appWindow;
    private bool allowClose;
    private bool cleanedUp;
    private NativeTrayIcon? trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        pages = new Dictionary<string, Page>
        {
            ["tracker"] = new DashboardPage(services),
            ["analytics"] = new AnalyticsPage(services),
            ["artworks"] = new ArtworksPage(services),
            ["history"] = new HistoryPage(services),
            ["settings"] = new SettingsPage(services)
        };
        Title = "TimeFly";
        ConfigureWindow();
        windowHandle = WindowNative.GetWindowHandle(this);
        trayIcon = new NativeTrayIcon(windowHandle, Path.Combine(AppContext.BaseDirectory, "Assets", "app_icon.ico"), "TimeFly · Digital Art Tracker");
        trayIcon.OpenRequested += RestoreWindow;
        trayIcon.ExitRequested += ExitApplication;

        Navigation.SelectedItem = Navigation.MenuItems[0];
        ContentFrame.Content = pages["tracker"];
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(1120, 760));
        WindowBoundsHelper.EnsureVisible(windowHandle);
        appWindow.Closing += AppWindow_Closing;

        var titleBar = appWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = Colors.White;
        titleBar.ButtonHoverForegroundColor = Colors.White;
        titleBar.ButtonPressedForegroundColor = Colors.White;
        titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 150, 150, 155);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app_icon.ico");
        if (File.Exists(iconPath))
        {
            appWindow.SetIcon(iconPath);
        }
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!allowClose && services.Database.GetBooleanSetting("minimize_to_tray"))
        {
            args.Cancel = true;
            _ = ShowWindow(windowHandle, 0);
            return;
        }
        Cleanup();
    }

    private void ExitApplication()
    {
        allowClose = true;
        Cleanup();
        Close();
    }

    private void RestoreWindow()
    {
        _ = ShowWindow(windowHandle, 9);
        WindowBoundsHelper.EnsureVisible(windowHandle);
        Activate();
        _ = SetForegroundWindow(windowHandle);
    }

    private void Cleanup()
    {
        if (cleanedUp) return;
        cleanedUp = true;
        trayIcon?.Dispose();
        services.Dispose();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Content = pages["settings"];
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        if (item.Tag?.ToString() is { } tag && pages.TryGetValue(tag, out var page)) ContentFrame.Content = page;
    }
}
