using Domus.Domain.Badges;
using Domus.Domain.Common;
using Xunit;

namespace Domus.Domain.Tests;

public class ParticipantBadgeTests
{
    private static readonly DateTimeOffset Now = TestData.Sunday13h;

    [Fact]
    public void Selo_concedido_guarda_sala_participante_codigo_e_origem()
    {
        var roomId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var roundId = Guid.NewGuid();

        var badge = ParticipantBadge.Award(roomId, participantId, BadgeCode.TabuasDaLei, Now, sourceRoundId: roundId);

        Assert.Equal(roomId, badge.RoomId);
        Assert.Equal(participantId, badge.ParticipantId);
        Assert.Equal(BadgeCode.TabuasDaLei, badge.Code);
        Assert.Equal(Now, badge.EarnedAt);
        Assert.Equal(roundId, badge.SourceRoundId);
        Assert.Null(badge.SourceSeasonId);
    }

    [Fact]
    public void Selo_exige_sala_e_participante_validos()
    {
        Assert.Throws<DomainValidationException>(
            () => ParticipantBadge.Award(Guid.Empty, Guid.NewGuid(), BadgeCode.SarcaArdente, Now));

        Assert.Throws<DomainValidationException>(
            () => ParticipantBadge.Award(Guid.NewGuid(), Guid.Empty, BadgeCode.SarcaArdente, Now));
    }
}
