using System.Runtime.InteropServices;

namespace TimeFly.App.Services;

public sealed class NativeTrayIcon : IDisposable
{
    private const int GwlpWndProc = -4;
    private const uint WmApp = 0x8000;
    private const uint CallbackMessage = WmApp + 128;
    private const uint WmLeftButtonDoubleClick = 0x0203;
    private const uint WmRightButtonUp = 0x0205;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint ImageIcon = 1;
    private const uint LoadFromFile = 0x00000010;
    private const uint MfString = 0;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private readonly IntPtr windowHandle;
    private readonly WindowProcedure windowProcedure;
    private readonly IntPtr originalProcedure;
    private NotifyIconData data;
    private bool disposed;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public NativeTrayIcon(IntPtr windowHandle, string iconPath, string tooltip)
    {
        this.windowHandle = windowHandle;
        windowProcedure = WindowProc;
        originalProcedure = SetWindowLongPtr(windowHandle, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(windowProcedure));
        var icon = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LoadFromFile);
        data = new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(), WindowHandle = windowHandle, Id = 1,
            Flags = NifMessage | NifIcon | NifTip, CallbackMessage = CallbackMessage,
            IconHandle = icon, ToolTip = tooltip
        };
        _ = ShellNotifyIcon(NimAdd, ref data);
    }

    public void Dispose()
    {
        if (disposed) return; disposed = true;
        _ = ShellNotifyIcon(NimDelete, ref data);
        if (originalProcedure != IntPtr.Zero) _ = SetWindowLongPtr(windowHandle, GwlpWndProc, originalProcedure);
        if (data.IconHandle != IntPtr.Zero) _ = DestroyIcon(data.IconHandle);
    }

    private IntPtr WindowProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == CallbackMessage)
        {
            var mouseMessage = unchecked((uint)lParam.ToInt64());
            if (mouseMessage == WmLeftButtonDoubleClick) OpenRequested?.Invoke();
            else if (mouseMessage == WmRightButtonUp) ShowMenu();
        }
        return CallWindowProc(originalProcedure, handle, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        _ = AppendMenu(menu, MfString, 1, "Open TimeFly");
        _ = AppendMenu(menu, MfSeparator, 0, string.Empty);
        _ = AppendMenu(menu, MfString, 2, "Exit");
        _ = GetCursorPos(out var point);
        _ = SetForegroundWindow(windowHandle);
        var command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCommand, point.X, point.Y, 0, windowHandle, IntPtr.Zero);
        _ = DestroyMenu(menu);
        if (command == 1) OpenRequested?.Invoke(); else if (command == 2) ExitRequested?.Invoke();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size; public IntPtr WindowHandle; public uint Id; public uint Flags; public uint CallbackMessage; public IntPtr IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string ToolTip;
        public uint State; public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid GuidItem; public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    private delegate IntPtr WindowProcedure(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadImageW")] private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint load);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr previous, IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr window, IntPtr rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr window);
}
