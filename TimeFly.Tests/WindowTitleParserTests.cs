using TimeFly.Core.Services;

namespace TimeFly.Tests;

public sealed class WindowTitleParserTests
{
    [Theory]
    [InlineData("krita", "[Not Saved] (69,0 MiB) - Krita", "Krita", "New / Unsaved Canvas")]
    [InlineData("krita", "[Not Saved] (63.4 MiB) - Krita", "Krita", "New / Unsaved Canvas")]
    [InlineData("krita", "Krita - [Not Saved] (84,9 MiB)", "Krita", "New / Unsaved Canvas")]
    [InlineData("krita", "Krita - [Unnamed] (57,8 MiB)", "Krita", "New / Unsaved Canvas")]
    [InlineData("krita", "character_sketch.kra (69,0 MiB) - Krita", "Krita", "character_sketch.kra")]
    [InlineData("krita", "character_sketch.kra [*] (84.9 MB) - Krita", "Krita", "character_sketch.kra")]
    [InlineData("krita", "[modified] portrait.kra @ 50% (RGBA/8) - Krita", "Krita", "portrait.kra")]
    [InlineData("krita", "Krita", "Krita", "New / Unsaved Canvas")]
    [InlineData("CLIPStudioPaint", "page.clip* (100%) - CLIP STUDIO PAINT", "Clip Studio Paint", "page.clip")]
    [InlineData("Photoshop", "painting.psd @ 50% (RGB/8#*) * - Adobe Photoshop", "Adobe Photoshop", "painting.psd")]
    [InlineData("Aseprite", "hero_idle.aseprite <100%> - Aseprite", "Aseprite", "hero_idle.aseprite")]
    [InlineData("Blender", "Blender [D:\\projects\\sculpt.blend]", "Blender", "sculpt.blend")]
    public void Parse_recognizes_supported_drawing_apps(
        string process,
        string title,
        string expectedApp,
        string expectedCanvas)
    {
        var result = WindowTitleParser.Parse(process, title);

        Assert.Equal(expectedApp, result.AppName);
        Assert.Equal(expectedCanvas, result.CanvasName);
    }
}

