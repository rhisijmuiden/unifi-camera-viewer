using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CamView.Localization;
using CamView.Services;

namespace CamView.Views;

public partial class InfoView : UserControl
{
    private ReleaseInfo? _latest;
    private bool _ready;

    public InfoView()
    {
        InitializeComponent();
        VersionText.Text = AppInfo.VersionText;
        BuildChannelText.Text = AppInfo.Channel.ToUpperInvariant();
        DateText.Text = AppInfo.BuildDate.ToString("d MMMM yyyy", Loc.Instance.Language == "nl"
            ? new System.Globalization.CultureInfo("nl-NL") : new System.Globalization.CultureInfo("en-GB"));
        CopyrightText.Text = $"© {DateTime.Now.Year} RHIS";
        (SettingsService.Current.UpdateChannel == "test" ? ChannelTest : ChannelProd).IsChecked = true;
        _ready = true;
        Loaded += async (_, _) => await CheckAsync();
    }

    private async void Channel_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        SettingsService.Current.UpdateChannel = ChannelTest.IsChecked == true ? "test" : "production";
        SettingsService.Save();
        await CheckAsync();
    }

    private async void Check_Click(object sender, RoutedEventArgs e) => await CheckAsync();

    private async Task CheckAsync()
    {
        CheckButton.IsEnabled = false;
        UpdatePanel.Visibility = Visibility.Collapsed;
        NotesButton.Visibility = Visibility.Collapsed;
        SetStatus(Loc.T("Checking"));
        try
        {
            var channel = SettingsService.Current.UpdateChannel;
            _latest = await UpdateService.GetLatestAsync(channel);
            var onOtherChannel = AppInfo.Channel != channel;

            if (_latest is null || (_latest.Version == AppInfo.Version && !onOtherChannel) || (_latest.Version < AppInfo.Version && !onOtherChannel))
            {
                SetStatus(Loc.T("UpToDate"));
                NotesButton.Visibility = _latest is null ? Visibility.Collapsed : Visibility.Visible;
                return;
            }

            // Newer version, or user switched channel (allows going back from test to production).
            UpdateTitle.Text = _latest.Version > AppInfo.Version
                ? Loc.T("UpdateAvailable", _latest.Tag.TrimStart('v'))
                : Loc.T("SwitchAvailable", _latest.Tag.TrimStart('v'));
            UpdateNotes.Text = FirstLines(_latest.Notes);
            InstallButton.Content = _latest.Version > AppInfo.Version ? Loc.T("UpdateNow") : Loc.T("InstallVersion");
            UpdatePanel.Visibility = Visibility.Visible;
            NotesButton.Visibility = Visibility.Visible;
            StatusText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            SetStatus(Loc.T("UpdateFailed", ex.Message));
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_latest is null) return;
        InstallButton.IsEnabled = false;
        CheckButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        try
        {
            var progress = new Progress<int>(p => { Progress.Value = p; SetStatus(Loc.T("Downloading", p)); });
            await UpdateService.DownloadAndInstallAsync(_latest, progress);
            SetStatus(Loc.T("Installing"));
        }
        catch (Exception ex)
        {
            SetStatus(Loc.T("UpdateFailed", ex.Message));
            InstallButton.IsEnabled = true;
            CheckButton.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void Notes_Click(object sender, RoutedEventArgs e)
    {
        if (_latest is not null) Open(_latest.HtmlUrl);
    }

    private void Rhis_Click(object sender, MouseButtonEventArgs e) => Open("https://rhis.nl");

    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        StatusText.Visibility = Visibility.Visible;
    }

    private static string FirstLines(string notes) =>
        string.Join("  ·  ", notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(3));
}
