namespace CamView.Services;

public static class StreamUrl
{
    /// <summary>
    /// UniFi Protect shares streams as rtsps://host:7441/TOKEN?enableSrtp. The same stream is
    /// available unencrypted as rtsp://host:7447/TOKEN, which LibVLC plays without certificate issues.
    /// </summary>
    public static string ForPlayback(string url, bool preferPlain)
    {
        if (!preferPlain || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return url.Trim();
        if (!uri.Scheme.Equals("rtsps", StringComparison.OrdinalIgnoreCase)) return url.Trim();

        var port = uri.Port == 7441 || uri.IsDefaultPort ? 7447 : uri.Port;
        var query = string.Join("&", uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("enableSrtp", StringComparison.OrdinalIgnoreCase)));
        var b = new UriBuilder(uri) { Scheme = "rtsp", Port = port, Query = query };
        return b.Uri.ToString();
    }

    public static bool LooksValid(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var u) &&
        (u.Scheme is "rtsp" or "rtsps" or "http" or "https");

    /// <summary>Hides the access token when showing a URL on screen.</summary>
    public static string Masked(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return url;
        var path = u.AbsolutePath.Trim('/');
        var shown = path.Length > 6 ? path[..6] + "…" : path;
        return $"{u.Scheme}://{u.Host}:{u.Port}/{shown}";
    }
}
