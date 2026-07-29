using Domus.Domain.Participants;

namespace Domus.Api.Common;

public static class MeMapper
{
    public static async Task<MeDto> BuildAsync(
        Participant participant,
        DomusQueries queries,
        CancellationToken ct = default)
    {
        var room = await queries.GetMyRoomAsync(participant.Id, ct);

        return new MeDto(
            participant.Id,
            participant.DisplayName,
            participant.AvatarUrl,
            participant.ShowInRanking,
            participant.IsAdmin,
            room is null ? null : new MyRoomSummaryDto(room.Id, room.Name));
    }
}
