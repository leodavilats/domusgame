using Domus.Domain.Attempts;
using Domus.Domain.Common;
using Domus.Domain.Rounds;
using Domus.Domain.Rooms;
using Domus.Domain.Seasons;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Common;

public sealed class DomusQueries(DomusDbContext db, TimeProvider clock)
{
    public DateTimeOffset Now => clock.GetUtcNow();

    public async Task<Room?> GetMyRoomAsync(Guid participantId, CancellationToken ct = default)
    {
        var roomId = await db.RoomMemberships.AsNoTracking()
            .Where(m => m.ParticipantId == participantId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => m.RoomId)
            .FirstOrDefaultAsync(ct);

        if (roomId == Guid.Empty) return null;

        return await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roomId, ct);
    }

    public async Task<Room> RequireMyRoomAsync(Guid participantId, CancellationToken ct = default) =>
        await GetMyRoomAsync(participantId, ct)
        ?? throw new ForbiddenException("Entre em uma sala com o codigo de convite para continuar.");

    public Task<bool> IsMemberAsync(Guid roomId, Guid participantId, CancellationToken ct = default) =>
        db.RoomMemberships.AsNoTracking()
            .AnyAsync(m => m.RoomId == roomId && m.ParticipantId == participantId, ct);

    public Task<Season?> GetActiveSeasonAsync(Guid roomId, CancellationToken ct = default) =>
        db.Seasons.AsNoTracking()
            .SingleOrDefaultAsync(s => s.RoomId == roomId && s.Status == SeasonStatus.Active, ct);

    public async Task<Round> GetRoundWithQuestionsAsync(Guid roundId, bool tracking, CancellationToken ct = default)
    {
        var query = db.Rounds
            .Include(r => r.Questions)
            .ThenInclude(q => q.Options)
            .AsQueryable();

        if (!tracking) query = query.AsNoTracking();

        return await query.SingleOrDefaultAsync(r => r.Id == roundId, ct)
            ?? throw NotFoundException.For("Rodada");
    }

    public async Task<Round> RequireRoundInMyRoomAsync(
        Guid roundId,
        Guid participantId,
        bool tracking,
        CancellationToken ct = default)
    {
        var round = await GetRoundWithQuestionsAsync(roundId, tracking, ct);
        await EnsureRoundIsInRoomAsync(round, participantId, ct);
        return round;
    }

    public async Task EnsureRoundIsInRoomAsync(Round round, Guid participantId, CancellationToken ct = default)
    {
        var room = await RequireMyRoomAsync(participantId, ct);

        var roomId = await db.Seasons.AsNoTracking()
            .Where(s => s.Id == round.SeasonId)
            .Select(s => s.RoomId)
            .FirstOrDefaultAsync(ct);

        if (roomId != room.Id) throw NotFoundException.For("Rodada");
    }

    public RoundSummaryDto ToSummary(Round round) => new(
        round.Id,
        round.SeasonId,
        round.WeekNumber,
        round.Title,
        round.AvailabilityAt(Now),
        round.OpensAt,
        round.ClosesAt,
        round.Questions.Count,
        round.MaxPoints,
        round.Scoring.PointsPerCorrectAnswer,
        round.Scoring.MaxSpeedBonus,
        round.Scoring.QuestionTimeLimitSeconds);

    public async Task<Round?> GetCurrentRoundAsync(Guid seasonId, CancellationToken ct = default)
    {
        var now = Now;

        var published = await db.Rounds
            .Include(r => r.Questions)
            .AsNoTracking()
            .Where(r => r.SeasonId == seasonId && r.Status == RoundStatus.Published)
            .ToListAsync(ct);

        return published.FirstOrDefault(r => r.IsOpenAt(now))
            ?? published.Where(r => r.IsClosedAt(now)).OrderByDescending(r => r.ClosesAt).FirstOrDefault()
            ?? published.OrderBy(r => r.OpensAt).FirstOrDefault();
    }

    public async Task<int> GetStreakAsync(Guid seasonId, Guid participantId, CancellationToken ct = default)
    {
        var now = Now;

        var closedRounds = await db.Rounds.AsNoTracking()
            .Where(r => r.SeasonId == seasonId && r.Status == RoundStatus.Published && r.ClosesAt < now)
            .OrderByDescending(r => r.ClosesAt)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (closedRounds.Count == 0) return 0;

        var played = await db.Attempts.AsNoTracking()
            .Where(a => a.ParticipantId == participantId && closedRounds.Contains(a.RoundId))
            .Select(a => a.RoundId)
            .ToListAsync(ct);

        var playedSet = played.ToHashSet();
        var streak = 0;

        foreach (var roundId in closedRounds)
        {
            if (!playedSet.Contains(roundId)) break;
            streak++;
        }

        return streak;
    }

    public Task<int> GetSeasonRoundsCountAsync(Guid seasonId, CancellationToken ct = default) =>
        db.Rounds.AsNoTracking()
            .CountAsync(r => r.SeasonId == seasonId && r.Status == RoundStatus.Published, ct);

    public Task<int> GetTotalRoundsPlayedAsync(Guid participantId, CancellationToken ct = default) =>
        db.Attempts.AsNoTracking()
            .CountAsync(a => a.ParticipantId == participantId && a.Status == AttemptStatus.Completed, ct);

    public async Task<Guid?> GetFirstRoundIdAsync(Guid roomId, CancellationToken ct = default)
    {
        var seasonIds = await db.Seasons.AsNoTracking()
            .Where(s => s.RoomId == roomId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        return await db.Rounds.AsNoTracking()
            .Where(r => seasonIds.Contains(r.SeasonId) && r.Status == RoundStatus.Published)
            .OrderBy(r => r.OpensAt)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RankingDto> GetRoundRankingAsync(Round round, Guid meId, CancellationToken ct = default)
    {
        var raw = await db.Attempts.AsNoTracking()
            .Where(a => a.RoundId == round.Id)
            .Join(
                db.Participants.AsNoTracking(),
                a => a.ParticipantId,
                p => p.Id,
                (a, p) => new
                {
                    p.Id,
                    p.DisplayName,
                    p.AvatarUrl,
                    a.TotalPoints,
                    a.TotalTimeMs
                })
            .ToListAsync(ct);

        var rows = raw
            .Select(r => new RankingRow(
                r.Id, r.DisplayName, r.AvatarUrl, r.TotalPoints, r.TotalTimeMs, 1))
            .ToList();

        return Build($"Semana {round.WeekNumber}", "round", rows, meId);
    }

    public async Task<RankingDto> GetSeasonRankingAsync(Season season, Guid meId, CancellationToken ct = default)
    {
        var memberIds = await db.RoomMemberships.AsNoTracking()
            .Where(m => m.RoomId == season.RoomId)
            .Select(m => m.ParticipantId)
            .ToListAsync(ct);

        var now = Now;

        var closedRoundIds = await db.Rounds.AsNoTracking()
            .Where(r => r.SeasonId == season.Id && r.Status == RoundStatus.Published && r.ClosesAt < now)
            .Select(r => r.Id)
            .ToListAsync(ct);

        var totals = await db.Attempts.AsNoTracking()
            .Where(a => closedRoundIds.Contains(a.RoundId))
            .GroupBy(a => a.ParticipantId)
            .Select(g => new
            {
                ParticipantId = g.Key,
                TotalPoints = g.Sum(a => a.TotalPoints),
                TotalTimeMs = g.Sum(a => a.TotalTimeMs),
                RoundsPlayed = g.Count()
            })
            .ToListAsync(ct);

        var byParticipant = totals.ToDictionary(t => t.ParticipantId);

        var participants = await db.Participants.AsNoTracking()
            .Where(p => !p.IsRemoved && memberIds.Contains(p.Id))
            .Select(p => new { p.Id, p.DisplayName, p.AvatarUrl })
            .ToListAsync(ct);

        var rows = participants
            .Select(p =>
            {
                byParticipant.TryGetValue(p.Id, out var total);
                return new RankingRow(
                    p.Id, p.DisplayName, p.AvatarUrl,
                    total?.TotalPoints ?? 0,
                    total?.TotalTimeMs ?? 0,
                    total?.RoundsPlayed ?? 0);
            })
            .ToList();

        return Build(season.Name, "season", rows, meId);
    }

    private static RankingDto Build(string title, string scope, List<RankingRow> rows, Guid meId)
    {
        var ordered = rows
            .OrderByDescending(r => r.TotalPoints)
            .ThenBy(r => r.TotalTimeMs)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = new List<RankingEntryDto>(ordered.Count);
        var position = 0;
        var index = 0;
        RankingRow? previous = null;

        foreach (var row in ordered)
        {
            index++;

            var tied = previous is not null &&
                       previous.TotalPoints == row.TotalPoints &&
                       previous.TotalTimeMs == row.TotalTimeMs;

            if (!tied) position = index;

            entries.Add(new RankingEntryDto(
                position,
                row.ParticipantId,
                row.DisplayName,
                row.AvatarUrl,
                row.TotalPoints,
                row.TotalTimeMs,
                row.RoundsPlayed,
                row.ParticipantId == meId));

            previous = row;
        }

        var me = entries.SingleOrDefault(e => e.IsMe);

        return new RankingDto(scope, title, entries, me);
    }

    private sealed record RankingRow(
        Guid ParticipantId,
        string DisplayName,
        string? AvatarUrl,
        int TotalPoints,
        long TotalTimeMs,
        int RoundsPlayed);
}
