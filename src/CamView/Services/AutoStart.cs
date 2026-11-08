using Microsoft.Win32;

namespace CamView.Services;

public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "CamView";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
            if (enabled && Environment.ProcessPath is { } exe)
                key.SetValue(Name, $"\"{exe}\"");
            else if (key.GetValue(Name) is not null)
                key.DeleteValue(Name);
        }
        catch
        {
            // Not critical: policy may block the Run key.
        }
    }
}
