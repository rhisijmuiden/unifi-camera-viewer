using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CamView.Services;

public sealed record ReleaseInfo(Version Version, string Tag, bool IsTest, string Notes, string MsiUrl, string HtmlUrl);

/// <summary>
/// Reads GitHub Releases. Production = normal releases (built from main),
/// Test = pre-releases (built from the test branch). Tags look like v1.0.42 / v1.0.43-test.
/// </summary>
public static class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"CamView/{AppInfo.VersionText}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static async Task<ReleaseInfo?> GetLatestAsync(string channel, CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{AppInfo.UpdateRepository}/releases?per_page=30";
        var releases = await Http.GetFromJsonAsync<List<GhRelease>>(url, ct) ?? new();
        var wantTest = channel == "test";

        return releases
            .Where(r => !r.Draft && r.Prerelease == wantTest)
            .Select(r => new
            {
                r,
                v = ParseTag(r.TagName),
                msi = r.Assets.FirstOrDefault(a => a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.v is not null && x.msi is not null)
            .OrderByDescending(x => x.v)
            .Select(x => new ReleaseInfo(x.v!, x.r.TagName, x.r.Prerelease, x.r.Body ?? "", x.msi!.DownloadUrl, x.r.HtmlUrl))
            .FirstOrDefault();
    }

    public static Version? ParseTag(string tag)
    {
        var t = tag.TrimStart('v', 'V');
        var dash = t.IndexOf('-');
        if (dash >= 0) t = t[..dash];
        return Version.TryParse(t, out var v) ? new Version(v.Major, v.Minor, Math.Max(v.Build, 0)) : null;
    }

    /// <summary>Downloads the MSI, starts a silent install and restarts CamView afterwards.</summary>
    public static async Task DownloadAndInstallAsync(ReleaseInfo release, IProgress<int> progress, CancellationToken ct = default)
    {
        var file = Path.Combine(Path.GetTempPath(), $"CamView-{release.Version}.msi");
        using (var resp = await Http.GetAsync(release.MsiUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? 0;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(file);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress.Report((int)(done * 100 / total));
            }
        }

        var exe = Environment.ProcessPath ?? "";
        // Wait until we have exited, install, then start the (new) app again.
        var script = $"/c timeout /t 2 /nobreak >nul & msiexec /i \"{file}\" /passive /norestart & start \"\" \"{exe}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", script) { CreateNoWindow = true, UseShellExecute = false });
        System.Windows.Application.Current.Shutdown();
    }

    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("draft")] public bool Draft { get; set; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = "";
        [JsonPropertyName("assets")] public List<GhAsset> Assets { get; set; } = new();
    }

    private sealed class GhAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
    }
}
