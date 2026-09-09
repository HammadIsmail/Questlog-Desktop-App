using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace Questlog.Services;

/// <summary>
/// Manages the Windows System Tray (NotifyIcon) for Questlog.
/// Provides minimize-to-tray behavior, keeping telemetry running silently.
/// Requires UseWindowsForms=true in the project file.
/// </summary>
public class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ActivityTrackerService _tracker;

    public event Action? OpenRequested;

    public TrayService(ActivityTrackerService tracker)
    {
        _tracker = tracker;

        _notifyIcon = new NotifyIcon
        {
            Text = "Questlog — Real-Life Dungeon Master",
            Icon = CreateDefaultIcon(),
            Visible = false,
        };

        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _notifyIcon.ContextMenuStrip = BuildContextMenu();
    }

    /// <summary>Show the tray icon (called when main window is hidden).</summary>
    public void Show() => _notifyIcon.Visible = true;

    /// <summary>Hide the tray icon (called when main window is shown).</summary>
    public void Hide() => _notifyIcon.Visible = false;

    /// <summary>Show a tray balloon notification.</summary>
    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(4000, title, message, icon);
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        // ── Open Console ──
        var openItem = new ToolStripMenuItem("Open Console");
        openItem.Font = new Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += (_, _) => OpenRequested?.Invoke();

        // ── Pause / Resume Tracking ──
        var pauseItem = new ToolStripMenuItem("Pause Tracking");
        pauseItem.Click += (_, _) =>
        {
            _tracker.IsTrackingEnabled = !_tracker.IsTrackingEnabled;
            pauseItem.Text = _tracker.IsTrackingEnabled ? "Pause Tracking" : "Resume Tracking";
            _notifyIcon.Text = _tracker.IsTrackingEnabled
                ? "Questlog — Tracking Active"
                : "Questlog — Tracking Paused";
        };

        // ── Exit ──
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _notifyIcon.Visible = false;
            System.Windows.Application.Current.Dispatcher.Invoke(
                () => System.Windows.Application.Current.Shutdown());
        };

        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    /// <summary>
    /// Generate a minimal programmatic icon when no .ico file is embedded.
    /// Draws a simple ⚔ sword glyph on the accent color.
    /// </summary>
    private static Icon CreateDefaultIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(216, 92, 50)); // #D85C32 accent
        using var brush = new SolidBrush(Color.White);
        using var font = new Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
        g.DrawString("Q", font, brush, -1f, 0f);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
