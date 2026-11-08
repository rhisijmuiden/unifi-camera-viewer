using System.IO;
using System.Reflection;

namespace CamView.Services;

public static class AppInfo
{
    private static readonly Assembly Asm = typeof(AppInfo).Assembly;

    public static Version Version { get; } = Asm.GetName().Version ?? new Version(0, 0, 1);
    public static string VersionText => $"{Version.Major}.{Version.Minor}.{Version.Build}";

    /// <summary>production, test or dev (set at build time).</summary>
    public static string Channel { get; } =
        Asm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "Channel")?.Value ?? "dev";

    public static DateTime BuildDate
    {
        get
        {
            try { return File.GetLastWriteTime(Environment.ProcessPath ?? Asm.Location); }
            catch { return DateTime.Now; }
        }
    }

    /// <summary>GitHub repository that publishes the MSI releases.</summary>
    public const string UpdateRepository = "rhisijmuiden/unifi-camera-viewer";
}
