using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Models;
using Questlog.Services;

namespace Questlog.ViewModels;

public class ScheduleViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private Timer? _blockStartTimer;

    public ObservableCollection<ScheduleItemRecord> ScheduleItems { get; } = new();
    public bool HasScheduleItems => ScheduleItems.Count > 0;

    public ICommand LoadScheduleCommand { get; }
    public ICommand GenerateAiScheduleCommand { get; }
    public ICommand ShiftScheduleCommand { get; }

    public ScheduleViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
        ScheduleItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasScheduleItems));

        LoadScheduleCommand = new AsyncRelayCommand(LoadScheduleAsync);
        GenerateAiScheduleCommand = new AsyncRelayCommand(GenerateAiScheduleAsync);
        ShiftScheduleCommand = new AsyncRelayCommand(param => ShiftScheduleAsync(param as ScheduleItemRecord));
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
            StartBlockStartWatcher();
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
            StartBlockStartWatcher();
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

    /// <summary>
    /// Shift all remaining incomplete schedule blocks forward by a given overrun amount.
    /// </summary>
    public async Task ShiftScheduleAsync(ScheduleItemRecord? anchor)
    {
        if (anchor == null) return;

        IsLoading = true;
        try
        {
            // Default overrun of 30 minutes — in a real UI this would come from a dialog input
            const int defaultOverrunMinutes = 30;
            var updated = await _apiClient.ShiftScheduleAsync(anchor.Id, defaultOverrunMinutes);
            if (updated.Count > 0)
            {
                await LoadScheduleAsync();
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

    /// <summary>
    /// Watches for upcoming schedule blocks and fires a toast 60 seconds before start.
    /// </summary>
    private void StartBlockStartWatcher()
    {
        _blockStartTimer?.Dispose();
        _blockStartTimer = new Timer(_ =>
        {
            var now = DateTime.Now;
            foreach (var item in ScheduleItems.ToList())
            {
                if (item.IsCompleted) continue;
                var diff = (item.StartTime.ToLocalTime() - now).TotalSeconds;
                // Fire toast when block is 55–65s away (one-shot window)
                if (diff is >= 55 and <= 65)
                {
                    var timeRange = $"{item.StartTime.ToLocalTime():hh:mm tt} – {item.EndTime.ToLocalTime():hh:mm tt}";
                    ToastService.NotifyFocusBlockStarting(item.Title, timeRange);
                }
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }
}
