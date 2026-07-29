using Domus.Api.Common;
using Domus.Domain.Common;
using Domus.Domain.Rooms;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Rooms;

public sealed record JoinRoomRequest(string InviteCode);

public sealed record MyRoomDto(Guid Id, string Name, DateTimeOffset JoinedAt, int MemberCount);

public static class RoomEndpoints
{
    public static void MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rooms").RequireAuthorization();

        group.MapGet("/mine", MineAsync);
        group.MapPost("/join", JoinAsync).RequireRateLimiting(RateLimitPolicies.Auth);
    }

    private static async Task<IResult> MineAsync(
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();
        var room = await queries.GetMyRoomAsync(meId, ct);

        if (room is null) return Results.Ok(Array.Empty<MyRoomDto>());

        return Results.Ok(new[] { await ToDtoAsync(db, room, meId, ct) });
    }

    private static async Task<IResult> JoinAsync(
        JoinRoomRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();

        var code = Guard.Text(request.InviteCode, "Codigo de convite", Room.InviteCodeMaxLength, 1);
        var normalized = Room.Normalize(code);

        var room = await db.Rooms.SingleOrDefaultAsync(r => r.NormalizedInviteCode == normalized, ct)
            ?? throw new DomainValidationException("Codigo invalido. Peca o codigo ao lider do GC.");

        var now = clock.GetUtcNow();

        if (!await queries.IsMemberAsync(room.Id, meId, ct))
        {
            db.RoomMemberships.Add(RoomMembership.Join(room, meId, now));

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
            }
        }

        return Results.Ok(await ToDtoAsync(db, room, meId, ct));
    }

    private static async Task<MyRoomDto> ToDtoAsync(
        DomusDbContext db,
        Room room,
        Guid meId,
        CancellationToken ct)
    {
        var joinedAt = await db.RoomMemberships.AsNoTracking()
            .Where(m => m.RoomId == room.Id && m.ParticipantId == meId)
            .Select(m => m.JoinedAt)
            .FirstOrDefaultAsync(ct);

        var members = await db.RoomMemberships.AsNoTracking()
            .CountAsync(m => m.RoomId == room.Id, ct);

        return new MyRoomDto(room.Id, room.Name, joinedAt, members);
    }
}
