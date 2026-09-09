using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;

namespace Questlog.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly DashboardViewModel _dashboardVm;
    private readonly ScheduleViewModel _scheduleVm;
    private readonly GoalsViewModel _goalsVm;
    private readonly AnalyticsViewModel _analyticsVm;
    private readonly ActivityViewModel _activityVm;
    private readonly CoachViewModel _coachVm;
    private readonly SettingsViewModel _settingsVm;

    private ViewModelBase _currentView;
    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    private string _currentViewTitle = "Campaign Overview";
    public string CurrentViewTitle
    {
        get => _currentViewTitle;
        set => SetProperty(ref _currentViewTitle, value);
    }

    public string CurrentDateFormatted => DateTime.Now.ToString("dddd, MMMM d, yyyy");

    public ICommand NavigateToCommand { get; }

    public MainViewModel(
        DashboardViewModel dashboardVm,
        ScheduleViewModel scheduleVm,
        GoalsViewModel goalsVm,
        AnalyticsViewModel analyticsVm,
        ActivityViewModel activityVm,
        CoachViewModel coachVm,
        SettingsViewModel settingsVm)
    {
        _dashboardVm = dashboardVm;
        _scheduleVm = scheduleVm;
        _goalsVm = goalsVm;
        _analyticsVm = analyticsVm;
        _activityVm = activityVm;
        _coachVm = coachVm;
        _settingsVm = settingsVm;

        _currentView = _dashboardVm;

        NavigateToCommand = new AsyncRelayCommand(param => NavigateToAsync(param?.ToString() ?? "Dashboard"));

        _ = _dashboardVm.InitializeAsync();
    }

    public async Task NavigateToAsync(string destination)
    {
        switch (destination)
        {
            case "Dashboard":
                CurrentView = _dashboardVm;
                CurrentViewTitle = "Campaign Overview";
                await _dashboardVm.InitializeAsync();
                break;
            case "Schedule":
                CurrentView = _scheduleVm;
                CurrentViewTitle = "Daily Schedule";
                await _scheduleVm.InitializeAsync();
                break;
            case "Goals":
                CurrentView = _goalsVm;
                CurrentViewTitle = "Active Quests";
                await _goalsVm.InitializeAsync();
                break;
            case "Analytics":
                CurrentView = _analyticsVm;
                CurrentViewTitle = "Tactical Analytics";
                await _analyticsVm.InitializeAsync();
                break;
            case "Activity":
                CurrentView = _activityVm;
                CurrentViewTitle = "Real-Time Activity";
                await _activityVm.InitializeAsync();
                break;
            case "Coach":
                CurrentView = _coachVm;
                CurrentViewTitle = "Dungeon Master AI";
                await _coachVm.InitializeAsync();
                break;
            case "Settings":
                CurrentView = _settingsVm;
                CurrentViewTitle = "System Settings";
                await _settingsVm.InitializeAsync();
                break;
        }
    }
}
