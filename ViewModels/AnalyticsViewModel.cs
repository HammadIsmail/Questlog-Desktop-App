using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Models;
using Questlog.Services;

namespace Questlog.ViewModels;

public class AnalyticsViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    private int _totalMinutes;
    public int TotalMinutes
    {
        get => _totalMinutes;
        set => SetProperty(ref _totalMinutes, value);
    }

    private int _productiveMinutes;
    public int ProductiveMinutes
    {
        get => _productiveMinutes;
        set => SetProperty(ref _productiveMinutes, value);
    }

    private int _unproductiveMinutes;
    public int UnproductiveMinutes
    {
        get => _unproductiveMinutes;
        set => SetProperty(ref _unproductiveMinutes, value);
    }

    private double _productivityPercentage;
    public double ProductivityPercentage
    {
        get => _productivityPercentage;
        set => SetProperty(ref _productivityPercentage, value);
    }

    public ObservableCollection<KeyValuePair<string, int>> CategoryBreakdown { get; } = new();
    public ObservableCollection<ScoreEventRecord> ScoreEvents { get; } = new();

    public ICommand LoadAnalyticsCommand { get; }

    public AnalyticsViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;

        LoadAnalyticsCommand = new AsyncRelayCommand(LoadAnalyticsAsync);
    }

    public override async Task InitializeAsync()
    {
        await LoadAnalyticsAsync();
    }

    public async Task LoadAnalyticsAsync()
    {
        IsLoading = true;
        try
        {
            var daily = await _apiClient.GetTodayAnalyticsAsync();
            if (daily != null)
            {
                TotalMinutes = daily.TotalMinutes;
                ProductiveMinutes = daily.ProductiveMinutes;
                UnproductiveMinutes = daily.UnproductiveMinutes;
                ProductivityPercentage = daily.ProductivityPercentage;

                CategoryBreakdown.Clear();
                if (daily.CategoryBreakdown != null)
                {
                    foreach (var pair in daily.CategoryBreakdown)
                    {
                        CategoryBreakdown.Add(pair);
                    }
                }
            }

            var events = await _apiClient.GetScoreEventsAsync();
            ScoreEvents.Clear();
            foreach (var ev in events)
            {
                ScoreEvents.Add(ev);
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
