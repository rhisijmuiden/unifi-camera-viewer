using System.Text.Json.Serialization;

namespace CamView.Models;

public sealed class CameraConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>Stream URL, stored encrypted (DPAPI, current Windows user) in settings.json.</summary>
    [JsonIgnore]
    public string Url { get; set; } = "";

    [JsonPropertyName("url")]
    public string ProtectedUrl
    {
        get => Services.Secret.Protect(Url);
        set => Url = Services.Secret.Unprotect(value);
    }
}

public sealed class AppSettings
{
    public string CompanyName { get; set; } = "";
    public string Language { get; set; } = "en";      // en | nl
    public string Theme { get; set; } = "dark";       // dark | light | system
    public int GridSize { get; set; } = 2;            // 1, 2, 3, 4 (columns = rows)
    public bool StartWithWindows { get; set; }
    public bool StartFullScreen { get; set; }
    /// <summary>Play rtsps://host:7441 links as plain rtsp://host:7447 (more reliable with UniFi Protect's self-signed certificate).</summary>
    public bool PreferPlainRtsp { get; set; } = true;
    public string UpdateChannel { get; set; } = "production"; // production | test
    public List<CameraConfig> Cameras { get; set; } = new();
}
