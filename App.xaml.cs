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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Core HTTP & API Client
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:8000"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _apiClient = new ApiClient(_httpClient);

        // Windows Activity Tracking
        _trackerService = new ActivityTrackerService(_apiClient);

        // ViewModels
        var dashboardVm = new DashboardViewModel(_apiClient, _trackerService);
        var scheduleVm = new ScheduleViewModel(_apiClient);
        var goalsVm = new GoalsViewModel(_apiClient);
        var analyticsVm = new AnalyticsViewModel(_apiClient);
        var activityVm = new ActivityViewModel(_apiClient, _trackerService);
        var coachVm = new CoachViewModel(_apiClient);
        var settingsVm = new SettingsViewModel(_apiClient, _trackerService);

        var mainVm = new MainViewModel(
            dashboardVm,
            scheduleVm,
            goalsVm,
            analyticsVm,
            activityVm,
            coachVm,
            settingsVm
        );

        // Main Window
        var mainWindow = new MainWindow(mainVm);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trackerService?.Dispose();
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
