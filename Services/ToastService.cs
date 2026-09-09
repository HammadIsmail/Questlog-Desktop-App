using System;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Questlog.Services;

/// <summary>
/// Sends Windows 10/11 toast (banner) notifications for key Questlog events.
/// Uses Microsoft.Toolkit.Uwp.Notifications which handles AppUserModelID
/// registration automatically for non-packaged WPF apps.
/// </summary>
public static class ToastService
{
    private static bool _initialized = false;

    /// <summary>Call once on app startup to register the app ID for toast delivery.</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        try
        {
            // The toolkit registers the AUMID automatically on first use.
            // We send a silent registration call to ensure it's ready.
            ToastNotificationManagerCompat.History.Clear();
            _initialized = true;
        }
        catch
        {
            // Non-critical — toasts will gracefully fail if not supported
        }
    }

    /// <summary>
    /// Notify that a scheduled focus block is about to start.
    /// Fires ~60 seconds before block start time.
    /// </summary>
    public static void NotifyFocusBlockStarting(string title, string timeRange)
    {
        TrySend(() =>
            new ToastContentBuilder()
                .AddText("⏰ Focus Block Starting")
                .AddText($"{title}")
                .AddText($"Scheduled: {timeRange}")
                .AddArgument("action", "open")
                .Show());
    }

    /// <summary>
    /// Notify that the user has been in an unproductive app for too long.
    /// Fires once per distraction block at the 20-minute threshold.
    /// </summary>
    public static void NotifyDistractionDetected(string appName, int minutes)
    {
        TrySend(() =>
            new ToastContentBuilder()
                .AddText("⚠️ Distraction Detected")
                .AddText($"{appName} — {minutes} minutes and counting")
                .AddText("Return to your quest before the Dungeon Master notices.")
                .AddArgument("action", "open")
                .Show());
    }

    /// <summary>
    /// Notify that a quest (goal) has been completed.
    /// </summary>
    public static void NotifyQuestCompleted(string questTitle)
    {
        TrySend(() =>
            new ToastContentBuilder()
                .AddText("✅ Quest Completed!")
                .AddText($"\"{questTitle}\"")
                .AddText("Score updated. The Dungeon Master approves.")
                .AddArgument("action", "open")
                .Show());
    }

    private static void TrySend(Action send)
    {
        try
        {
            send();
        }
        catch
        {
            // Toast failures are non-critical — swallow silently
        }
    }
}
