using Domus.Api.Common;
using Domus.Domain.Attempts;
using Domus.Domain.Rounds;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Dashboard;

public sealed record SeasonInfoDto(Guid Id, string Name, DateOnly StartsOn, DateOnly EndsOn);

public sealed record DashboardActionsDto(bool CanStart, bool CanResume, bool CanSeeResult, bool CanReview);

public sealed record MyStatsDto(int SeasonPoints, int? Position, int ParticipantsCount, int Streak, int RoundsPlayed);

public sealed record DashboardDto(
    string GcName,
    SeasonInfoDto? Season,
    RoundSummaryDto? Round,
    string? LessonTitle,
    string? LessonReference,
    MyAttemptSummaryDto? MyAttempt,
    DashboardActionsDto Actions,
    MyStatsDto Stats,
    DateTimeOffset? NextRoundOpensAt,
    DateTimeOffset ServerNow);

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard", GetAsync).RequireAuthorization();
    }

    private static async Task<IResult> GetAsync(
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();
        var now = queries.Now;
        var settings = await queries.GetSettingsAsync(ct);
        var season = await queries.GetActiveSeasonAsync(ct);

        if (season is null)
        {
            return Results.Ok(new DashboardDto(
                settings.GcName, null, null, null, null, null,
                new DashboardActionsDto(false, false, false, false),
                new MyStatsDto(0, null, 0, 0, 0),
                null, now));
        }

        var seasonInfo = new SeasonInfoDto(season.Id, season.Name, season.StartsOn, season.EndsOn);
        var round = await queries.GetCurrentRoundAsync(season.Id, ct);

        var ranking = await queries.GetSeasonRankingAsync(season, meId, ct);
        var me = ranking.Me;
        var streak = await queries.GetStreakAsync(season.Id, meId, ct);

        var stats = new MyStatsDto(
            me?.TotalPoints ?? 0,
            me?.Position,
            ranking.Entries.Count,
            streak,
            me?.RoundsPlayed ?? 0);

        if (round is null)
        {
            return Results.Ok(new DashboardDto(
                settings.GcName, seasonInfo, null, null, null, null,
                new DashboardActionsDto(false, false, false, false),
                stats, null, now));
        }

        var availability = round.AvailabilityAt(now);
        var lessonVisible = availability is RoundAvailability.Open or RoundAvailability.Closed;

        var attempt = await db.Attempts.AsNoTracking()
            .Include(a => a.Answers)
            .SingleOrDefaultAsync(a => a.RoundId == round.Id && a.ParticipantId == meId, ct);

        int? position = null;
        if (availability == RoundAvailability.Closed && attempt is not null)
        {
            var roundRanking = await queries.GetRoundRankingAsync(round, meId, ct);
            position = roundRanking.Me?.Position;
        }

        var attemptSummary = attempt is null
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

        var actions = new DashboardActionsDto(
            CanStart: availability == RoundAvailability.Open && attempt is null,
            CanResume: availability == RoundAvailability.Open && attempt is { Status: AttemptStatus.InProgress },
            CanSeeResult: attempt is not null,
            CanReview: availability == RoundAvailability.Closed);

        var nextRoundOpensAt = await db.Rounds.AsNoTracking()
            .Where(r => r.SeasonId == season.Id && r.Status == RoundStatus.Published && r.OpensAt > now)
            .OrderBy(r => r.OpensAt)
            .Select(r => (DateTimeOffset?)r.OpensAt)
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new DashboardDto(
            settings.GcName,
            seasonInfo,
            queries.ToSummary(round),
            lessonVisible ? round.Lesson.Title : null,
            lessonVisible ? round.Lesson.ScriptureReference : null,
            attemptSummary,
            actions,
            stats,
            nextRoundOpensAt,
            now));
    }
}
