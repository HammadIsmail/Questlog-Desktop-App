using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Models;
using Questlog.Services;

namespace Questlog.ViewModels;

public class ScheduleViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    public ObservableCollection<ScheduleItemRecord> ScheduleItems { get; } = new();

    public ICommand LoadScheduleCommand { get; }
    public ICommand GenerateAiScheduleCommand { get; }

    public ScheduleViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;

        LoadScheduleCommand = new AsyncRelayCommand(LoadScheduleAsync);
        GenerateAiScheduleCommand = new AsyncRelayCommand(GenerateAiScheduleAsync);
    }

    public override async Task InitializeAsync()
    {
        await LoadScheduleAsync();
    }

    public async Task LoadScheduleAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _apiClient.GetTodayScheduleAsync();
            ScheduleItems.Clear();
            foreach (var item in items)
            {
                ScheduleItems.Add(item);
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

    public async Task GenerateAiScheduleAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _apiClient.GenerateScheduleAsync();
            ScheduleItems.Clear();
            foreach (var item in items)
            {
                ScheduleItems.Add(item);
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
}
