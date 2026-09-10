using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Questlog.Models;

public record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("user")] UserRecord? User
);

public record UserRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("name")] string Name
);

public record ActivityRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("app_name")] string AppName,
    [property: JsonPropertyName("window_title")] string? WindowTitle,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("start_time")] DateTime StartTime,
    [property: JsonPropertyName("end_time")] DateTime EndTime,
    [property: JsonPropertyName("duration_seconds")] int DurationSeconds,
    [property: JsonPropertyName("is_productive")] bool IsProductive,
    [property: JsonPropertyName("is_ignored")] bool IsIgnored
);

public record ActivityCreate(
    [property: JsonPropertyName("application")] string? AppName,
    [property: JsonPropertyName("window_title")] string? WindowTitle,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("started_at")] DateTime StartTime,
    [property: JsonPropertyName("ended_at")] DateTime EndTime,
    [property: JsonPropertyName("duration_seconds")] int DurationSeconds,
    [property: JsonPropertyName("is_productive")] bool IsProductive,
    [property: JsonPropertyName("source")] string Source = "desktop",
    [property: JsonPropertyName("is_planned")] bool IsPlanned = false
);

public record GoalRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("target_date")] DateTime? TargetDate,
    [property: JsonPropertyName("estimated_minutes")] int? EstimatedMinutes,
    [property: JsonPropertyName("is_completed")] bool IsCompleted,
    [property: JsonPropertyName("completed_at")] DateTime? CompletedAt
);

public record GoalCreate(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("priority")] string Priority = "medium",
    [property: JsonPropertyName("target_date")] DateTime? TargetDate = null,
    [property: JsonPropertyName("estimated_minutes")] int? EstimatedMinutes = null
);

public record ConversationGoalResponse(
    [property: JsonPropertyName("created_goals")] List<GoalRecord> CreatedGoals,
    [property: JsonPropertyName("assistant_reply")] string AssistantReply
);

/// <summary>One bubble in the voice chat UI.</summary>
public record ChatMessage(string Role, string Text, DateTime Timestamp)
{
    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
}

/// <summary>Sent to backend as conversation history context.</summary>
public record ConversationTurn(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("text")] string Text
);

public record ScheduleItemRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("goal_id")] Guid? GoalId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("start_time")] DateTime StartTime,
    [property: JsonPropertyName("end_time")] DateTime EndTime,
    [property: JsonPropertyName("duration_minutes")] int DurationMinutes,
    [property: JsonPropertyName("is_completed")] bool IsCompleted,
    [property: JsonPropertyName("sort_order")] int SortOrder
);

public record DailyScoreRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("date")] DateTime Date,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("previous_score")] int? PreviousScore,
    [property: JsonPropertyName("streak_days")] int StreakDays,
    [property: JsonPropertyName("total_active_minutes")] int TotalActiveMinutes,
    [property: JsonPropertyName("productive_minutes")] int ProductiveMinutes
);

public record ScoreEventRecord(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("event_type")] string EventType,
    [property: JsonPropertyName("delta")] int Delta,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt
);

public record DailyAnalyticsRecord(
    [property: JsonPropertyName("date")] DateTime Date,
    [property: JsonPropertyName("total_minutes")] int TotalMinutes,
    [property: JsonPropertyName("productive_minutes")] int ProductiveMinutes,
    [property: JsonPropertyName("unproductive_minutes")] int UnproductiveMinutes,
    [property: JsonPropertyName("category_breakdown")] Dictionary<string, int>? CategoryBreakdown,
    [property: JsonPropertyName("productivity_percentage")] double ProductivityPercentage
);

public record WeeklyAnalyticsRecord(
    [property: JsonPropertyName("start_date")] DateTime StartDate,
    [property: JsonPropertyName("end_date")] DateTime EndDate,
    [property: JsonPropertyName("total_minutes")] int TotalMinutes,
    [property: JsonPropertyName("productive_minutes")] int ProductiveMinutes,
    [property: JsonPropertyName("daily_scores")] List<int>? DailyScores,
    [property: JsonPropertyName("top_apps")] List<AppUsageStat>? TopApps
);

public record AppUsageStat(
    [property: JsonPropertyName("app_name")] string AppName,
    [property: JsonPropertyName("minutes")] int Minutes,
    [property: JsonPropertyName("category")] string Category
);

public record ChatResponse(
    [property: JsonPropertyName("reply")] string Reply
);

public record InsightRecord(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("priority")] string Priority,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt
);

public record InsightsResponse(
    [property: JsonPropertyName("insights")] List<InsightRecord> Insights
);

public record VoiceTokenResponse(
    [property: JsonPropertyName("token")] string Token
);
