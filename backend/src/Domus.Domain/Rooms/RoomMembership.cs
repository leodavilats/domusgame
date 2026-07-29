using Domus.Domain.Common;

namespace Domus.Domain.Rooms;

public sealed class RoomMembership : Entity
{
    private RoomMembership() : base() { }

    private RoomMembership(Guid roomId, Guid participantId, DateTimeOffset now)
        : base(NewId())
    {
        Guard.Requires(roomId != Guid.Empty, "Sala invalida.");
        Guard.Requires(participantId != Guid.Empty, "Participante invalido.");

        RoomId = roomId;
        ParticipantId = participantId;
        JoinedAt = now;
    }

    public Guid RoomId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    public static RoomMembership Join(Room room, Guid participantId, DateTimeOffset now) =>
        new(room.Id, participantId, now);
}
