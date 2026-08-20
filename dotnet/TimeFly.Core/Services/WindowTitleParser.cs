using System.Text.RegularExpressions;

namespace TimeFly.Core.Services;

public static partial class WindowTitleParser
{
    public static (string AppName, string CanvasName) Parse(string processName, string windowTitle)
    {
        var process = processName.Trim().ToLowerInvariant();
        var title = windowTitle.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            return ("Unknown", "Untitled");
        }

        if (process.Contains("timefly") || title.Contains("TimeFly", StringComparison.OrdinalIgnoreCase))
        {
            return ("TimeFly", "Dashboard");
        }

        if (process.Contains("krita") || title.Contains("Krita", StringComparison.OrdinalIgnoreCase))
        {
            var match = KritaTitleRegex().Match(title);
            if (match.Success)
            {
                return ("Krita", FileName(match.Groups[1].Value));
            }

            return ("Krita", title.Equals("Krita", StringComparison.OrdinalIgnoreCase)
                ? "New / Unsaved Canvas"
                : title.Replace(" - Krita", string.Empty, StringComparison.OrdinalIgnoreCase).Trim());
        }

        if (process.Contains("clipstudio") || title.Contains("CLIP STUDIO", StringComparison.OrdinalIgnoreCase))
        {
            return ("Clip Studio Paint", BeforeSuffix(title, " - CLIP STUDIO PAINT"));
        }

        if (process.Contains("photoshop") || title.Contains("Photoshop", StringComparison.OrdinalIgnoreCase))
        {
            var canvas = title.Split(" @ ", 2)[0];
            return ("Adobe Photoshop", FileName(canvas));
        }

        if (process.Contains("aseprite") || title.Contains("Aseprite", StringComparison.OrdinalIgnoreCase))
        {
            return ("Aseprite", BeforeSuffix(title, " - Aseprite"));
        }

        if (process.Contains("blender") || title.Contains("Blender", StringComparison.OrdinalIgnoreCase))
        {
            return ("Blender", FileName(title));
        }

        var parts = title.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[1], parts[0]) : (processName, title);
    }

    private static string BeforeSuffix(string value, string suffix)
    {
        var index = value.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
        return FileName(index >= 0 ? value[..index] : value);
    }

    private static string FileName(string value) => Path.GetFileName(value.Trim().Trim('[', ']', '*'));

    [GeneratedRegex(@"^(?:\[modified\]\s*)?(.+?)(?:\s*\[\*\])?\s*(?:\([^)]+\))?\s*-\s*Krita$", RegexOptions.IgnoreCase)]
    private static partial Regex KritaTitleRegex();
}

