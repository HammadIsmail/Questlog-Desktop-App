using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Services;

namespace Questlog.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly AuthViewModel _authVm;
    private readonly DashboardViewModel _dashboardVm;
    private readonly ScheduleViewModel _scheduleVm;
    private readonly GoalsViewModel _goalsVm;
    private readonly AnalyticsViewModel _analyticsVm;
    private readonly ActivityViewModel _activityVm;
    private readonly CoachViewModel _coachVm;
    private readonly SettingsViewModel _settingsVm;

    private ViewModelBase _currentView = null!;
    public ViewModelBase CurrentView
    {
        get => _currentView;
        set
        {
            if (_currentView != null)
            {
                _currentView.PropertyChanged -= OnChildViewPropertyChanged;
            }
            if (SetProperty(ref _currentView, value))
            {
                if (_currentView != null)
                {
                    _currentView.PropertyChanged += OnChildViewPropertyChanged;
                }
                OnPropertyChanged(nameof(IsGlobalLoading));
            }
        }
    }

    private string _currentViewTitle = "Campaign Overview";
    public string CurrentViewTitle
    {
        get => _currentViewTitle;
        set => SetProperty(ref _currentViewTitle, value);
    }

    public string CurrentDateFormatted => DateTime.Now.ToString("dddd, MMMM d, yyyy");

    public bool IsAuthenticated => _apiClient.IsAuthenticated;
    public string UserDisplay => _apiClient.CurrentUserName ?? _apiClient.CurrentUserEmail ?? "Adventurer";
    public bool IsGlobalLoading => CurrentView != null && CurrentView.IsLoading;

    public ICommand NavigateToCommand { get; }
    public ICommand LogoutCommand { get; }

    public MainViewModel(
        ApiClient apiClient,
        AuthViewModel authVm,
        DashboardViewModel dashboardVm,
        ScheduleViewModel scheduleVm,
        GoalsViewModel goalsVm,
        AnalyticsViewModel analyticsVm,
        ActivityViewModel activityVm,
        CoachViewModel coachVm,
        SettingsViewModel settingsVm)
    {
        _apiClient = apiClient;
        _authVm = authVm;
        _dashboardVm = dashboardVm;
        _scheduleVm = scheduleVm;
        _goalsVm = goalsVm;
        _analyticsVm = analyticsVm;
        _activityVm = activityVm;
        _coachVm = coachVm;
        _settingsVm = settingsVm;

        NavigateToCommand = new AsyncRelayCommand(param => NavigateToAsync(param?.ToString() ?? "Dashboard"));
        LogoutCommand = new RelayCommand(Logout);

        _apiClient.AuthStateChanged += OnAuthStateChanged;
        _authVm.Authenticated += OnAuthSuccess;

        if (_apiClient.IsAuthenticated)
        {
            _currentView = _dashboardVm;
            _currentViewTitle = "Campaign Overview";
            _ = _dashboardVm.InitializeAsync();
        }
        else
        {
            _currentView = _authVm;
            _currentViewTitle = string.Empty;
        }

        if (_currentView != null)
        {
            _currentView.PropertyChanged += OnChildViewPropertyChanged;
        }
    }

    private void OnChildViewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsLoading))
        {
            OnPropertyChanged(nameof(IsGlobalLoading));
        }
    }

    private void OnAuthStateChanged()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(UserDisplay));
        if (!_apiClient.IsAuthenticated)
        {
            CurrentView = _authVm;
            CurrentViewTitle = string.Empty;
        }
    }

    private void OnAuthSuccess()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(UserDisplay));
        CurrentView = _dashboardVm;
        CurrentViewTitle = "Campaign Overview";
        _ = _dashboardVm.InitializeAsync();
    }

    public void Logout()
    {
        _apiClient.Logout();
    }

    public async Task NavigateToAsync(string destination)
    {
        if (!IsAuthenticated && destination != "Settings")
        {
            CurrentView = _authVm;
            CurrentViewTitle = "Realm Authentication";
            return;
        }

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
