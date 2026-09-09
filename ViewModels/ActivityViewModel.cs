using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Models;
using Questlog.Services;

namespace Questlog.ViewModels;

public class ActivityViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly ActivityTrackerService _tracker;

    public ObservableCollection<ActivityRecord> Activities { get; } = new();

    public ICommand LoadActivitiesCommand { get; }
    public ICommand SyncNowCommand { get; }

    public ActivityViewModel(ApiClient apiClient, ActivityTrackerService tracker)
    {
        _apiClient = apiClient;
        _tracker = tracker;

        LoadActivitiesCommand = new AsyncRelayCommand(LoadActivitiesAsync);
        SyncNowCommand = new AsyncRelayCommand(SyncNowAsync);

        _tracker.ActivityLogged += record =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Activities.Insert(0, record);
                if (Activities.Count > 100)
                {
                    Activities.RemoveAt(Activities.Count - 1);
                }
            });
        };
    }

    public override async Task InitializeAsync()
    {
        await LoadActivitiesAsync();
    }

    public async Task LoadActivitiesAsync()
    {
        IsLoading = true;
        try
        {
            var serverActivities = await _apiClient.GetTodayActivitiesAsync();
            Activities.Clear();
            foreach (var act in serverActivities)
            {
                Activities.Add(act);
            }

            foreach (var local in _tracker.GetRecentActivities())
            {
                if (!Activities.Contains(local))
                {
                    Activities.Add(local);
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SyncNowAsync()
    {
        IsLoading = true;
        try
        {
            await _tracker.FlushActivitiesAsync();
            await LoadActivitiesAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }
}
