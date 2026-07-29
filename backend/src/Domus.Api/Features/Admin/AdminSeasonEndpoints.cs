using System.Text;
using Domus.Api.Common;
using Domus.Domain.Common;
using Domus.Domain.Rounds;
using Domus.Domain.Seasons;
using Domus.Domain.Settings;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Admin;

public sealed record SeasonRequest(string Name, DateOnly StartsOn, DateOnly EndsOn);

public sealed record AdminSeasonDto(
    Guid Id,
    string Name,
    DateOnly StartsOn,
    DateOnly EndsOn,
    SeasonStatus Status,
    int RoundCount,
    int PublishedRoundCount,
    IReadOnlyList<PodiumEntryDto> Podium);

public sealed record PodiumEntryDto(int Position, string DisplayName, int TotalPoints, long TotalTimeMs);

public static class AdminSeasonEndpoints
{
    public static void MapAdminSeasonEndpoints(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/seasons");

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/activate", ActivateAsync);
        group.MapPost("/{id:guid}/finish", FinishAsync);
        group.MapGet("/{id:guid}/export", ExportAsync);
    }

    private static async Task<IResult> ListAsync(
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var room = await queries.RequireMyRoomAsync(currentUser.RequireAdminId(), ct);

        var seasons = await db.Seasons.AsNoTracking()
            .Include(s => s.Podium)
            .Where(s => s.RoomId == room.Id)
            .OrderByDescending(s => s.StartsOn)
            .ToListAsync(ct);

        var counts = await db.Rounds.AsNoTracking()
            .GroupBy(r => r.SeasonId)
            .Select(g => new
            {
                SeasonId = g.Key,
                Total = g.Count(),
                Published = g.Count(r => r.Status == RoundStatus.Published)
            })
            .ToListAsync(ct);

        var items = seasons.Select(season =>
        {
            var count = counts.SingleOrDefault(c => c.SeasonId == season.Id);
            return ToDto(season, count?.Total ?? 0, count?.Published ?? 0);
        });

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateAsync(
        SeasonRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var room = await queries.RequireMyRoomAsync(currentUser.RequireAdminId(), ct);

        var season = Season.Create(room.Id, request.Name, request.StartsOn, request.EndsOn, clock.GetUtcNow());

        db.Seasons.Add(season);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/seasons/{season.Id}", ToDto(season, 0, 0));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        SeasonRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var season = await LoadAsync(db, queries, currentUser, id, ct);

        season.Update(request.Name, request.StartsOn, request.EndsOn);
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(season, 0, 0));
    }

    private static async Task<IResult> ActivateAsync(
        Guid id,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var season = await LoadAsync(db, queries, currentUser, id, ct);
        if (season.Status == SeasonStatus.Active) return Results.Ok(ToDto(season, 0, 0));

        var audit = AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, AuditLogEntry.Actions.SeasonActivated, season.Name, clock.GetUtcNow());

        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

                var current = await db.Seasons.SingleOrDefaultAsync(
                s => s.RoomId == season.RoomId && s.Status == SeasonStatus.Active, ct);
            if (current is not null)
            {
                current.Deactivate();
                await db.SaveChangesAsync(ct);
            }

            season.Activate();
            db.AuditLogs.Add(audit);

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        return Results.Ok(ToDto(season, 0, 0));
    }

    private static async Task<IResult> FinishAsync(
        Guid id,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var season = await LoadAsync(db, queries, currentUser, id, ct);
        var now = clock.GetUtcNow();

        var ranking = await queries.GetSeasonRankingAsync(season, currentUser.RequireAdminId(), ct);

        var candidates = ranking.Entries
            .Where(entry => entry.RoundsPlayed > 0)
            .OrderBy(entry => entry.Position)
            .Take(Season.MaxPodiumPositions)
            .Select(entry => new PodiumCandidate(
                entry.ParticipantId, entry.DisplayName, entry.TotalPoints, entry.TotalTimeMs));

        season.Finish(now, candidates);

        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, AuditLogEntry.Actions.SeasonFinished, season.Name, now));

        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(season, 0, 0));
    }

    private static async Task<IResult> ExportAsync(
        Guid id,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var season = await LoadAsync(db, queries, currentUser, id, ct);
        var ranking = await queries.GetSeasonRankingAsync(season, currentUser.RequireAdminId(), ct);

        var csv = new StringBuilder();
        csv.AppendLine("Posição;Nome;Pontos;Tempo total (s);Rodadas respondidas");

        foreach (var entry in ranking.Entries)
        {
            csv.AppendLine(string.Join(';',
                entry.Position,
                Escape(entry.DisplayName),
                entry.TotalPoints,
                entry.TotalTimeMs / 1000,
                entry.RoundsPlayed));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var fileName = $"ranking-{Slug(season.Name)}.csv";

        return Results.File(bytes, "text/csv", fileName);
    }

    private static async Task<Season> LoadAsync(
        DomusDbContext db,
        DomusQueries queries,
        CurrentUser currentUser,
        Guid id,
        CancellationToken ct)
    {
        var room = await queries.RequireMyRoomAsync(currentUser.RequireAdminId(), ct);

        return await db.Seasons.Include(s => s.Podium)
            .SingleOrDefaultAsync(s => s.Id == id && s.RoomId == room.Id, ct)
            ?? throw NotFoundException.For("Temporada");
    }

    private static AdminSeasonDto ToDto(Season season, int rounds, int published) => new(
        season.Id,
        season.Name,
        season.StartsOn,
        season.EndsOn,
        season.Status,
        rounds,
        published,
        [.. season.Podium
            .OrderBy(p => p.Position)
            .Select(p => new PodiumEntryDto(p.Position, p.DisplayName, p.TotalPoints, p.TotalTimeMs))]);

    private static string Escape(string value) => value.Replace(';', ',');

    private static string Slug(string value) =>
        new(value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
}
