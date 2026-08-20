using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using TimeFly.App.Services;

namespace TimeFly.App;

public partial class App : Application
{
    private static Mutex? instanceMutex;
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        UnhandledException += (_, eventArgs) => WriteStartupError(eventArgs.Exception);
        try
        {
            InitializeComponent();
        }
        catch (Exception exception)
        {
            WriteStartupError(exception);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        instanceMutex = new Mutex(true, @"Local\TimeFly.WinUI3.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            NativeMethods.RestoreExistingWindow();
            Exit();
            return;
        }

        try
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
        catch (Exception exception)
        {
            WriteStartupError(exception);
            throw;
        }
    }

    private static void WriteStartupError(Exception exception)
    {
        var path = Path.Combine(Path.GetTempPath(), "TimeFly.WinUI.startup.log");
        File.WriteAllText(path, exception.ToString());
    }

    private static partial class NativeMethods
    {
        private const int Restore = 9;

        public static void RestoreExistingWindow()
        {
            var window = FindWindow(null, "TimeFly");
            if (window == IntPtr.Zero)
            {
                return;
            }

            _ = ShowWindow(window, Restore);
            WindowBoundsHelper.EnsureVisible(window);
            _ = SetForegroundWindow(window);
        }

        [LibraryImport("user32.dll", EntryPoint = "FindWindowW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr FindWindow(string? className, string windowName);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr window, int command);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(IntPtr window);
    }
}
