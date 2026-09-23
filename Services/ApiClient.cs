using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Questlog.Models;

namespace Questlog.Services;

/// <summary>
/// HTTP client wrapper for all FastAPI endpoints.
/// All methods return null on failure — UI should handle gracefully.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private string? _accessToken;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ApiClient(HttpClient http)
    {
        _http = http;
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);
    public string? CurrentUserEmail { get; private set; }
    public string? CurrentUserName { get; private set; }
    public event Action? AuthStateChanged;

    public void SetToken(string token)
    {
        _accessToken = token;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public void Logout()
    {
        _accessToken = null;
        CurrentUserEmail = null;
        CurrentUserName = null;
        _http.DefaultRequestHeaders.Authorization = null;
        TokenStore.Clear();
        AuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Called at app startup — silently restores a saved session so the user
    /// doesn't have to log in again after closing the app.
    /// Returns true if a valid saved token was found.
    /// </summary>
    public bool TryRestoreSession()
    {
        var saved = TokenStore.TryLoad();
        if (saved == null) return false;

        SetToken(saved.Value.Token);
        CurrentUserEmail = saved.Value.Email;
        CurrentUserName = saved.Value.Name;
        AuthStateChanged?.Invoke();
        return true;
    }

    // ──────── Auth ────────
    public async Task<TokenResponse?> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/auth/login",
                new { email, password });
            if (!resp.IsSuccessStatusCode) return null;
            var result = await resp.Content.ReadFromJsonAsync<TokenResponse>(JsonOpts);
            if (result != null)
            {
                SetToken(result.AccessToken);
                CurrentUserEmail = result.User?.Email ?? email;
                CurrentUserName = result.User?.Name ?? "Adventurer";
                TokenStore.Save(result.AccessToken, CurrentUserEmail, CurrentUserName);
                AuthStateChanged?.Invoke();
            }
            return result;
        }
        catch { return null; }
    }

    public async Task<TokenResponse?> RegisterAsync(string email, string name, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/auth/register",
                new { email, name, password });
            if (!resp.IsSuccessStatusCode) return null;
            var result = await resp.Content.ReadFromJsonAsync<TokenResponse>(JsonOpts);
            if (result != null)
            {
                SetToken(result.AccessToken);
                CurrentUserEmail = result.User?.Email ?? email;
                CurrentUserName = result.User?.Name ?? name;
                TokenStore.Save(result.AccessToken, CurrentUserEmail, CurrentUserName);
                AuthStateChanged?.Invoke();
            }
            return result;
        }
        catch { return null; }
    }

    /// <summary>
    /// Validates the current session against /api/v1/auth/me.
    /// If invalid or expired (401), automatically clears tokens and logs out.
    /// </summary>
    public async Task<bool> ValidateSessionAsync()
    {
        if (!IsAuthenticated) return false;
        try
        {
            var resp = await _http.GetAsync("/api/v1/auth/me");
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Logout();
                return false;
            }
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Quick guest login for one-click testing or default adventurers.
    /// </summary>
    public async Task<bool> QuickLoginGuestAsync()
    {
        const string defaultEmail = "adventurer@questlog.com";
        const string defaultPass = "AdventurerPass123!";
        const string defaultName = "Adventurer";

        var login = await LoginAsync(defaultEmail, defaultPass);
        if (login != null) return true;

        var reg = await RegisterAsync(defaultEmail, defaultName, defaultPass);
        return reg != null;
    }

    /// <summary>
    /// Ensures client has token.
    /// </summary>
    public Task<bool> EnsureAuthenticatedAsync()
    {
        return Task.FromResult(IsAuthenticated);
    }

    private async Task<HttpResponseMessage?> SendWithAuthCheckAsync(Func<Task<HttpResponseMessage>> sendFunc)
    {
        try
        {
            var resp = await sendFunc();
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Logout();
                return null;
            }
            return resp;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Logout();
            return null;
        }
        catch
        {
            return null;
        }
    }

    // ──────── Activities ────────
    public async Task<List<ActivityRecord>> GetTodayActivitiesAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/activities/today"));
        if (resp == null || !resp.IsSuccessStatusCode) return [];
        try
        {
            return await resp.Content.ReadFromJsonAsync<List<ActivityRecord>>(JsonOpts) ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> BatchIngestActivitiesAsync(List<ActivityCreate> activities)
    {
        var resp = await SendWithAuthCheckAsync(() => _http.PostAsJsonAsync("/api/v1/activities/batch",
            new { activities }, JsonOpts));
        return resp?.IsSuccessStatusCode ?? false;
    }

    // ──────── Goals ────────
    public async Task<List<GoalRecord>> GetTodayGoalsAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/goals/today"));
        if (resp == null || !resp.IsSuccessStatusCode) return [];
        try
        {
            return await resp.Content.ReadFromJsonAsync<List<GoalRecord>>(JsonOpts) ?? [];
        }
        catch { return []; }
    }

    public async Task<List<GoalRecord>> GetAllGoalsAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/goals"));
        if (resp == null || !resp.IsSuccessStatusCode) return [];
        try
        {
            return await resp.Content.ReadFromJsonAsync<List<GoalRecord>>(JsonOpts) ?? [];
        }
        catch { return []; }
    }

    public async Task<ConversationGoalResponse?> CreateGoalsFromConversationAsync(
        string text, IEnumerable<ConversationTurn>? history = null)
    {
        var resp = await SendWithAuthCheckAsync(() => _http.PostAsJsonAsync("/api/v1/goals/from-conversation",
            new { text, conversation_history = history ?? [] }, JsonOpts));
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            return await resp.Content.ReadFromJsonAsync<ConversationGoalResponse>(JsonOpts);
        }
        catch { return null; }
    }

    public async Task<GoalRecord?> CreateGoalAsync(GoalCreate goal)
    {
        var resp = await SendWithAuthCheckAsync(() => _http.PostAsJsonAsync("/api/v1/goals", goal, JsonOpts));
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            return await resp.Content.ReadFromJsonAsync<GoalRecord>(JsonOpts);
        }
        catch { return null; }
    }

    public async Task<bool> CompleteGoalAsync(Guid goalId)
    {
        var resp = await SendWithAuthCheckAsync(() => _http.PostAsync($"/api/v1/goals/{goalId}/complete", null));
        return resp?.IsSuccessStatusCode ?? false;
    }

    public async Task<bool> DeleteGoalAsync(Guid goalId)
    {
        var resp = await SendWithAuthCheckAsync(() => _http.DeleteAsync($"/api/v1/goals/{goalId}"));
        return resp?.IsSuccessStatusCode ?? false;
    }

    // ──────── Schedule ────────
    public async Task<List<ScheduleItemRecord>> GetTodayScheduleAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/schedule/today"));
        if (resp == null || !resp.IsSuccessStatusCode) return [];
        try
        {
            return await resp.Content.ReadFromJsonAsync<List<ScheduleItemRecord>>(JsonOpts) ?? [];
        }
        catch { return []; }
    }

    public async Task<List<ScheduleItemRecord>> GenerateScheduleAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.PostAsJsonAsync("/api/v1/schedule/generate", new { }));
        if (resp == null || !resp.IsSuccessStatusCode) return [];
        try
        {
            return await resp.Content.ReadFromJsonAsync<List<ScheduleItemRecord>>(JsonOpts) ?? [];
        }
        catch { return []; }
    }

    // ──────── Score ────────
    public async Task<DailyScoreRecord?> GetTodayScoreAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/score/today"));
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            return await resp.Content.ReadFromJsonAsync<DailyScoreRecord>(JsonOpts);
        }
        catch { return null; }
    }

    public async Task<List<ScoreEventRecord>> GetScoreEventsAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/score/events"));
        if (resp == null || !resp.IsSuccessStatusCode) return [];
        try
        {
            return await resp.Content.ReadFromJsonAsync<List<ScoreEventRecord>>(JsonOpts) ?? [];
        }
        catch { return []; }
    }

    public async Task<List<DailyScoreRecord>> GetScoreHistoryAsync(int days = 7)
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync($"/api/v1/score/history?days={days}"));
        if (resp == null || !resp.IsSuccessStatusCode) return [];
        try
        {
            return await resp.Content.ReadFromJsonAsync<List<DailyScoreRecord>>(JsonOpts) ?? [];
        }
        catch { return []; }
    }

    // ──────── Analytics ────────
    public async Task<DailyAnalyticsRecord?> GetTodayAnalyticsAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/analytics/today"));
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            return await resp.Content.ReadFromJsonAsync<DailyAnalyticsRecord>(JsonOpts);
        }
        catch { return null; }
    }

    public async Task<WeeklyAnalyticsRecord?> GetWeeklyAnalyticsAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/analytics/week"));
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            return await resp.Content.ReadFromJsonAsync<WeeklyAnalyticsRecord>(JsonOpts);
        }
        catch { return null; }
    }

    // ──────── AI Coach ────────
    public async Task<string?> ChatWithCoachAsync(string message)
    {
        var resp = await SendWithAuthCheckAsync(() => _http.PostAsJsonAsync("/api/v1/ai/chat",
            new { message }, JsonOpts));
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            var result = await resp.Content.ReadFromJsonAsync<ChatResponse>(JsonOpts);
            return result?.Reply;
        }
        catch { return null; }
    }

    public async Task<List<InsightRecord>> GetInsightsAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/ai/insights"));
        if (resp == null || !resp.IsSuccessStatusCode) return [];
        try
        {
            var result = await resp.Content.ReadFromJsonAsync<InsightsResponse>(JsonOpts);
            return result?.Insights ?? [];
        }
        catch { return []; }
    }

    // ──────── Voice ────────
    public async Task<string?> GetVoiceTokenAsync()
    {
        var resp = await SendWithAuthCheckAsync(() => _http.GetAsync("/api/v1/voice/token"));
        if (resp == null || !resp.IsSuccessStatusCode) return null;
        try
        {
            var result = await resp.Content.ReadFromJsonAsync<VoiceTokenResponse>(JsonOpts);
            return result?.Token;
        }
        catch { return null; }
    }

    public async Task<List<ScheduleItemRecord>> ShiftScheduleAsync(Guid itemId, int overrunMinutes)
    {
        var resp = await SendWithAuthCheckAsync(() => _http.PostAsJsonAsync("/api/v1/schedule/shift",
            new { item_id = itemId, overrun_minutes = overrunMinutes }, JsonOpts));
        if (resp == null || !resp.IsSuccessStatusCode) return [];
        try
        {
            return await resp.Content.ReadFromJsonAsync<List<ScheduleItemRecord>>(JsonOpts) ?? [];
        }
        catch { return []; }
    }

    // ──────── Health ────────
    public async Task<bool> IsBackendAliveAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/v1/health");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
