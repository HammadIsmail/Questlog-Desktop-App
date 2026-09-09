using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Questlog.Models;
using Questlog.Tracking;

namespace Questlog.Services;

public class ActivityTrackerService : IDisposable
{
    private readonly ApiClient _apiClient;
    private readonly Timer _pollTimer;
    private readonly Timer _flushTimer;

    private readonly object _syncLock = new();
    private string? _currentAppName;
    private string? _currentWindowTitle;
    private string _currentCategory = "Other";
    private bool _currentIsProductive = true;
    private DateTime _currentSessionStart;

    // Distraction tracking (Phase 6)
    private int _consecutiveUnproductiveSeconds = 0;
    private bool _distractionAlertFired = false;
    private const int DistractionThresholdSeconds = 20 * 60; // 20 minutes

    private readonly List<ActivityCreate> _pendingActivities = new();
    private readonly List<ActivityRecord> _recentActivities = new();

    public event Action<ActivityRecord>? ActivityLogged;
    public event Action<string, string, string>? ActiveWindowChanged;
    /// <summary>Fires once per distraction block when unproductive time exceeds 20 minutes.</summary>
    public event Action<string, int>? DistractionThresholdExceeded;

    public bool IsTrackingEnabled { get; set; } = true;
    public double IdleThresholdSeconds { get; set; } = 120;

