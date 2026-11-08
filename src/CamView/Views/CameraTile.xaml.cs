using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CamView.Localization;
using CamView.Models;
using CamView.Services;
using LibVLCSharp.Shared;

namespace CamView.Views;

public partial class CameraTile : UserControl
{
    private enum State { Connecting, Playing, Reconnecting, Error }

    private readonly CameraConfig _camera;
    private readonly DispatcherTimer _watchdog = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer _retry = new() { Interval = TimeSpan.FromSeconds(5) };
    private MediaPlayer? _player;
    private Media? _media;
    private int _failures;
    private bool _stopped;

    /// <summary>Raised when the user asks to see this camera full screen.</summary>
    public event Action<CameraConfig>? FullScreenRequested;

    public CameraTile(CameraConfig camera)
    {
        InitializeComponent();
        _camera = camera;
        NameText.Text = camera.Name;
        _watchdog.Tick += (_, _) => { _watchdog.Stop(); Fail(); };
        _retry.Tick += (_, _) => { _retry.Stop(); Play(); };
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
    }

    private void Start()
    {
        if (_player is not null) return;
        _stopped = false;
        _player = new MediaPlayer(App.Vlc) { EnableHardwareDecoding = true };
        _player.Playing += (_, _) => Dispatcher.BeginInvoke(new Action(() => SetState(State.Playing)));
        _player.EncounteredError += (_, _) => Dispatcher.BeginInvoke(new Action(Fail));
        _player.EndReached += (_, _) => Dispatcher.BeginInvoke(new Action(Fail));
        Video.MediaPlayer = _player;
        Play();
    }

    private void Play()
    {
        if (_stopped || _player is null) return;
        _media?.Dispose();
        var url = StreamUrl.ForPlayback(_camera.Url, SettingsService.Current.PreferPlainRtsp);
        if (!StreamUrl.LooksValid(url))
        {
            SetState(State.Error);
            return;
        }
        _media = new Media(App.Vlc, new Uri(url));
        _media.AddOption(":no-audio");
        _player.Play(_media);
        SetState(_failures == 0 ? State.Connecting : State.Reconnecting);
        _watchdog.Start();
    }

    private void Fail()
    {
        if (_stopped) return;
        _watchdog.Stop();
        _failures++;
        SetState(_failures >= 3 ? State.Error : State.Reconnecting);
        // Back off: 5s, 10s, ... max 30s. Keep trying, the office network may come back.
        _retry.Interval = TimeSpan.FromSeconds(Math.Min(30, 5 * _failures));
        _retry.Start();
    }

    private void SetState(State state)
    {
        if (state == State.Playing)
        {
            _watchdog.Stop();
            _failures = 0;
        }
        LiveBadge.Visibility = state == State.Playing ? Visibility.Visible : Visibility.Collapsed;
        StatusPanel.Visibility = state == State.Playing ? Visibility.Collapsed : Visibility.Visible;
        StatusHint.Visibility = state is State.Reconnecting or State.Error ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = state switch
        {
            State.Connecting => Loc.T("Connecting"),
            State.Reconnecting => Loc.T("Reconnecting"),
            State.Error => Loc.T("StreamError"),
            _ => "",
        };
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(
            state == State.Error ? "ErrorBrush" : state == State.Connecting ? "TextMutedBrush" : "WarnBrush");
    }

    public void Stop()
    {
        _stopped = true;
        _watchdog.Stop();
        _retry.Stop();
        var player = _player;
        var media = _media;
        _player = null;
        _media = null;
        Video.MediaPlayer = null;
        if (player is null) return;
        // Stopping an RTSP stream can block for a moment: do it off the UI thread.
        Task.Run(() =>
        {
            try { player.Stop(); } catch { /* ignore */ }
            player.Dispose();
            media?.Dispose();
        });
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) FullScreenRequested?.Invoke(_camera);
    }

    private void FullScreen_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        FullScreenRequested?.Invoke(_camera);
    }
}
