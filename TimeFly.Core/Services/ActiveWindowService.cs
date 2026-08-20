using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using TimeFly.Core.Models;

namespace TimeFly.Core.Services;

public sealed class ActiveWindowService
{
    private static readonly string[] DefaultProcesses = ["krita", "CLIPStudioPaint", "Photoshop", "Aseprite", "blender", "sai2"];

    public TrackingSnapshot Capture(IEnumerable<string>? trackedProcesses = null)
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return EmptySnapshot();
        }

        var length = GetWindowTextLength(handle);
        var titleBuffer = new StringBuilder(length + 1);
        _ = GetWindowText(handle, titleBuffer, titleBuffer.Capacity);
        _ = GetWindowThreadProcessId(handle, out var processId);

        var processName = "Unknown";
        try
        {
            processName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            // The focused process can exit between the Win32 calls.
        }

        var title = titleBuffer.ToString();
        var parsed = WindowTitleParser.Parse(processName, title);
        var tracked = new HashSet<string>((trackedProcesses ?? DefaultProcesses).Select(Normalize), StringComparer.OrdinalIgnoreCase);
        var isSelf = processId == Environment.ProcessId || title.Contains("TimeFly", StringComparison.OrdinalIgnoreCase);
        return new TrackingSnapshot(
            processName,
            title,
            parsed.AppName,
            parsed.CanvasName,
            isSelf,
            !isSelf && (tracked.Contains(Normalize(processName)) || processName.Contains("krita", StringComparison.OrdinalIgnoreCase)),
            GetIdleDuration());
    }

    private static TrackingSnapshot EmptySnapshot() =>
        new("Unknown", string.Empty, "Unknown", "Untitled", false, false, GetIdleDuration());

    private static TimeSpan GetIdleDuration()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info)
            ? TimeSpan.FromMilliseconds(GetTickCount64() - info.Time)
            : TimeSpan.Zero;
    }

    private static string Normalize(string value) => Path.GetFileNameWithoutExtension(value.Trim());

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr windowHandle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();
}
