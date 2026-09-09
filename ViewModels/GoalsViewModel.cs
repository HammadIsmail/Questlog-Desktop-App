using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Models;
using Questlog.Services;

namespace Questlog.ViewModels;

public class GoalsViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    private string _newGoalTitle = string.Empty;
    public string NewGoalTitle
    {
        get => _newGoalTitle;
        set => SetProperty(ref _newGoalTitle, value);
    }

    private string _newGoalDescription = string.Empty;
    public string NewGoalDescription
    {
        get => _newGoalDescription;
        set => SetProperty(ref _newGoalDescription, value);
    }

    private string _newGoalPriority = "medium";
    public string NewGoalPriority
    {
        get => _newGoalPriority;
        set => SetProperty(ref _newGoalPriority, value);
    }

    private int _newGoalEstimateMinutes = 30;
    public int NewGoalEstimateMinutes
    {
        get => _newGoalEstimateMinutes;
        set => SetProperty(ref _newGoalEstimateMinutes, value);
    }

    public ObservableCollection<GoalRecord> Goals { get; } = new();

    public ICommand LoadGoalsCommand { get; }
    public ICommand CreateGoalCommand { get; }
    public ICommand CompleteGoalCommand { get; }
    public ICommand DeleteGoalCommand { get; }

    public GoalsViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;

        LoadGoalsCommand = new AsyncRelayCommand(LoadGoalsAsync);
        CreateGoalCommand = new AsyncRelayCommand(CreateGoalAsync);
        CompleteGoalCommand = new AsyncRelayCommand(param => CompleteGoalAsync(param as GoalRecord));
        DeleteGoalCommand = new AsyncRelayCommand(param => DeleteGoalAsync(param as GoalRecord));
    }

    public override async Task InitializeAsync()
    {
        await LoadGoalsAsync();
    }

    public async Task LoadGoalsAsync()
    {
        IsLoading = true;
        try
        {
            var goals = await _apiClient.GetAllGoalsAsync();
            Goals.Clear();
            foreach (var g in goals)
            {
                Goals.Add(g);
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

    public async Task CreateGoalAsync()
    {
        if (string.IsNullOrWhiteSpace(NewGoalTitle)) return;

        IsLoading = true;
        try
        {
            var request = new GoalCreate(
                Title: NewGoalTitle.Trim(),
                Description: string.IsNullOrWhiteSpace(NewGoalDescription) ? null : NewGoalDescription.Trim(),
                Priority: NewGoalPriority,
                TargetDate: DateTime.UtcNow.Date,
                EstimatedMinutes: NewGoalEstimateMinutes
            );

            var created = await _apiClient.CreateGoalAsync(request);
            if (created != null)
            {
                Goals.Insert(0, created);
                NewGoalTitle = string.Empty;
                NewGoalDescription = string.Empty;
                NewGoalEstimateMinutes = 30;
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
            await LoadGoalsAsync();
        }
    }

    public async Task DeleteGoalAsync(GoalRecord? goal)
    {
        if (goal == null) return;
        bool ok = await _apiClient.DeleteGoalAsync(goal.Id);
        if (ok)
        {
            Goals.Remove(goal);
        }
    }
}
