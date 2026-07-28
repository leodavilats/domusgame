using Domus.Domain.Common;
using Domus.Domain.Rounds;
using Domus.Domain.Seasons;
using Domus.Domain.Settings;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Common;

/// <summary>
/// Leituras compartilhadas entre features (o lado "query" do CQRS logico).
/// Com dezenas de participantes, consulta direta resolve: nada de tabela materializada (RNF-12).
/// </summary>
public sealed class DomusQueries(DomusDbContext db, TimeProvider clock)
{
    public DateTimeOffset Now => clock.GetUtcNow();

    public async Task<GcSettings> GetSettingsAsync(CancellationToken ct = default) =>
        await db.GcSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == GcSettings.SingletonId, ct)
        ?? throw new NotFoundException("Configuracao do GC nao encontrada.");

    public Task<Season?> GetActiveSeasonAsync(CancellationToken ct = default) =>
        db.Seasons.AsNoTracking().SingleOrDefaultAsync(s => s.Status == SeasonStatus.Active, ct);

    /// <summary>Rodada completa (perguntas e alternativas), rastreada para permitir escrita.</summary>
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

    /// <summary>
    /// Rodada em destaque para o participante: a aberta; senao a ultima encerrada;
    /// senao a proxima agendada.
    /// </summary>
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

    /// <summary>Rodadas encerradas consecutivas, da mais recente para tras, com participacao.</summary>
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

    // ------------------------------------------------------------------ rankings

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
                    p.ShowInRanking,
                    a.TotalPoints,
                    a.TotalTimeMs
                })
            .ToListAsync(ct);

        var rows = raw
            .Select(r => new RankingRow(
                r.Id, r.DisplayName, r.AvatarUrl, r.ShowInRanking, r.TotalPoints, r.TotalTimeMs, 1))
            .ToList();

        return Build($"Semana {round.WeekNumber}", "round", rows, meId);
    }

    public async Task<RankingDto> GetSeasonRankingAsync(Season season, Guid meId, CancellationToken ct = default)
    {
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

        // RN-33: quem nao participou aparece com zero, nao some do ranking.
        var participants = await db.Participants.AsNoTracking()
            .Where(p => !p.IsRemoved)
            .Select(p => new { p.Id, p.DisplayName, p.AvatarUrl, p.ShowInRanking })
            .ToListAsync(ct);

        var rows = participants
            .Select(p =>
            {
                byParticipant.TryGetValue(p.Id, out var total);
                return new RankingRow(
                    p.Id, p.DisplayName, p.AvatarUrl, p.ShowInRanking,
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

            // Empate absoluto (mesmos pontos e mesmo tempo) compartilha a posicao.
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

        // RN-22: quem optou por nao aparecer some da lista publica, mas continua vendo sua posicao.
        var hidden = ordered.Where(r => !r.ShowInRanking).Select(r => r.ParticipantId).ToHashSet();
        var visible = entries.Where(e => e.IsMe || !hidden.Contains(e.ParticipantId)).ToList();

        return new RankingDto(scope, title, visible, me);
    }

    private sealed record RankingRow(
        Guid ParticipantId,
        string DisplayName,
        string? AvatarUrl,
        bool ShowInRanking,
        int TotalPoints,
        long TotalTimeMs,
        int RoundsPlayed);
}
