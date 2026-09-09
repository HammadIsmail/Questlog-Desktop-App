using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Services;

namespace Questlog.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly ActivityTrackerService _tracker;

    private string _backendUrl = "http://127.0.0.1:8000";
    public string BackendUrl
    {
        get => _backendUrl;
        set => SetProperty(ref _backendUrl, value);
    }

    private string _backendStatus = "Checking...";
    public string BackendStatus
    {
        get => _backendStatus;
        set => SetProperty(ref _backendStatus, value);
    }

    public bool IsTrackingEnabled
    {
        get => _tracker.IsTrackingEnabled;
        set
        {
            if (_tracker.IsTrackingEnabled != value)
            {
                _tracker.IsTrackingEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public double IdleThresholdSeconds
    {
        get => _tracker.IdleThresholdSeconds;
        set
        {
            if (Math.Abs(_tracker.IdleThresholdSeconds - value) > 0.1)
            {
                _tracker.IdleThresholdSeconds = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand CheckHealthCommand { get; }

    public SettingsViewModel(ApiClient apiClient, ActivityTrackerService tracker)
    {
        _apiClient = apiClient;
        _tracker = tracker;

        CheckHealthCommand = new AsyncRelayCommand(CheckHealthAsync);
    }

    public override async Task InitializeAsync()
    {
        await CheckHealthAsync();
    }

    public async Task CheckHealthAsync()
    {
        IsLoading = true;
        try
        {
            bool alive = await _apiClient.IsBackendAliveAsync();
            BackendStatus = alive ? "Online (Healthy)" : "Offline (Cannot reach 127.0.0.1:8000)";
        }
        catch (Exception ex)
        {
            BackendStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
