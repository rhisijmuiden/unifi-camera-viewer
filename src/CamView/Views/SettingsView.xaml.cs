using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CamView.Localization;
using CamView.Models;
using CamView.Services;
using VlcMedia = LibVLCSharp.Shared.Media;
using VlcPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace CamView.Views;

public partial class SettingsView : UserControl
{
    private CameraConfig? _editing;

    public SettingsView()
    {
        InitializeComponent();
        var s = SettingsService.Current;
        CompanyBox.Text = s.CompanyName;
        (s.Language == "nl" ? LangNl : LangEn).IsChecked = true;
        (s.Theme switch { "light" => ThemeLight, "system" => ThemeSystem, _ => ThemeDark }).IsChecked = true;
        AutoStartBox.IsChecked = s.StartWithWindows;
        FullScreenBox.IsChecked = s.StartFullScreen;
        PlainRtspBox.IsChecked = s.PreferPlainRtsp;
        RenderCameraList();
    }

    // ---------- Cameras ----------

    private void RenderCameraList()
    {
        CameraList.Children.Clear();
        var cams = SettingsService.Current.Cameras;
        for (var i = 0; i < cams.Count; i++)
        {
            var cam = cams[i];
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 0) };
            var border = new Border
            {
                BorderBrush = (Brush)FindResource("LineBrush"),
                BorderThickness = new Thickness(0, 0, 0, i < cams.Count - 1 ? 1 : 0),
                Padding = new Thickness(0, 10, 0, 10),
                Child = row,
            };

            var remove = new Button { Content = Loc.T("Remove"), Style = (Style)FindResource("DangerButton"), Margin = new Thickness(8, 0, 0, 0) };
            remove.Click += (_, _) => RemoveCamera(cam);
            var edit = new Button { Content = Loc.T("Edit"), Margin = new Thickness(8, 0, 0, 0) };
            edit.Click += (_, _) => StartEdit(cam);
            var up = new Button { Content = "↑", ToolTip = Loc.T("MoveUp"), Padding = new Thickness(10, 8, 10, 8), IsEnabled = i > 0 };
            up.Click += (_, _) => MoveUp(cam);
            DockPanel.SetDock(remove, Dock.Right);
            DockPanel.SetDock(edit, Dock.Right);
            DockPanel.SetDock(up, Dock.Right);
            row.Children.Add(remove);
            row.Children.Add(edit);
            row.Children.Add(up);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = cam.Name, Foreground = (Brush)FindResource("TextBrush") });
            text.Children.Add(new TextBlock
            {
                Text = StreamUrl.Masked(cam.Url),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = (Brush)FindResource("TextMutedBrush"),
            });
            row.Children.Add(text);
            CameraList.Children.Add(border);
        }
    }

    private bool ValidateForm(out string name, out string url)
    {
        name = NameBox.Text.Trim();
        url = UrlBox.Text.Trim();
        if (name.Length == 0) { ShowMessage(Loc.T("NameRequired"), "ErrorBrush"); return false; }
        if (!StreamUrl.LooksValid(url)) { ShowMessage(Loc.T("InvalidUrl"), "ErrorBrush"); return false; }
        return true;
    }

    private void AddOrUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateForm(out var name, out var url)) return;
        if (_editing is not null)
        {
            _editing.Name = name;
            _editing.Url = url;
        }
        else
        {
            SettingsService.Current.Cameras.Add(new CameraConfig { Name = name, Url = url });
        }
        SettingsService.Save();
        ClearForm();
        RenderCameraList();
        FlashSaved();
    }

    private void StartEdit(CameraConfig cam)
    {
        _editing = cam;
        NameBox.Text = cam.Name;
        UrlBox.Text = cam.Url;
        AddButton.Content = Loc.T("UpdateCamera");
        CancelEditButton.Visibility = Visibility.Visible;
        FormMessage.Visibility = Visibility.Collapsed;
        NameBox.Focus();
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e) => ClearForm();

    private void ClearForm()
    {
        _editing = null;
        NameBox.Text = "";
        UrlBox.Text = "";
        AddButton.Content = Loc.T("AddCamera");
        CancelEditButton.Visibility = Visibility.Collapsed;
        FormMessage.Visibility = Visibility.Collapsed;
    }

    private void RemoveCamera(CameraConfig cam)
    {
        var ok = MessageBox.Show(Window.GetWindow(this)!, Loc.T("RemoveConfirm", cam.Name), "CamView",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        if (!ok) return;
        SettingsService.Current.Cameras.Remove(cam);
        SettingsService.Save();
        if (_editing == cam) ClearForm();
        RenderCameraList();
    }

    private void MoveUp(CameraConfig cam)
    {
        var list = SettingsService.Current.Cameras;
        var i = list.IndexOf(cam);
        if (i <= 0) return;
        list.RemoveAt(i);
        list.Insert(i - 1, cam);
        SettingsService.Save();
        RenderCameraList();
    }

    /// <summary>Opens the stream without showing video and waits for "playing" or an error.</summary>
    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text.Trim();
        if (!StreamUrl.LooksValid(url)) { ShowMessage(Loc.T("InvalidUrl"), "ErrorBrush"); return; }

        TestButton.IsEnabled = false;
        ShowMessage(Loc.T("Testing"), "TextMutedBrush");
        var playUrl = StreamUrl.ForPlayback(url, PlainRtspBox.IsChecked == true);

        var ok = await Task.Run(() =>
        {
            var done = new TaskCompletionSource<bool>();
            using var media = new VlcMedia(App.Vlc, new Uri(playUrl));
            media.AddOption(":no-audio");
            media.AddOption(":no-video");
            using var player = new VlcPlayer(App.Vlc);
            player.Playing += (_, _) => done.TrySetResult(true);
            player.EncounteredError += (_, _) => done.TrySetResult(false);
            player.EndReached += (_, _) => done.TrySetResult(false);
            player.Play(media);
            var result = done.Task.Wait(TimeSpan.FromSeconds(12)) && done.Task.Result;
            player.Stop();
            return result;
        });

        ShowMessage(ok ? Loc.T("TestOk") : $"{Loc.T("TestFailed")}. {Loc.T("OfficeHint")}.", ok ? "OkBrush" : "ErrorBrush");
        TestButton.IsEnabled = true;
    }

    private void ShowMessage(string text, string brushKey)
    {
        FormMessage.Text = text;
        FormMessage.Foreground = (Brush)FindResource(brushKey);
        FormMessage.Visibility = Visibility.Visible;
    }

    // ---------- General ----------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var s = SettingsService.Current;
        s.CompanyName = CompanyBox.Text.Trim();
        s.Language = LangNl.IsChecked == true ? "nl" : "en";
        s.Theme = ThemeLight.IsChecked == true ? "light" : ThemeSystem.IsChecked == true ? "system" : "dark";
        s.StartWithWindows = AutoStartBox.IsChecked == true;
        s.StartFullScreen = FullScreenBox.IsChecked == true;
        s.PreferPlainRtsp = PlainRtspBox.IsChecked == true;
        SettingsService.Save();

        Loc.Instance.SetLanguage(s.Language);
        ThemeService.Apply(s.Theme);
        RenderCameraList(); // re-translate buttons
        if (_editing is null) AddButton.Content = Loc.T("AddCamera");
        FlashSaved();
    }

    private void FlashSaved()
    {
        SavedText.Visibility = Visibility.Visible;
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        t.Tick += (_, _) => { t.Stop(); SavedText.Visibility = Visibility.Collapsed; };
        t.Start();
    }
}
