using System.ComponentModel;

namespace CamView.Localization;

/// <summary>
/// Simple runtime-switchable translations. In XAML:
/// Text="{Binding [Cameras], Source={x:Static loc:Loc.Instance}}"
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language { get; private set; } = "en";

    public string this[string key] =>
        (Language == "nl" && Strings.Nl.TryGetValue(key, out var nl)) ? nl :
        Strings.En.TryGetValue(key, out var en) ? en : key;

    public static string T(string key) => Instance[key];
    public static string T(string key, params object[] args) => string.Format(Instance[key], args);

    public void SetLanguage(string lang)
    {
        Language = lang == "nl" ? "nl" : "en";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }
}
