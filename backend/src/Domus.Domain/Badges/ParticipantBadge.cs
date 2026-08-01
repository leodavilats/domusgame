using Domus.Domain.Common;

namespace Domus.Domain.Badges;

public sealed class ParticipantBadge : Entity
{
    private ParticipantBadge() : base() { }

    private ParticipantBadge(
        Guid roomId,
        Guid participantId,
        BadgeCode code,
        DateTimeOffset earnedAt,
        Guid? sourceRoundId,
        Guid? sourceSeasonId)
        : base(NewId())
    {
        Guard.Requires(roomId != Guid.Empty, "Sala invalida.");
        Guard.Requires(participantId != Guid.Empty, "Participante invalido.");

        RoomId = roomId;
        ParticipantId = participantId;
        Code = code;
        EarnedAt = earnedAt;
        SourceRoundId = sourceRoundId;
        SourceSeasonId = sourceSeasonId;
    }

    public Guid RoomId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public BadgeCode Code { get; private set; }
    public DateTimeOffset EarnedAt { get; private set; }
    public Guid? SourceRoundId { get; private set; }
    public Guid? SourceSeasonId { get; private set; }

    public static ParticipantBadge Award(
        Guid roomId,
        Guid participantId,
        BadgeCode code,
        DateTimeOffset earnedAt,
        Guid? sourceRoundId = null,
        Guid? sourceSeasonId = null) =>
        new(roomId, participantId, code, earnedAt, sourceRoundId, sourceSeasonId);
}
