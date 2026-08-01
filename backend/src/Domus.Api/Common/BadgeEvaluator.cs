using Domus.Domain.Attempts;
using Domus.Domain.Badges;
using Domus.Domain.Rounds;
using Domus.Domain.Seasons;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Common;

// Nao ha evento de "rodada fechou"/"temporada fechou" no sistema (fechamento e computado na
// leitura, ver Round.AvailabilityAt / Attempt.CompleteIfRoundClosed). Por isso a avaliacao dos
// selos e feita da mesma forma preguicosa, chamada a partir de MeMapper e do resultado da tentativa.
public sealed class BadgeEvaluator(DomusDbContext db, DomusQueries queries)
{
    public async Task<IReadOnlyList<BadgeCode>> EvaluateAndAwardAsync(
        Guid roomId,
        Guid participantId,
        CancellationToken ct = default)
    {
        var earned = (await db.ParticipantBadges.AsNoTracking()
                .Where(b => b.ParticipantId == participantId)
                .Select(b => b.Code)
                .ToListAsync(ct))
            .ToHashSet();

        var now = queries.Now;
        var newly = new List<ParticipantBadge>();

        void Award(BadgeCode code, Guid? roundId = null, Guid? seasonId = null)
        {
            if (!earned.Add(code)) return;
            newly.Add(ParticipantBadge.Award(roomId, participantId, code, now, roundId, seasonId));
        }

        var attempts = await db.Attempts.AsNoTracking()
            .Where(a => a.ParticipantId == participantId && a.Status == AttemptStatus.Completed)
            .Select(a => new
            {
                a.RoundId,
                a.CorrectCount,
                a.QuestionCount,
                a.TotalTimeMs,
                AvailableMs = (long)a.QuestionCount * a.Scoring.QuestionTimeLimitSeconds * 1000L
            })
            .ToListAsync(ct);

        if (attempts.Count == 0) return newly.Select(b => b.Code).ToList();

        Award(BadgeCode.SarcaArdente);

        foreach (var attempt in attempts)
        {
            if (attempt.QuestionCount == 0 || attempt.CorrectCount != attempt.QuestionCount) continue;

            Award(BadgeCode.TabuasDaLei, attempt.RoundId);

            if (attempt.TotalTimeMs < attempt.AvailableMs * 0.25) Award(BadgeCode.ColunaDeFogo, attempt.RoundId);
            if (attempt.TotalTimeMs > attempt.AvailableMs * 0.8) Award(BadgeCode.AncoraDaEsperanca, attempt.RoundId);
        }

        if (attempts.Count >= 10) Award(BadgeCode.LampadaAcesa10);
        if (attempts.Count >= 25) Award(BadgeCode.LampadaAcesa25);
        if (attempts.Count >= 50) Award(BadgeCode.LampadaAcesa50);

        var playedRoundIds = attempts.Select(a => a.RoundId).ToHashSet();

        var firstRoundId = await queries.GetFirstRoundIdAsync(roomId, ct);
        if (firstRoundId is not null && playedRoundIds.Contains(firstRoundId.Value))
        {
            Award(BadgeCode.PedraAngular, firstRoundId);
        }

        var activeSeason = await queries.GetActiveSeasonAsync(roomId, ct);
        if (activeSeason is not null)
        {
            var streak = await queries.GetStreakAsync(activeSeason.Id, participantId, ct);
            if (streak >= 4) Award(BadgeCode.CestoDeMana, seasonId: activeSeason.Id);
        }

        var closedRoundsPlayed = await db.Rounds.AsNoTracking()
            .Where(r => r.Status == RoundStatus.Published && r.ClosesAt < now && playedRoundIds.Contains(r.Id))
            .ToListAsync(ct);

        foreach (var round in closedRoundsPlayed)
        {
            var ranking = await queries.GetRoundRankingAsync(round, participantId, ct);
            if (ranking.Me is { Position: <= 3 }) Award(BadgeCode.HarpaDeDavi, round.Id);
        }

        var finishedSeasons = await db.Seasons
            .Include(s => s.Podium)
            .AsNoTracking()
            .Where(s => s.RoomId == roomId && s.Status == SeasonStatus.Finished)
            .ToListAsync(ct);

        foreach (var season in finishedSeasons)
        {
            if (season.Podium.Any(p => p.Position == 1 && p.ParticipantId == participantId))
            {
                Award(BadgeCode.CoroaDaVida, seasonId: season.Id);
            }

            var totalRounds = await queries.GetSeasonRoundsCountAsync(season.Id, ct);
            if (totalRounds == 0) continue;

            var playedInSeason = await db.Rounds.AsNoTracking()
                .Where(r => r.SeasonId == season.Id && playedRoundIds.Contains(r.Id))
                .CountAsync(ct);

            if (playedInSeason == totalRounds) Award(BadgeCode.JaquimEBoaz, seasonId: season.Id);
        }

        if (newly.Count > 0)
        {
            db.ParticipantBadges.AddRange(newly);
            await db.SaveChangesAsync(ct);
        }

        return newly.Select(b => b.Code).ToList();
    }

    public async Task<IReadOnlyList<EarnedBadgeDto>> GetEarnedBadgesAsync(
        Guid participantId,
        CancellationToken ct = default)
    {
        var rows = await db.ParticipantBadges.AsNoTracking()
            .Where(b => b.ParticipantId == participantId)
            .OrderBy(b => b.EarnedAt)
            .Select(b => new { b.Code, b.EarnedAt })
            .ToListAsync(ct);

        return rows.Select(r => new EarnedBadgeDto(r.Code.ToString(), r.EarnedAt)).ToList();
    }
}
