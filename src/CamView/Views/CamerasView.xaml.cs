using System.Windows;
using System.Windows.Controls;
using CamView.Localization;
using CamView.Models;
using CamView.Services;

namespace CamView.Views;

public partial class CamerasView : UserControl
{
    private readonly CameraConfig? _single;
    private int _page;
    private bool _ready;

    public event Action<CameraConfig?>? FullScreenRequested;
    public event Action? SettingsRequested;

    /// <param name="single">Show just this camera (full screen mode).</param>
    public CamerasView(CameraConfig? single = null, bool hideHeader = false)
    {
        InitializeComponent();
        _single = single;
        if (hideHeader) Header.Visibility = Visibility.Collapsed;

        var size = Math.Clamp(SettingsService.Current.GridSize, 1, 4);
        (size switch { 1 => Grid1, 3 => Grid3, 4 => Grid4, _ => Grid2 }).IsChecked = true;
        _ready = true;
        Loaded += (_, _) => Build();
        Unloaded += (_, _) => StopAll();
    }

    private int GridSize => _single is not null ? 1 : Math.Clamp(SettingsService.Current.GridSize, 1, 4);

    private void Build()
    {
        StopAll();
        Tiles.Children.Clear();

        var cameras = _single is not null
            ? new List<CameraConfig> { _single }
            : SettingsService.Current.Cameras;

        Empty.Visibility = cameras.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var perPage = GridSize * GridSize;
        var pages = Math.Max(1, (int)Math.Ceiling(cameras.Count / (double)perPage));
        _page = Math.Clamp(_page, 0, pages - 1);
        Pager.Visibility = pages > 1 ? Visibility.Visible : Visibility.Collapsed;
        PageText.Text = Loc.T("Page", _page + 1, pages);

        var shown = cameras.Skip(_page * perPage).Take(perPage).ToList();
        // Fewer cameras than slots: shrink the grid so tiles stay big (3 cameras in 2×2, 2 cameras side by side).
        var cols = Math.Min(GridSize, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(shown.Count))));
        Tiles.Columns = cols;
        Tiles.Rows = Math.Max(1, (int)Math.Ceiling(shown.Count / (double)cols));

        foreach (var cam in shown)
        {
            var tile = new CameraTile(cam);
            tile.FullScreenRequested += c => FullScreenRequested?.Invoke(c);
            Tiles.Children.Add(tile);
        }
    }

    private void StopAll()
    {
        foreach (var tile in Tiles.Children.OfType<CameraTile>()) tile.Stop();
    }

    private void GridSize_Checked(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton { Tag: string tag }) return;
        SettingsService.Current.GridSize = int.Parse(tag);
        SettingsService.Save();
        _page = 0;
        if (IsLoaded) Build();
    }

    private void PrevPage_Click(object sender, RoutedEventArgs e) { _page--; Build(); }
    private void NextPage_Click(object sender, RoutedEventArgs e) { _page++; Build(); }
    private void FullScreen_Click(object sender, RoutedEventArgs e) => FullScreenRequested?.Invoke(null);
    private void OpenSettings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
}
