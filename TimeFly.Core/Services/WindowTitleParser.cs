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
            return ("Unknown", "New / Unsaved Canvas");
        }

        if (process.Contains("timefly") || title.Contains("TimeFly", StringComparison.OrdinalIgnoreCase))
        {
            return ("TimeFly", "Dashboard");
        }

        if (process.Contains("krita") || title.Contains("Krita", StringComparison.OrdinalIgnoreCase))
        {
            var clean = CleanKritaTitle(title);
            return ("Krita", clean);
        }

        if (process.Contains("clipstudio") || title.Contains("CLIP STUDIO", StringComparison.OrdinalIgnoreCase))
        {
            return ("Clip Studio Paint", CleanGeneralCanvas(BeforeSuffix(title, " - CLIP STUDIO PAINT")));
        }

        if (process.Contains("photoshop") || title.Contains("Photoshop", StringComparison.OrdinalIgnoreCase))
        {
            var canvas = title.Split(" @ ", 2)[0];
            return ("Adobe Photoshop", CleanGeneralCanvas(canvas));
        }

        if (process.Contains("aseprite") || title.Contains("Aseprite", StringComparison.OrdinalIgnoreCase))
        {
            return ("Aseprite", CleanGeneralCanvas(BeforeSuffix(title, " - Aseprite")));
        }

        if (process.Contains("blender") || title.Contains("Blender", StringComparison.OrdinalIgnoreCase))
        {
            return ("Blender", CleanGeneralCanvas(title));
        }

        var parts = title.Split(" - ", 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2
            ? (parts[1], CleanGeneralCanvas(parts[0]))
            : (processName, CleanGeneralCanvas(title));
    }

    private static string CleanKritaTitle(string title)
    {
        if (title.Equals("Krita", StringComparison.OrdinalIgnoreCase))
        {
            return "New / Unsaved Canvas";
        }

        var clean = title;
        if (clean.StartsWith("Krita - ", StringComparison.OrdinalIgnoreCase))
            clean = clean[8..];
        else if (clean.EndsWith(" - Krita", StringComparison.OrdinalIgnoreCase))
            clean = clean[..^8];

        return CleanGeneralCanvas(clean);
    }

    private static string CleanGeneralCanvas(string value)
    {
        var clean = value.Trim();

        // 1. Remove memory / size patterns: (69,0 MiB), (63.4 MB), (1.2 GiB), (500 KB), etc.
        clean = MemoryRegex().Replace(clean, string.Empty);

        // 2. Remove zoom patterns: @ 100%, (100%), <100%>
        clean = ZoomRegex().Replace(clean, string.Empty);

        // 3. Remove color format info: (RGB/8), (RGBA/8), (CMYK/16), etc.
        clean = ColorSpaceRegex().Replace(clean, string.Empty);

        // 4. Remove dirty/modified tags: [modified], [*], [Not Saved], [*], etc.
        clean = ModifiedTagsRegex().Replace(clean, string.Empty);

        // 5. Clean brackets, asterisks, bullets, extra whitespace
        clean = clean.Trim('[', ']', '(', ')', '<', '>', '*', '•', ' ', '\t', '-');

        // Extract filename if a path exists
        clean = FileName(clean);

        if (string.IsNullOrWhiteSpace(clean) ||
            clean.Equals("Not Saved", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("Unnamed", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("Untitled", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("New Canvas", StringComparison.OrdinalIgnoreCase))
        {
            return "New / Unsaved Canvas";
        }

        return clean;
    }

    private static string BeforeSuffix(string value, string suffix)
    {
        var index = value.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? value[..index] : value;
    }

    private static string FileName(string value)
    {
        try
        {
            var fn = Path.GetFileName(value.Trim());
            return string.IsNullOrWhiteSpace(fn) ? value.Trim() : fn;
        }
        catch
        {
            return value.Trim();
        }
    }

    [GeneratedRegex(@"\s*\(\s*\d+(?:[.,]\d+)?\s*(?:B|KB|KiB|MB|MiB|GB|GiB)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex MemoryRegex();

    [GeneratedRegex(@"\s*(?:@\s*|\(|\<)?\d+(?:[.,]\d+)?%\s*(?:\)|\>)?", RegexOptions.IgnoreCase)]
    private static partial Regex ZoomRegex();

    [GeneratedRegex(@"\s*\((?:RGB|RGBA|CMYK|Grayscale|Indexed|Lab)[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ColorSpaceRegex();

    [GeneratedRegex(@"\[\s*(?:modified|\*|Not Saved|Unnamed|Untitled)\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex ModifiedTagsRegex();
}

