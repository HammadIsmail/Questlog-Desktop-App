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

    public bool IsAuthenticated => _accessToken != null;

    public void SetToken(string token)
    {
        _accessToken = token;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
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
            if (result != null) SetToken(result.AccessToken);
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
            if (result != null) SetToken(result.AccessToken);
            return result;
        }
        catch { return null; }
    }

    /// <summary>
    /// Ensures the client has a valid JWT session by auto-authenticating the local desktop user.
    /// </summary>
    public async Task<bool> EnsureAuthenticatedAsync()
    {
        if (IsAuthenticated) return true;

        const string defaultEmail = "adventurer@questlog.com";
        const string defaultPass = "AdventurerPass123!";
        const string defaultName = "Adventurer";

        var login = await LoginAsync(defaultEmail, defaultPass);
        if (login != null) return true;

        var reg = await RegisterAsync(defaultEmail, defaultName, defaultPass);
        return reg != null;
    }

    // ──────── Activities ────────
    public async Task<List<ActivityRecord>> GetTodayActivitiesAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<ActivityRecord>>("/api/v1/activities/today", JsonOpts);
            return result ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> BatchIngestActivitiesAsync(List<ActivityCreate> activities)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/activities/batch",
                new { activities }, JsonOpts);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ──────── Goals ────────
    public async Task<List<GoalRecord>> GetTodayGoalsAsync()
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var result = await _http.GetFromJsonAsync<List<GoalRecord>>("/api/v1/goals/today", JsonOpts);
            return result ?? [];
        }
        catch { return []; }
    }

    public async Task<List<GoalRecord>> GetAllGoalsAsync()
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var result = await _http.GetFromJsonAsync<List<GoalRecord>>("/api/v1/goals", JsonOpts);
            return result ?? [];
        }
        catch { return []; }
    }

    public async Task<GoalRecord?> CreateGoalAsync(GoalCreate goal)
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var resp = await _http.PostAsJsonAsync("/api/v1/goals", goal, JsonOpts);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<GoalRecord>(JsonOpts);
        }
        catch { return null; }
    }

    public async Task<bool> CompleteGoalAsync(Guid goalId)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/v1/goals/{goalId}/complete", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteGoalAsync(Guid goalId)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/v1/goals/{goalId}");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ──────── Schedule ────────
    public async Task<List<ScheduleItemRecord>> GetTodayScheduleAsync()
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var result = await _http.GetFromJsonAsync<List<ScheduleItemRecord>>("/api/v1/schedule/today", JsonOpts);
            return result ?? [];
        }
        catch { return []; }
    }

    public async Task<List<ScheduleItemRecord>> GenerateScheduleAsync()
    {
        try
        {
            await EnsureAuthenticatedAsync();
            var resp = await _http.PostAsJsonAsync("/api/v1/schedule/generate", new { });
            if (!resp.IsSuccessStatusCode) return [];
            return await resp.Content.ReadFromJsonAsync<List<ScheduleItemRecord>>(JsonOpts) ?? [];
        }
        catch { return []; }
    }

    // ──────── Score ────────
    public async Task<DailyScoreRecord?> GetTodayScoreAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<DailyScoreRecord>("/api/v1/score/today", JsonOpts);
        }
        catch { return null; }
    }

    public async Task<List<ScoreEventRecord>> GetScoreEventsAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<ScoreEventRecord>>("/api/v1/score/events", JsonOpts);
            return result ?? [];
        }
        catch { return []; }
    }

    public async Task<List<DailyScoreRecord>> GetScoreHistoryAsync(int days = 7)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<DailyScoreRecord>>($"/api/v1/score/history?days={days}", JsonOpts);
            return result ?? [];
        }
        catch { return []; }
    }

    // ──────── Analytics ────────
    public async Task<DailyAnalyticsRecord?> GetTodayAnalyticsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<DailyAnalyticsRecord>("/api/v1/analytics/today", JsonOpts);
        }
        catch { return null; }
    }

    public async Task<WeeklyAnalyticsRecord?> GetWeeklyAnalyticsAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<WeeklyAnalyticsRecord>("/api/v1/analytics/week", JsonOpts);
        }
        catch { return null; }
    }

    // ──────── AI Coach ────────
    public async Task<string?> ChatWithCoachAsync(string message)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/ai/chat",
                new { message }, JsonOpts);
            if (!resp.IsSuccessStatusCode) return null;
            var result = await resp.Content.ReadFromJsonAsync<ChatResponse>(JsonOpts);
            return result?.Reply;
        }
        catch { return null; }
    }

    public async Task<List<InsightRecord>> GetInsightsAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<InsightsResponse>("/api/v1/ai/insights", JsonOpts);
            return result?.Insights ?? [];
        }
        catch { return []; }
    }

    // ──────── Voice ────────
    public async Task<string?> GetVoiceTokenAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<VoiceTokenResponse>("/api/v1/voice/token", JsonOpts);
            return result?.Token;
        }
        catch { return null; }
    }

    public async Task<List<ScheduleItemRecord>> ShiftScheduleAsync(Guid itemId, int overrunMinutes)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/schedule/shift",
                new { item_id = itemId, overrun_minutes = overrunMinutes }, JsonOpts);
            if (!resp.IsSuccessStatusCode) return [];
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
