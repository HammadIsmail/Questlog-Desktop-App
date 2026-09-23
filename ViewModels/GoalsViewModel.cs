using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Models;
using Questlog.Services;

namespace Questlog.ViewModels;

public class GoalsViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly VoiceService _voiceService;
    private readonly TtsService? _ttsService;

    // ── Manual Input Fields ──
    private string _newGoalTitle = string.Empty;
    public string NewGoalTitle
    {
        get => _newGoalTitle;
        set => SetProperty(ref _newGoalTitle, value);
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

    // ── Voice / Conversational State ──
    private bool _isVoiceListening;
    public bool IsVoiceListening
    {
        get => _isVoiceListening;
        set => SetProperty(ref _isVoiceListening, value);
    }

    private string _voiceStatusText = "🎙️ Click the mic or speak your quest";
    public string VoiceStatusText
    {
        get => _voiceStatusText;
        set => SetProperty(ref _voiceStatusText, value);
    }

    private string _spokenTranscript = string.Empty;
    public string SpokenTranscript
    {
        get => _spokenTranscript;
        set
        {
            if (SetProperty(ref _spokenTranscript, value))
            {
                OnPropertyChanged(nameof(HasSpokenTranscript));
            }
        }
    }
    public bool HasSpokenTranscript => !string.IsNullOrWhiteSpace(_spokenTranscript);

    private string _voiceInputText = string.Empty;
    public string VoiceInputText
    {
        get => _voiceInputText;
        set => SetProperty(ref _voiceInputText, value);
    }

    private string _assistantMessage = "What quest or task would you like to add?";
    public string AssistantMessage
    {
        get => _assistantMessage;
        set => SetProperty(ref _assistantMessage, value);
    }

    // ── Filter State ("all", "pending", "completed") ──
    private string _selectedFilter = "all";
    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                ApplyFilter();
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterPending));
                OnPropertyChanged(nameof(IsFilterCompleted));
            }
        }
    }

    public bool IsFilterAll => _selectedFilter == "all";
    public bool IsFilterPending => _selectedFilter == "pending";
    public bool IsFilterCompleted => _selectedFilter == "completed";

    // ── Collections ──
    public ObservableCollection<GoalRecord> Goals { get; } = new();
    public ObservableCollection<GoalRecord> FilteredGoals { get; } = new();
    public ObservableCollection<ChatMessage> ChatHistory { get; } = new();

    public bool HasGoals => FilteredGoals.Count > 0;
    public int ActiveCount => Goals.Count(g => !g.IsCompleted);
    public int CompletedCount => Goals.Count(g => g.IsCompleted);

    // ── Commands ──
    public ICommand LoadGoalsCommand { get; }
    public ICommand CreateGoalCommand { get; }
    public ICommand CompleteGoalCommand { get; }
    public ICommand DeleteGoalCommand { get; }
    public ICommand ToggleVoiceListeningCommand { get; }
    public ICommand SubmitVoicePlanCommand { get; }
    public ICommand QuickSpokenPromptCommand { get; }
    public ICommand SetFilterCommand { get; }

    public GoalsViewModel(ApiClient apiClient, VoiceService voiceService, TtsService? ttsService = null)
    {
        _apiClient = apiClient;
        _voiceService = voiceService;
        _ttsService = ttsService;

        LoadGoalsCommand = new AsyncRelayCommand(LoadGoalsAsync);
        CreateGoalCommand = new AsyncRelayCommand(CreateGoalAsync);
        CompleteGoalCommand = new AsyncRelayCommand(param => CompleteGoalAsync(param as GoalRecord));
        DeleteGoalCommand = new AsyncRelayCommand(param => DeleteGoalAsync(param as GoalRecord));
        ToggleVoiceListeningCommand = new AsyncRelayCommand(ToggleVoiceListeningAsync);
        SubmitVoicePlanCommand = new AsyncRelayCommand(SubmitVoicePlanAsync);
        SetFilterCommand = new RelayCommand(param => SelectedFilter = param?.ToString() ?? "all");

        QuickSpokenPromptCommand = new AsyncRelayCommand(async param =>
        {
            if (param is string prompt && !string.IsNullOrWhiteSpace(prompt))
            {
                VoiceInputText = prompt;
                await ProcessUserInputAsync(prompt);
            }
        });

        // Wire voice streaming events
        _voiceService.StateChanged += OnVoiceStateChanged;
        _voiceService.TranscriptPartialReceived += OnPartialTranscript;
        _voiceService.TranscriptFinalReceived += OnFinalTranscript;
        _voiceService.ErrorOccurred += OnVoiceError;
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
            ApplyFilter();
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

    private void ApplyFilter()
    {
        FilteredGoals.Clear();
        var items = _selectedFilter switch
        {
            "pending" => Goals.Where(g => !g.IsCompleted),
            "completed" => Goals.Where(g => g.IsCompleted),
            _ => Goals
        };

        foreach (var item in items)
        {
            FilteredGoals.Add(item);
        }

        OnPropertyChanged(nameof(HasGoals));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(CompletedCount));
    }

    public async Task ToggleVoiceListeningAsync()
    {
        if (_voiceService.IsActive)
        {
            _ttsService?.Stop();
            await _voiceService.StopSessionAsync();
            VoiceStatusText = "🎙️ Microphone stopped. Click to speak again.";
        }
        else
        {
            // Prompt the user verbally and in the bubble
            var prompt = "What quest or task would you like to add?";
            AssistantMessage = prompt;
            _ = _ttsService?.SpeakAsync(prompt);

            await _voiceService.StartSessionAsync();
        }
    }

    private void OnVoiceStateChanged(VoiceState state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsVoiceListening = state is VoiceState.Listening or VoiceState.Processing or VoiceState.Connecting;
            VoiceStatusText = state switch
            {
                VoiceState.Connecting => "⏳ Connecting to AssemblyAI streaming...",
                VoiceState.Listening => "🔴 Listening... Speak your task, update, or deletion",
                VoiceState.Processing => "⏳ Processing speech...",
                VoiceState.Error => "⚠️ Voice connection issue. Click mic to retry.",
                _ => "🎙️ Click the mic or speak your quest"
            };
        });
    }

    private void OnPartialTranscript(string text)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SpokenTranscript = text;
            VoiceInputText = text;
        });
    }

    private void OnFinalTranscript(string text)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            SpokenTranscript = string.Empty;
            VoiceInputText = text;
            await ProcessUserInputAsync(text);
        });
    }

    private void OnVoiceError(string err)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            AssistantMessage = $"Voice notice: {err}. You can also type your command below.";
        });
    }

    public async Task SubmitVoicePlanAsync()
    {
        if (string.IsNullOrWhiteSpace(VoiceInputText)) return;
        var text = VoiceInputText;
        VoiceInputText = string.Empty;
        await ProcessUserInputAsync(text);
    }

    public async Task ProcessUserInputAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Record user turn in chat
        ChatHistory.Add(new ChatMessage("user", text, DateTime.Now));

        var historyForApi = ChatHistory
            .TakeLast(8)
            .Select(m => new ConversationTurn(m.Role, m.Text))
            .ToList();

        IsLoading = true;
        try
        {
            var res = await _apiClient.CreateGoalsFromConversationAsync(text, historyForApi);
            if (res != null)
            {
                // Handle created goals
                if (res.CreatedGoals != null && res.CreatedGoals.Count > 0)
                {
                    foreach (var g in res.CreatedGoals)
                    {
                        Goals.Insert(0, g);
                    }
                }

                // Handle updated goals
                if (res.UpdatedGoals != null && res.UpdatedGoals.Count > 0)
                {
                    foreach (var updated in res.UpdatedGoals)
                    {
                        var idx = Goals.ToList().FindIndex(g => g.Id == updated.Id);
                        if (idx >= 0)
                        {
                            Goals[idx] = updated;
                        }
                    }
                }

                // Handle delete all or bulk delete
                if (res.Action == "delete_all")
                {
                    Goals.Clear();
                }
                else if (res.DeletedGoalIds != null && res.DeletedGoalIds.Count > 0)
                {
                    foreach (var delId in res.DeletedGoalIds)
                    {
                        var target = Goals.FirstOrDefault(g => g.Id == delId);
                        if (target != null)
                        {
                            Goals.Remove(target);
                        }
                    }
                }

                ApplyFilter();

                var reply = res.AssistantReply;
                if (string.IsNullOrWhiteSpace(reply))
                {
                    reply = "Done! What else would you like to plan?";
                }

                ChatHistory.Add(new ChatMessage("assistant", reply, DateTime.Now));
                AssistantMessage = reply;
                _ = _ttsService?.SpeakAsync(reply);

                // If asking for clarification, keep listening or re-arm mic
                if (res.Action == "ask_clarification" && !_voiceService.IsActive)
                {
                    await Task.Delay(1200); // brief pause to allow TTS to begin
                    await _voiceService.StartSessionAsync();
                }
            }
            else
            {
                var errMsg = "Could not reach the quest service. Please ensure the backend is running.";
                AssistantMessage = errMsg;
                _ = _ttsService?.SpeakAsync(errMsg);
            }
        }
        catch (Exception ex)
        {
            var errMsg = $"Error: {ex.Message}";
            AssistantMessage = errMsg;
            _ = _ttsService?.SpeakAsync("Something went wrong. Please try again.");
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
                Description: null,
                Priority: NewGoalPriority,
                TargetDate: DateTime.UtcNow.Date,
                EstimatedMinutes: NewGoalEstimateMinutes
            );

            var created = await _apiClient.CreateGoalAsync(request);
            if (created != null)
            {
                Goals.Insert(0, created);
                ApplyFilter();
                NewGoalTitle = string.Empty;
                NewGoalEstimateMinutes = 30;
                AssistantMessage = $"Added '{created.Title}' ({created.Priority} priority) to your quest log!";
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
            ToastService.NotifyQuestCompleted(goal.Title);
            await LoadGoalsAsync();
            AssistantMessage = $"🎉 Quest completed: '{goal.Title}'! Experience awarded.";
            _ = _ttsService?.SpeakAsync($"Awesome job completing {goal.Title}!");
        }
    }

    public async Task DeleteGoalAsync(GoalRecord? goal)
    {
        if (goal == null) return;
        bool ok = await _apiClient.DeleteGoalAsync(goal.Id);
        if (ok)
        {
            Goals.Remove(goal);
            ApplyFilter();
            AssistantMessage = $"Removed '{goal.Title}' from quests.";
        }
    }
}
