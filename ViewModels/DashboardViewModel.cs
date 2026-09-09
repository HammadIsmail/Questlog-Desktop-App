using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Models;
using Questlog.Services;

namespace Questlog.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly ActivityTrackerService _tracker;

    private int _score = 750;
    public int Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    private int _streakDays = 5;
    public int StreakDays
    {
        get => _streakDays;
        set => SetProperty(ref _streakDays, value);
    }

    private int _productiveMinutes = 185;
    public int ProductiveMinutes
    {
        get => _productiveMinutes;
        set => SetProperty(ref _productiveMinutes, value);
    }

    private int _totalActiveMinutes = 240;
    public int TotalActiveMinutes
    {
        get => _totalActiveMinutes;
        set => SetProperty(ref _totalActiveMinutes, value);
    }

    private string _currentApp = "Visual Studio Code";
    public string CurrentApp
    {
        get => _currentApp;
        set => SetProperty(ref _currentApp, value);
    }

    private string _currentCategory = "Development";
    public string CurrentCategory
    {
        get => _currentCategory;
        set => SetProperty(ref _currentCategory, value);
    }

    private string _scoreLevel = "Apprentice Adept";
    public string ScoreLevel
    {
        get => _scoreLevel;
        set => SetProperty(ref _scoreLevel, value);
    }

    public ObservableCollection<GoalRecord> TodayGoals { get; } = new();
    public ObservableCollection<ScheduleItemRecord> TodaySchedule { get; } = new();

    public ICommand LoadDashboardDataCommand { get; }
    public ICommand CompleteGoalCommand { get; }

    public DashboardViewModel(ApiClient apiClient, ActivityTrackerService tracker)
    {
        _apiClient = apiClient;
        _tracker = tracker;

        LoadDashboardDataCommand = new AsyncRelayCommand(LoadDashboardDataAsync);
        CompleteGoalCommand = new AsyncRelayCommand(param => CompleteGoalAsync(param as GoalRecord));

        _tracker.ActiveWindowChanged += (app, title, category) =>
        {
            CurrentApp = string.IsNullOrWhiteSpace(app) ? "System" : app;
            CurrentCategory = category;
        };
    }

    public override async Task InitializeAsync()
    {
        await LoadDashboardDataAsync();
    }

    public async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        try
        {
            var score = await _apiClient.GetTodayScoreAsync();
            if (score != null)
            {
                Score = score.Score;
                StreakDays = score.StreakDays;
                ProductiveMinutes = score.ProductiveMinutes;
                TotalActiveMinutes = score.TotalActiveMinutes;
                ScoreLevel = GetRankForScore(Score);
            }

            var goals = await _apiClient.GetTodayGoalsAsync();
            TodayGoals.Clear();
            foreach (var g in goals)
            {
                TodayGoals.Add(g);
            }

            var schedule = await _apiClient.GetTodayScheduleAsync();
            TodaySchedule.Clear();
            foreach (var s in schedule)
            {
                TodaySchedule.Add(s);
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

    public async Task CompleteGoalAsync(GoalRecord? goal)
    {
        if (goal == null) return;
        bool ok = await _apiClient.CompleteGoalAsync(goal.Id);
        if (ok)
        {
            await LoadDashboardDataAsync();
        }
    }

    private static string GetRankForScore(int score) => score switch
    {
        >= 900 => "Legendary Grandmaster",
        >= 800 => "Master Tactician",
        >= 700 => "Journeyman Knight",
        >= 500 => "Apprentice Adept",
        >= 300 => "Novice Adventurer",
        _ => "Initiate"
    };
}
