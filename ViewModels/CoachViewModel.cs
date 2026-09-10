using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Models;
using Questlog.Services;

namespace Questlog.ViewModels;

public record ChatBubble(string Sender, string Message, bool IsUser, DateTime Timestamp);

public class CoachViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;
    private readonly VoiceService _voiceService;

    private string _userInput = string.Empty;
    public string UserInput
    {
        get => _userInput;
        set => SetProperty(ref _userInput, value);
    }

    private string _voiceStateLabel = "🎙 Voice Consult";
    public string VoiceStateLabel
    {
        get => _voiceStateLabel;
        private set => SetProperty(ref _voiceStateLabel, value);
    }

    private bool _isVoiceActive;
    public bool IsVoiceActive
    {
        get => _isVoiceActive;
        private set => SetProperty(ref _isVoiceActive, value);
    }

    private string _partialTranscript = string.Empty;
    public string PartialTranscript
    {
        get => _partialTranscript;
        private set => SetProperty(ref _partialTranscript, value);
    }

    public ObservableCollection<ChatBubble> Messages { get; } = new();
    public ObservableCollection<InsightRecord> Insights { get; } = new();

    public ICommand SendMessageCommand { get; }
    public ICommand LoadInsightsCommand { get; }
    public ICommand ToggleVoiceCommand { get; }
    public ICommand QuickPromptCommand { get; }

    public CoachViewModel(ApiClient apiClient, VoiceService voiceService)
    {
        _apiClient = apiClient;
        _voiceService = voiceService;

        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync);
        LoadInsightsCommand = new AsyncRelayCommand(LoadInsightsAsync);
        ToggleVoiceCommand = new AsyncRelayCommand(ToggleVoiceAsync);
        QuickPromptCommand = new AsyncRelayCommand(async param =>
        {
            if (param is string prompt && !string.IsNullOrWhiteSpace(prompt))
            {
                UserInput = prompt;
                await SendMessageAsync();
            }
        });

        // Wire voice service events
        _voiceService.StateChanged += OnVoiceStateChanged;
        _voiceService.TranscriptPartialReceived += OnPartialTranscript;
        _voiceService.TranscriptFinalReceived += OnFinalTranscript;
        _voiceService.ErrorOccurred += OnVoiceError;

        Messages.Add(new ChatBubble(
            Sender: "Dungeon Master",
            Message: "Greetings, adventurer. I observe all deeds and quiet moments in your quest. Speak or write your itinerary (e.g., 'Schedule 2 hours of coding and 1 hour of DSA') or ask for guidance on your campaign.",
            IsUser: false,
            Timestamp: DateTime.Now
        ));
    }

    public override async Task InitializeAsync()
    {
        await LoadInsightsAsync();
    }

    public async Task ToggleVoiceAsync()
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
            IsVoiceActive = state is VoiceState.Connecting or VoiceState.Listening or VoiceState.Processing;
            VoiceStateLabel = state switch
            {
                VoiceState.Connecting  => "⏳ Connecting...",
                VoiceState.Listening   => "🔴 Listening...",
                VoiceState.Processing  => "⏳ Thinking...",
                VoiceState.Error       => "⚠ Voice Error",
                _                      => "🎙 Voice Consult",
            };

            if (state == VoiceState.Idle)
                PartialTranscript = string.Empty;
        });
    }

    private void OnPartialTranscript(string text)
    {
        Application.Current.Dispatcher.Invoke(() => PartialTranscript = text);
    }

    private void OnFinalTranscript(string text)
    {
        Application.Current.Dispatcher.Invoke(async () =>
        {
            PartialTranscript = string.Empty;
            UserInput = text;
            await SendMessageAsync();
        });
    }

    private void OnVoiceError(string error)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Messages.Add(new ChatBubble(
                Sender: "System",
                Message: $"Voice: {error}",
                IsUser: false,
                Timestamp: DateTime.Now
            ));
        });
    }

    public async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput)) return;

        var message = UserInput.Trim();
        UserInput = string.Empty;

        Messages.Add(new ChatBubble(
            Sender: "You",
            Message: message,
            IsUser: true,
            Timestamp: DateTime.Now
        ));

        IsLoading = true;
        try
        {
            var reply = await _apiClient.ChatWithCoachAsync(message);
            string replyText = string.IsNullOrWhiteSpace(reply)
                ? "The Dungeon Master ponders your words carefully. Continue your focus, for victory favors the disciplined."
                : reply;

            Messages.Add(new ChatBubble(
                Sender: "Dungeon Master",
                Message: replyText,
                IsUser: false,
                Timestamp: DateTime.Now
            ));
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatBubble(
                Sender: "System",
                Message: $"Failed to consult coach: {ex.Message}",
                IsUser: false,
                Timestamp: DateTime.Now
            ));
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadInsightsAsync()
    {
        try
        {
            var list = await _apiClient.GetInsightsAsync();
            Insights.Clear();
            foreach (var insight in list)
            {
                Insights.Add(insight);
            }
        }
        catch { }
    }
}
