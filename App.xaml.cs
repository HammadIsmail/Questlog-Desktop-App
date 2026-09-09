using System;
using System.Net.Http;
using System.Windows;
using Questlog.Services;
using Questlog.ViewModels;

namespace Questlog;

public partial class App : Application
{
    private HttpClient? _httpClient;
    private ApiClient? _apiClient;
    private ActivityTrackerService? _trackerService;
    private VoiceService? _voiceService;
    private TrayService? _trayService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize Toast notifications (registers AppUserModelID)
        ToastService.Initialize();

        // Core HTTP & API Client
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:8000"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _apiClient = new ApiClient(_httpClient);

        // Services
        _trackerService = new ActivityTrackerService(_apiClient);
        _voiceService = new VoiceService(_apiClient);
        _trayService = new TrayService(_trackerService);

        // Wire distraction alert → Toast
        _trackerService.DistractionThresholdExceeded += (appName, minutes) =>
            ToastService.NotifyDistractionDetected(appName, minutes);

        // ViewModels
        var dashboardVm  = new DashboardViewModel(_apiClient, _trackerService);
        var scheduleVm   = new ScheduleViewModel(_apiClient);
        var goalsVm      = new GoalsViewModel(_apiClient);
        var analyticsVm  = new AnalyticsViewModel(_apiClient);
        var activityVm   = new ActivityViewModel(_apiClient, _trackerService);
        var coachVm      = new CoachViewModel(_apiClient, _voiceService);
        var settingsVm   = new SettingsViewModel(_apiClient, _trackerService);

        var mainVm = new MainViewModel(
            dashboardVm, scheduleVm, goalsVm, analyticsVm,
            activityVm, coachVm, settingsVm);

        // Main Window
        _mainWindow = new MainWindow(mainVm);

        // ── System Tray: intercept close → minimize to tray ──
        _mainWindow.Closing += (_, closingArgs) =>
        {
            // Only hide if tray service is active (not a real Shutdown())
            if (!_isShuttingDown)
            {
                closingArgs.Cancel = true;
                _mainWindow.Hide();
                _trayService!.Show();
            }
        };

        _trayService.OpenRequested += () =>
        {
            _mainWindow.Show();
            _mainWindow.Activate();
            _trayService.Hide();
        };

        _mainWindow.Show();
    }

    private bool _isShuttingDown = false;

    protected override void OnExit(ExitEventArgs e)
    {
        _isShuttingDown = true;
        _trayService?.Dispose();
        _trackerService?.Dispose();
        _voiceService?.Dispose();
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
