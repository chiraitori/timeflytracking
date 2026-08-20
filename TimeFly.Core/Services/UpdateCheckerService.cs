using System.Text.Json;

namespace TimeFly.Core.Services;

public sealed class UpdateCheckerService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/chiraitori/timeflytracking/releases/latest";
    private readonly Version currentVersion;

    public string CurrentVersionString { get; }

    public UpdateCheckerService(string currentVersionString = "0.1.0")
    {
        CurrentVersionString = currentVersionString;
        if (!Version.TryParse(NormalizeVersion(currentVersionString), out var v))
        {
            v = new Version(0, 1, 0);
        }
        currentVersion = v;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TimeFly-ArtTracker");

            var json = await client.GetStringAsync(GitHubApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
            var releaseName = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? tagName : tagName;
            var htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "https://github.com/chiraitori/timeflytracking/releases" : "https://github.com/chiraitori/timeflytracking/releases";
            var body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";

            var cleanTag = NormalizeVersion(tagName);
            if (Version.TryParse(cleanTag, out var latestVer) && latestVer > currentVersion)
            {
                return new UpdateCheckResult(true, tagName, releaseName, htmlUrl, body, null);
            }

            return new UpdateCheckResult(false, tagName, releaseName, htmlUrl, body, null);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, "", "", "", "", ex.Message);
        }
    }

    private static string NormalizeVersion(string raw)
    {
        var clean = raw.Trim().TrimStart('v', 'V');
        var parts = clean.Split('.');
        if (parts.Length == 1) return $"{parts[0]}.0.0";
        if (parts.Length == 2) return $"{parts[0]}.{parts[1]}.0";
        return clean;
    }
}

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string TagName,
    string ReleaseName,
    string ReleaseUrl,
    string ReleaseNotes,
    string? ErrorMessage);
