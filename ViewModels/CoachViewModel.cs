using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Questlog.Common;
using Questlog.Models;
using Questlog.Services;

namespace Questlog.ViewModels;

public record ChatBubble(string Sender, string Message, bool IsUser, DateTime Timestamp);

public class CoachViewModel : ViewModelBase
{
    private readonly ApiClient _apiClient;

    private string _userInput = string.Empty;
    public string UserInput
    {
        get => _userInput;
        set => SetProperty(ref _userInput, value);
    }

    public ObservableCollection<ChatBubble> Messages { get; } = new();
    public ObservableCollection<InsightRecord> Insights { get; } = new();

    public ICommand SendMessageCommand { get; }
    public ICommand LoadInsightsCommand { get; }

    public CoachViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;

        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync);
        LoadInsightsCommand = new AsyncRelayCommand(LoadInsightsAsync);

        Messages.Add(new ChatBubble(
            Sender: "Dungeon Master",
            Message: "Greetings, adventurer. I observe all deeds and quiet moments in your quest. What guidance or counsel do you seek today?",
            IsUser: false,
            Timestamp: DateTime.Now
        ));
    }

    public override async Task InitializeAsync()
    {
        await LoadInsightsAsync();
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
