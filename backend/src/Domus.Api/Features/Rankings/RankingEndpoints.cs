using Domus.Api.Common;
using Domus.Domain.Common;
using Domus.Domain.Seasons;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Rankings;

public static class RankingEndpoints
{
    public static void MapRankingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rankings").RequireAuthorization();

        group.MapGet("/round/{roundId:guid}", RoundRankingAsync);
        group.MapGet("/season", SeasonRankingAsync);
    }

    private static async Task<IResult> RoundRankingAsync(
        Guid roundId,
        CurrentUser currentUser,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();
        var round = await queries.RequireRoundInMyRoomAsync(roundId, meId, tracking: false, ct);

        if (!round.IsClosedAt(queries.Now) && !currentUser.IsAdmin)
        {
            throw new ForbiddenException("O ranking da semana sai quando a rodada encerrar.");
        }

        return Results.Ok(await queries.GetRoundRankingAsync(round, meId, ct));
    }

    private static async Task<IResult> SeasonRankingAsync(
        Guid? seasonId,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();

        var room = await queries.RequireMyRoomAsync(meId, ct);

        var season = seasonId is null
            ? await queries.GetActiveSeasonAsync(room.Id, ct)
            : await db.Seasons.AsNoTracking().SingleOrDefaultAsync(s => s.Id == seasonId && s.RoomId == room.Id, ct);

        if (season is null) throw new NotFoundException("Nenhuma temporada encontrada.");

        return Results.Ok(await queries.GetSeasonRankingAsync(season, meId, ct));
    }
}
