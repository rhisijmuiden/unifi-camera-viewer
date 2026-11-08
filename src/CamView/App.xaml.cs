using System.Windows;
using CamView.Localization;
using CamView.Services;
using LibVLCSharp.Shared;

namespace CamView;

public partial class App : Application
{
    /// <summary>One LibVLC instance shared by all camera tiles.</summary>
    public static LibVLC Vlc { get; private set; } = null!;

    private static Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One CamView per user: a second start brings the running window to the front instead
        // of opening every stream twice.
        _singleInstance = new Mutex(true, @"Local\RHIS.CamView", out var isFirst);
        if (!isFirst)
        {
            NativeMethods.ActivateExistingInstance();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "CamView", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        SettingsService.Load();
        Loc.Instance.SetLanguage(SettingsService.Current.Language);
        ThemeService.Apply(SettingsService.Current.Theme);

        Core.Initialize();
        // Low latency, RTSP over TCP (reliable through switches/firewalls), no on-screen title.
        Vlc = new LibVLC("--rtsp-tcp", "--network-caching=300", "--no-video-title-show", "--quiet");

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Vlc?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
