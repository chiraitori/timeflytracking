using System.Runtime.InteropServices;

namespace TimeFly.App.Services;

internal static class WindowBoundsHelper
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint NoSize = 0x0001;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;

    public static void EnsureVisible(IntPtr window)
    {
        if (window == IntPtr.Zero || IsZoomed(window) || !GetWindowRect(window, out var bounds)) return;
        var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo)) return;

        var work = monitorInfo.WorkArea;
        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        const int reachableWidth = 160;
        const int reachableHeight = 48;
        var minX = work.Left - Math.Max(0, width - reachableWidth);
        var maxX = work.Right - Math.Min(width, reachableWidth);
        var maxY = work.Bottom - Math.Min(height, reachableHeight);
        var x = Math.Clamp(bounds.Left, minX, maxX);
        var y = Math.Clamp(bounds.Top, work.Top, maxY);
        if (x != bounds.Left || y != bounds.Top)
            _ = SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, NoSize | NoZOrder | NoActivate);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rect bounds);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
