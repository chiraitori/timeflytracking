using TimeFly.Core.Services;

namespace TimeFly.Tests;

public sealed class WindowTitleParserTests
{
    [Theory]
    [InlineData("krita", "[modified] portrait.kra - Krita", "Krita", "portrait.kra")]
    [InlineData("krita", "Krita", "Krita", "New / Unsaved Canvas")]
    [InlineData("CLIPStudioPaint", "page.clip - CLIP STUDIO PAINT", "Clip Studio Paint", "page.clip")]
    [InlineData("Photoshop", "painting.psd @ 50% (RGB/8) - Adobe Photoshop", "Adobe Photoshop", "painting.psd")]
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

