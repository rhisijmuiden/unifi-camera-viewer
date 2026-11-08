using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CamView.Services;

internal static class NativeMethods
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int SW_RESTORE = 9;

    /// <summary>Makes the Windows title bar follow the app theme (Windows 10 20H1+ and 11).</summary>
    public static void SetDarkTitleBar(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var value = dark ? 1 : 0;
        try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)); }
        catch { /* older Windows: keep the default title bar */ }
    }

    /// <summary>Brings the window of an already running CamView to the front.</summary>
    public static void ActivateExistingInstance()
    {
        var current = Process.GetCurrentProcess();
        var other = Process.GetProcessesByName(current.ProcessName)
            .FirstOrDefault(p => p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero);
        if (other is null) return;
        if (IsIconic(other.MainWindowHandle)) ShowWindow(other.MainWindowHandle, SW_RESTORE);
        SetForegroundWindow(other.MainWindowHandle);
    }
}
