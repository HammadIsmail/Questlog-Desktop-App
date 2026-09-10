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

    // ── Manual Goal Forge Fields ──
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

    // ── Voice Assistant State ──
    private bool _isVoicePanelOpen;
    public bool IsVoicePanelOpen
    {
        get => _isVoicePanelOpen;
        set => SetProperty(ref _isVoicePanelOpen, value);
    }

    private bool _isVoiceListening;
    public bool IsVoiceListening
    {
        get => _isVoiceListening;
        set => SetProperty(ref _isVoiceListening, value);
    }

    private string _voiceStatusText = "🎙️ Voice Assistant Ready. Speak your plan.";
    public string VoiceStatusText
    {
        get => _voiceStatusText;
        set => SetProperty(ref _voiceStatusText, value);
    }

    private string _spokenTranscript = string.Empty;
    public string SpokenTranscript
    {
        get => _spokenTranscript;
        set => SetProperty(ref _spokenTranscript, value);
    }

    private string _voiceInputText = string.Empty;
    public string VoiceInputText
    {
        get => _voiceInputText;
        set => SetProperty(ref _voiceInputText, value);
    }

    private string? _assistantMessage;
    public string? AssistantMessage
    {
        get => _assistantMessage;
        set
        {
            if (SetProperty(ref _assistantMessage, value))
            {
                OnPropertyChanged(nameof(HasAssistantMessage));
            }
        }
    }
    public bool HasAssistantMessage => !string.IsNullOrWhiteSpace(_assistantMessage);

    public ObservableCollection<GoalRecord> Goals { get; } = new();
    public ObservableCollection<ChatMessage> ChatHistory { get; } = new();


    public ICommand LoadGoalsCommand { get; }
    public ICommand CreateGoalCommand { get; }
    public ICommand CompleteGoalCommand { get; }
    public ICommand DeleteGoalCommand { get; }
    public ICommand ToggleVoicePanelCommand { get; }
    public ICommand ToggleVoiceListeningCommand { get; }
    public ICommand SubmitVoicePlanCommand { get; }
    public ICommand QuickSpokenPromptCommand { get; }

    public GoalsViewModel(ApiClient apiClient, VoiceService voiceService, TtsService? ttsService = null)
    {
        _apiClient = apiClient;
        _voiceService = voiceService;
        _ttsService = ttsService;

        LoadGoalsCommand = new AsyncRelayCommand(LoadGoalsAsync);
        CreateGoalCommand = new AsyncRelayCommand(CreateGoalAsync);
        CompleteGoalCommand = new AsyncRelayCommand(param => CompleteGoalAsync(param as GoalRecord));
        DeleteGoalCommand = new AsyncRelayCommand(param => DeleteGoalAsync(param as GoalRecord));
        ToggleVoicePanelCommand = new AsyncRelayCommand(ToggleVoicePanelAsync);
        ToggleVoiceListeningCommand = new AsyncRelayCommand(ToggleVoiceListeningAsync);
        SubmitVoicePlanCommand = new AsyncRelayCommand(SubmitVoicePlanAsync);
        QuickSpokenPromptCommand = new AsyncRelayCommand(async param =>
        {
            if (param is string prompt && !string.IsNullOrWhiteSpace(prompt))
            {
                VoiceInputText = prompt;
                await AddGoalsFromTextAsync(prompt);
            }
        });

        // Wire voice events
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

    public async Task ToggleVoicePanelAsync()
    {
        IsVoicePanelOpen = !IsVoicePanelOpen;
        if (IsVoicePanelOpen)
        {
            if (ChatHistory.Count == 0)
            {
                var greeting = "Hey! I'm your Quest Master. Tell me about your plan for today. Just speak naturally! For example: I want to do 2 hours of DSA, then 1 hour of project work. I'll add them to your list!";
                ChatHistory.Add(new ChatMessage("assistant", "👋 " + greeting, DateTime.Now));
                AssistantMessage = ChatHistory.Last().Text;
                _ = _ttsService?.SpeakAsync(greeting);
            }
            else
            {
                AssistantMessage = ChatHistory.Last().Text;
            }
            if (!_voiceService.IsActive)
            {
                await _voiceService.StartSessionAsync();
            }
        }
        else
        {
            _ttsService?.Stop();
            if (_voiceService.IsActive)
            {
                await _voiceService.StopSessionAsync();
            }
        }
    }

    public async Task ToggleVoiceListeningAsync()
    {
        if (_voiceService.IsActive)
        {
            await _voiceService.StopSessionAsync();
        }
        else
        {
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
                VoiceState.Listening => "🔴 Listening... Tell me your plan of the day",
                VoiceState.Processing => "⏳ Processing speech...",
                VoiceState.Error => "⚠️ Voice connection issue",
                _ => "🎙️ Microphone idle. Click to speak."
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
            await AddGoalsFromTextAsync(text);
        });
    }

    private void OnVoiceError(string err)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            AssistantMessage = $"Voice error: {err}. You can also type your plan below.";
        });
    }

    public async Task SubmitVoicePlanAsync()
    {
        if (string.IsNullOrWhiteSpace(VoiceInputText)) return;
        var text = VoiceInputText;
        VoiceInputText = string.Empty;
        await AddGoalsFromTextAsync(text);
    }

    public async Task AddGoalsFromTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Add user bubble
        ChatHistory.Add(new ChatMessage("user", text, DateTime.Now));

        // Build history context for the LLM (last 8 turns)
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
                if (res.CreatedGoals != null && res.CreatedGoals.Count > 0)
                {
                    foreach (var g in res.CreatedGoals)
                        Goals.Insert(0, g);
                }
                var reply = res.AssistantReply ?? "Got it! What else would you like to add?";
                ChatHistory.Add(new ChatMessage("assistant", reply, DateTime.Now));
                AssistantMessage = reply;
                _ = _ttsService?.SpeakAsync(reply);
            }
            else
            {
                var errMsg = "Hmm, I had trouble connecting. Make sure the backend is running and try again!";
                ChatHistory.Add(new ChatMessage("assistant", errMsg, DateTime.Now));
                AssistantMessage = errMsg;
                _ = _ttsService?.SpeakAsync(errMsg);
            }
        }
        catch (Exception ex)
        {
            var errMsg = $"Something went wrong: {ex.Message}";
            ChatHistory.Add(new ChatMessage("assistant", errMsg, DateTime.Now));
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
            ToastService.NotifyQuestCompleted(goal.Title);
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
