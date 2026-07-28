using Domus.Domain.Attempts;
using Domus.Domain.Rounds;

namespace Domus.Api.Common;

/// <summary>Sessao atual.</summary>
public sealed record MeDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    bool ShowInRanking,
    bool IsAdmin,
    string GcName);

/// <summary>Cabecalho de rodada, comum a varias telas. Nunca carrega gabarito.</summary>
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

/// <summary>Resultado do participante em uma rodada.</summary>
public sealed record MyAttemptSummaryDto(
    Guid AttemptId,
    AttemptStatus Status,
    int AnsweredCount,
    int QuestionCount,
    int? TotalPoints,
    int? CorrectCount,
    long? TotalTimeMs,
    int? Position);

/// <summary>Licao da semana.</summary>
public sealed record LessonDto(string Title, string ScriptureReference, string Content, string? ExternalUrl);

/// <summary>Pergunta entregue durante a tentativa. Sem qualquer indicacao de alternativa correta (RNF-02).</summary>
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

/// <summary>Estado da tentativa em andamento. Nao expoe pontos: isso revelaria acertos (RN-21).</summary>
public sealed record AttemptStateDto(
    Guid AttemptId,
    Guid RoundId,
    AttemptStatus Status,
    int QuestionCount,
    int AnsweredCount,
    AttemptQuestionDto? CurrentQuestion);

public sealed record SubmitAnswerRequest(Guid QuestionId, Guid? SelectedOptionId);

/// <summary>Resposta do envio. Deliberadamente nao informa acerto.</summary>
public sealed record SubmitAnswerResponse(
    Guid AnswerId,
    bool TimedOut,
    bool AttemptFinished,
    AttemptQuestionDto? NextQuestion);

/// <summary>Resultado consolidado, exibido apos concluir a tentativa (UC-08).</summary>
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

/// <summary>Revisao pergunta a pergunta, so apos o encerramento (UC-09).</summary>
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
