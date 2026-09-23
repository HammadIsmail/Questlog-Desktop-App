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
    private TtsService? _ttsService;
    private TrayService? _trayService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize Toast notifications (registers AppUserModelID)
        ToastService.Initialize();

        // Core HTTP & API Client (defaults to live Vercel backend, or override via QUESTLOG_API_URL)
        var backendUrl = Environment.GetEnvironmentVariable("QUESTLOG_API_URL")
                         ?? "https://questlog-backend-pi.vercel.app";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(backendUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _apiClient = new ApiClient(_httpClient);

        // ── Persistent login: restore saved JWT from Windows Credential Manager ──
        _apiClient.TryRestoreSession();

        // Services
        _trackerService = new ActivityTrackerService(_apiClient);
        _voiceService = new VoiceService(_apiClient);
        _ttsService = new TtsService(_httpClient);
        _trayService = new TrayService(_trackerService);

        // Wire distraction alert → Toast
        _trackerService.DistractionThresholdExceeded += (appName, minutes) =>
            ToastService.NotifyDistractionDetected(appName, minutes);

        // ViewModels
        var authVm       = new AuthViewModel(_apiClient);
        var dashboardVm  = new DashboardViewModel(_apiClient, _trackerService);
        var scheduleVm   = new ScheduleViewModel(_apiClient);
        var goalsVm      = new GoalsViewModel(_apiClient, _voiceService!, _ttsService);
        var analyticsVm  = new AnalyticsViewModel(_apiClient);
        var activityVm   = new ActivityViewModel(_apiClient, _trackerService);
        var coachVm      = new CoachViewModel(_apiClient, _voiceService);
        var settingsVm   = new SettingsViewModel(_apiClient, _trackerService);

        var mainVm = new MainViewModel(
            _apiClient, authVm,
            dashboardVm, scheduleVm, goalsVm, analyticsVm,
            activityVm, coachVm, settingsVm);

        // Catch unhandled UI exceptions so the app never closes abruptly
        DispatcherUnhandledException += (sender, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[DispatcherUnhandledException] {args.Exception}");
            args.Handled = true;
        };

        // Main Window
        _mainWindow = new MainWindow(mainVm);

        // ── Window Closing: Clean exit ──
        _mainWindow.Closing += (_, closingArgs) =>
        {
            if (!_isShuttingDown)
            {
                _isShuttingDown = true;
                Shutdown();
            }
        };

        _trayService.OpenRequested += () =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
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
        _ttsService?.Dispose();
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
