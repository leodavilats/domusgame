using Domus.Api.Common;
using Domus.Domain.Attempts;
using Domus.Domain.Common;
using Domus.Domain.Rounds;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Rounds;

public sealed record RoundListItemDto(RoundSummaryDto Round, MyAttemptSummaryDto? MyAttempt);

public sealed record RoundDetailDto(
    RoundSummaryDto Round,
    LessonDto? Lesson,
    MyAttemptSummaryDto? MyAttempt,
    DateTimeOffset ServerNow);

public sealed record RoundReviewDto(
    RoundSummaryDto Round,
    LessonDto Lesson,
    int TotalPoints,
    int MaxPoints,
    int CorrectCount,
    long TotalTimeMs,
    IReadOnlyList<ReviewQuestionDto> Questions);

public static class RoundEndpoints
{
    public static void MapRoundEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rounds").RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/review", ReviewAsync);
    }

    private static async Task<IResult> ListAsync(
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        Guid? seasonId,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();

        var targetSeasonId = seasonId ?? (await queries.GetActiveSeasonAsync(ct))?.Id;
        if (targetSeasonId is null) return Results.Ok(Array.Empty<RoundListItemDto>());

        var rounds = await db.Rounds.AsNoTracking()
            .Include(r => r.Questions)
            .Where(r => r.SeasonId == targetSeasonId && r.Status == RoundStatus.Published)
            .OrderByDescending(r => r.WeekNumber)
            .ToListAsync(ct);

        var roundIds = rounds.Select(r => r.Id).ToList();

        var attempts = await db.Attempts.AsNoTracking()
            .Include(a => a.Answers)
            .Where(a => a.ParticipantId == meId && roundIds.Contains(a.RoundId))
            .ToListAsync(ct);

        var items = rounds.Select(round =>
        {
            var attempt = attempts.SingleOrDefault(a => a.RoundId == round.Id);
            return new RoundListItemDto(queries.ToSummary(round), ToSummary(attempt, null));
        });

        return Results.Ok(items);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();
        var round = await queries.GetRoundWithQuestionsAsync(id, tracking: false, ct);
        var availability = round.AvailabilityAt(queries.Now);

        if (availability == RoundAvailability.Draft && !currentUser.IsAdmin)
        {
            throw NotFoundException.For("Rodada");
        }

        var attempt = await db.Attempts.AsNoTracking()
            .Include(a => a.Answers)
            .SingleOrDefaultAsync(a => a.RoundId == round.Id && a.ParticipantId == meId, ct);

        var lessonVisible = availability is RoundAvailability.Open or RoundAvailability.Closed || currentUser.IsAdmin;

        var lesson = lessonVisible
            ? new LessonDto(
                round.Lesson.Title,
                round.Lesson.ScriptureReference,
                round.Lesson.Content,
                round.Lesson.ExternalUrl)
            : null;

        return Results.Ok(new RoundDetailDto(
            queries.ToSummary(round),
            lesson,
            ToSummary(attempt, null),
            queries.Now));
    }

    private static async Task<IResult> ReviewAsync(
        Guid id,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();
        var round = await queries.GetRoundWithQuestionsAsync(id, tracking: false, ct);

        if (!round.IsAnswerRevealedAt(queries.Now))
        {
            throw new ForbiddenException("O gabarito fica disponivel quando a rodada encerrar.");
        }

        var attempt = await db.Attempts.AsNoTracking()
            .Include(a => a.Answers)
            .SingleOrDefaultAsync(a => a.RoundId == round.Id && a.ParticipantId == meId, ct);

        var answers = attempt?.Answers.ToDictionary(a => a.QuestionId) ?? [];

        var questions = round.OrderedQuestions.Select(question =>
        {
            answers.TryGetValue(question.Id, out var answer);

            return new ReviewQuestionDto(
                question.Id,
                question.Order,
                question.Text,
                question.MediaType,
                question.MediaUrl,
                question.Explanation,
                [.. question.Options
                    .OrderBy(o => o.Order)
                    .Select(o => new ReviewOptionDto(o.Id, o.Text, o.IsCorrect))],
                answer?.SelectedOptionId,
                answer?.Outcome ?? AnswerOutcome.Pending,
                answer?.Points ?? 0,
                answer?.ElapsedMs ?? 0);
        }).ToList();

        return Results.Ok(new RoundReviewDto(
            queries.ToSummary(round),
            new LessonDto(
                round.Lesson.Title,
                round.Lesson.ScriptureReference,
                round.Lesson.Content,
                round.Lesson.ExternalUrl),
            attempt?.TotalPoints ?? 0,
            round.MaxPoints,
            attempt?.CorrectCount ?? 0,
            attempt?.TotalTimeMs ?? 0,
            questions));
    }

    internal static MyAttemptSummaryDto? ToSummary(Attempt? attempt, int? position) =>
        attempt is null
            ? null
            : new MyAttemptSummaryDto(
                attempt.Id,
                attempt.Status,
                attempt.AnsweredCount,
                attempt.QuestionCount,
                attempt.IsFinished ? attempt.TotalPoints : null,
                attempt.IsFinished ? attempt.CorrectCount : null,
                attempt.IsFinished ? attempt.TotalTimeMs : null,
                position);
}
