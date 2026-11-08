using System.Windows;
using Microsoft.Win32;

namespace CamView.Services;

public static class ThemeService
{
    public static void Apply(string theme)
    {
        var dark = theme switch
        {
            "light" => false,
            "system" => SystemPrefersDark(),
            _ => true,
        };
        var uri = new Uri($"pack://application:,,,/Themes/Theme.{(dark ? "Dark" : "Light")}.xaml");
        var dicts = Application.Current.Resources.MergedDictionaries;
        var old = dicts.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Themes/Theme.") == true);
        var fresh = new ResourceDictionary { Source = uri };
        if (old is null) dicts.Insert(0, fresh);
        else dicts[dicts.IndexOf(old)] = fresh;
    }

    private static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return true; }
    }
}