    public ActivityTrackerService(ApiClient apiClient)
    {
        _apiClient = apiClient;
        _currentSessionStart = DateTime.UtcNow;

        // Poll foreground window every 1000ms
        _pollTimer = new Timer(PollActiveWindow, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        // Flush pending activities to backend every 30 seconds
        _flushTimer = new Timer(async _ => await FlushActivitiesAsync(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30));
    }

    public IReadOnlyList<ActivityRecord> GetRecentActivities()
    {
        lock (_syncLock)
        {
            return _recentActivities.OrderByDescending(a => a.StartTime).Take(50).ToList();
        }
    }

    private void PollActiveWindow(object? state)
    {
        if (!IsTrackingEnabled) return;

        bool isIdle = IdleDetector.IsIdle(IdleThresholdSeconds);
        var winInfo = isIdle ? null : ForegroundWindowTracker.GetActiveWindow();

        string appName = isIdle ? "System" : (winInfo?.ProcessName ?? "Idle");
        string windowTitle = isIdle ? "User Idle" : (winInfo?.WindowTitle ?? "Idle");

        lock (_syncLock)
        {
            // App or title switched or became idle
            if (appName != _currentAppName || windowTitle != _currentWindowTitle)
            {
                // Reset distraction tracking when app changes
                if (_currentIsProductive == false)
                {
                    _consecutiveUnproductiveSeconds = 0;
                    _distractionAlertFired = false;
                }

                EndCurrentSession();

                // Start new session
                _currentAppName = appName;
                _currentWindowTitle = windowTitle;
                _currentSessionStart = DateTime.UtcNow;
                var (category, isProductive) = ClassifyActivity(appName, windowTitle, isIdle);
                _currentCategory = category;
                _currentIsProductive = isProductive;

                ActiveWindowChanged?.Invoke(_currentAppName, _currentWindowTitle, _currentCategory);
            }
            else if (!_currentIsProductive)
            {
                // Still on the same unproductive app — accumulate time
                _consecutiveUnproductiveSeconds += 1; // polled every 1s
                if (!_distractionAlertFired && _consecutiveUnproductiveSeconds >= DistractionThresholdSeconds)
                {
                    _distractionAlertFired = true;
                    var appForAlert = _currentAppName ?? "Unknown";
                    var minsForAlert = _consecutiveUnproductiveSeconds / 60;
                    DistractionThresholdExceeded?.Invoke(appForAlert, minsForAlert);
                }
            }
        }
    }

    private void EndCurrentSession()
    {
        if (string.IsNullOrEmpty(_currentAppName)) return;

        var now = DateTime.UtcNow;
        var duration = (int)(now - _currentSessionStart).TotalSeconds;

        // Discard trivial flashes under 2 seconds
        if (duration >= 2)
        {
            var activityCreate = new ActivityCreate(
                AppName: _currentAppName,
                WindowTitle: _currentWindowTitle,
                Category: _currentCategory,
                StartTime: _currentSessionStart,
                EndTime: now,
                DurationSeconds: duration,
                IsProductive: _currentIsProductive
            );

            _pendingActivities.Add(activityCreate);

            var record = new ActivityRecord(
                Id: Guid.NewGuid(),
                AppName: _currentAppName,
                WindowTitle: _currentWindowTitle,
                Category: _currentCategory,
                StartTime: _currentSessionStart,
                EndTime: now,
                DurationSeconds: duration,
                IsProductive: _currentIsProductive,
                IsIgnored: false
            );

            _recentActivities.Insert(0, record);
            if (_recentActivities.Count > 100)
            {
                _recentActivities.RemoveAt(_recentActivities.Count - 1);
            }

            ActivityLogged?.Invoke(record);
        }
    }

    public async Task FlushActivitiesAsync()
    {
        List<ActivityCreate> toSend;
        lock (_syncLock)
        {
            if (_pendingActivities.Count == 0) return;
            toSend = new List<ActivityCreate>(_pendingActivities);
            _pendingActivities.Clear();
        }

        try
        {
            bool success = await _apiClient.BatchIngestActivitiesAsync(toSend);
            if (!success)
            {
                // Re-add to retry next flush if failed
                lock (_syncLock)
                {
                    _pendingActivities.InsertRange(0, toSend);
                }
            }
        }
        catch
        {
            lock (_syncLock)
            {
                _pendingActivities.InsertRange(0, toSend);
            }
        }
    }

    private static (string Category, bool IsProductive) ClassifyActivity(string app, string title, bool isIdle)
    {
        if (isIdle) return ("Idle", false);

        string lowerApp = app.ToLowerInvariant();
        string lowerTitle = title.ToLowerInvariant();

        // Entertainment / Gaming
        if (lowerApp.Contains("steam") || lowerApp.Contains("spotify") || lowerApp.Contains("vlc") ||
            lowerApp.Contains("game") || lowerTitle.Contains("youtube") || lowerTitle.Contains("netflix") ||
            lowerTitle.Contains("twitch") || lowerTitle.Contains("reddit") || lowerTitle.Contains("tiktok") ||
            lowerTitle.Contains("facebook") || lowerTitle.Contains("instagram") || lowerTitle.Contains("twitter"))
        {
            return ("Entertainment", false);
        }

        // Development
        if (lowerApp.Contains("devenv") || lowerApp.Contains("code") || lowerApp.Contains("rider") ||
            lowerApp.Contains("idea") || lowerApp.Contains("pycharm") || lowerApp.Contains("powershell") ||
            lowerApp.Contains("cmd") || lowerApp.Contains("wt") || lowerApp.Contains("git") ||
            lowerTitle.Contains("visual studio") || lowerTitle.Contains("github") || lowerTitle.Contains("stack overflow"))
        {
            return ("Development", true);
        }

        // Productivity / Office
        if (lowerApp.Contains("winword") || lowerApp.Contains("excel") || lowerApp.Contains("powerpnt") ||
            lowerApp.Contains("notion") || lowerApp.Contains("obsidian") || lowerApp.Contains("onenote") ||
            lowerApp.Contains("acrobat") || lowerApp.Contains("questlog"))
        {
            return ("Productivity", true);
        }

        // Communication
        if (lowerApp.Contains("slack") || lowerApp.Contains("teams") || lowerApp.Contains("discord") ||
            lowerApp.Contains("outlook") || lowerApp.Contains("thunderbird") || lowerApp.Contains("zoom"))
        {
            return ("Communication", true);
        }

        // Browsers
        if (lowerApp.Contains("chrome") || lowerApp.Contains("msedge") || lowerApp.Contains("firefox") ||
            lowerApp.Contains("brave") || lowerApp.Contains("opera"))
        {
            return ("Browsing", true);
        }

        return ("Other", true);
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
        _flushTimer.Dispose();
        EndCurrentSession();
    }
}
