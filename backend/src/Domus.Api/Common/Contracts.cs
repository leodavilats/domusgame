using Domus.Domain.Attempts;
using Domus.Domain.Rounds;

namespace Domus.Api.Common;

public sealed record MyRoomSummaryDto(Guid Id, string Name);

public sealed record MeDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    bool ShowInRanking,
    bool IsAdmin,
    MyRoomSummaryDto? Room);

public sealed record RoundSummaryDto(
    Guid Id,
    Guid SeasonId,
    int WeekNumber,
    string Title,
    RoundAvailability Availability,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    int QuestionCount,
    int MaxPoints,
    int PointsPerCorrectAnswer,
    int MaxSpeedBonus,
    int QuestionTimeLimitSeconds);

public sealed record MyAttemptSummaryDto(
    Guid AttemptId,
    AttemptStatus Status,
    int AnsweredCount,
    int QuestionCount,
    int? TotalPoints,
    int? CorrectCount,
    long? TotalTimeMs,
    int? Position);

public sealed record LessonDto(string Title, string ScriptureReference, string Content, string? ExternalUrl);

public sealed record AttemptQuestionDto(
    Guid Id,
    int Order,
    int TotalQuestions,
    string Text,
    QuestionMediaType MediaType,
    string? MediaUrl,
    IReadOnlyList<AttemptOptionDto> Options,
    int TimeLimitSeconds,
    DateTimeOffset ServedAt,
    DateTimeOffset DeadlineAt,
    DateTimeOffset ServerNow);

public sealed record AttemptOptionDto(Guid Id, string Text);

public sealed record AttemptStateDto(
    Guid AttemptId,
    Guid RoundId,
    AttemptStatus Status,
    int QuestionCount,
    int AnsweredCount,
    AttemptQuestionDto? CurrentQuestion);

public sealed record SubmitAnswerRequest(Guid QuestionId, Guid? SelectedOptionId);

public sealed record SubmitAnswerResponse(
    Guid AnswerId,
    bool TimedOut,
    bool AttemptFinished,
    AttemptQuestionDto? NextQuestion);

public sealed record AttemptResultDto(
    Guid AttemptId,
    RoundSummaryDto Round,
    AttemptStatus Status,
    int TotalPoints,
    int MaxPoints,
    int CorrectCount,
    int QuestionCount,
    long TotalTimeMs,
    bool AnswersRevealed,
    int? Position);

public sealed record ReviewQuestionDto(
    Guid QuestionId,
    int Order,
    string Text,
    QuestionMediaType MediaType,
    string? MediaUrl,
    string? Explanation,
    IReadOnlyList<ReviewOptionDto> Options,
    Guid? SelectedOptionId,
    AnswerOutcome Outcome,
    int Points,
    long ElapsedMs);

public sealed record ReviewOptionDto(Guid Id, string Text, bool IsCorrect);

public sealed record RankingEntryDto(
    int Position,
    Guid ParticipantId,
    string DisplayName,
    string? AvatarUrl,
    int TotalPoints,
    long TotalTimeMs,
    int RoundsPlayed,
    bool IsMe);

public sealed record RankingDto(
    string Scope,
    string Title,
    IReadOnlyList<RankingEntryDto> Entries,
    RankingEntryDto? Me);
