using Domus.Domain.Participants;

namespace Domus.Api.Common;

public static class MeMapper
{
    public static async Task<MeDto> BuildAsync(
        Participant participant,
        DomusQueries queries,
        BadgeEvaluator badges,
        CancellationToken ct = default)
    {
        var room = await queries.GetMyRoomAsync(participant.Id, ct);

        if (room is not null) await badges.EvaluateAndAwardAsync(room.Id, participant.Id, ct);

        var earnedBadges = await badges.GetEarnedBadgesAsync(participant.Id, ct);

        return new MeDto(
            participant.Id,
            participant.DisplayName,
            participant.AvatarUrl,
            participant.IsAdmin,
            room is null ? null : new MyRoomSummaryDto(room.Id, room.Name),
            earnedBadges);
    }
}
