using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CamView.Models;
using CamView.Services;
using CamView.Views;

namespace CamView;

public partial class MainWindow : Window
{
    private bool _fullScreen;
    private WindowState _prevState;

    public MainWindow()
    {
        InitializeComponent();
        ApplyBranding();
        SettingsService.Changed += ApplyBranding;
        SourceInitialized += (_, _) => NativeMethods.SetDarkTitleBar(this, ThemeService.IsDark);
        ThemeService.Changed += dark => NativeMethods.SetDarkTitleBar(this, dark);
        NavCameras.IsChecked = true;

        Loaded += async (_, _) =>
        {
            if (SettingsService.Current.StartFullScreen && SettingsService.Current.Cameras.Count > 0)
                EnterFullScreen(null);
            await CheckForUpdatesQuietly();
        };
    }

    private void ApplyBranding()
    {
        var company = SettingsService.Current.CompanyName.Trim();
        CompanyText.Text = company.Length > 0 ? company : "CamView";
        ProductText.Visibility = company.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Title = company.Length > 0 ? $"CamView · {company}" : "CamView";
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, NavCameras)) ShowCameras();
        else if (ReferenceEquals(sender, NavSettings)) PageHost.Content = new SettingsView();
        else if (ReferenceEquals(sender, NavInfo)) PageHost.Content = new InfoView();
    }

    private void ShowCameras()
    {
        var view = new CamerasView();
        view.FullScreenRequested += EnterFullScreen;
        view.SettingsRequested += () => NavSettings.IsChecked = true;
        PageHost.Content = view;
    }

    /// <param name="camera">One camera, or null for the whole grid.</param>
    private void EnterFullScreen(CameraConfig? camera)
    {
        if (_fullScreen) { ExitFullScreen(); if (camera is null) return; }
        _fullScreen = true;
        _prevState = WindowState;
        SidebarColumn.Width = new GridLength(0);
        Sidebar.Visibility = Visibility.Collapsed;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        // Toggle state so Windows recalculates the size without the title bar.
        WindowState = WindowState.Normal;
        WindowState = WindowState.Maximized;

        var view = new CamerasView(camera, hideHeader: true);
        view.FullScreenRequested += c => { if (camera is null && c is not null) EnterFullScreen(c); else ExitFullScreen(); };
        PageHost.Content = view;

        FullScreenHint.Visibility = Visibility.Visible;
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        t.Tick += (_, _) => { t.Stop(); FullScreenHint.Visibility = Visibility.Collapsed; };
        t.Start();
    }

    private void ExitFullScreen()
    {
        if (!_fullScreen) return;
        _fullScreen = false;
        FullScreenHint.Visibility = Visibility.Collapsed;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.CanResize;
        WindowState = _prevState;
        SidebarColumn.Width = new GridLength(232);
        Sidebar.Visibility = Visibility.Visible;
        if (NavCameras.IsChecked == true) ShowCameras();
        else NavCameras.IsChecked = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _fullScreen) { ExitFullScreen(); e.Handled = true; }
        else if (e.Key == Key.F11) { if (_fullScreen) ExitFullScreen(); else EnterFullScreen(null); e.Handled = true; }
    }

    private async Task CheckForUpdatesQuietly()
    {
        try
        {
            var latest = await UpdateService.GetLatestAsync(SettingsService.Current.UpdateChannel);
            if (latest is not null && latest.Version > AppInfo.Version)
                UpdateDot.Visibility = Visibility.Visible;
        }
        catch
        {
            // Offline or rate limited: the Info page shows details when the user checks manually.
        }
    }

    private void Rhis_Click(object sender, MouseButtonEventArgs e) =>
        Process.Start(new ProcessStartInfo("https://rhis.nl") { UseShellExecute = true });
}
