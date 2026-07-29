using Domus.Domain.Common;
using Domus.Domain.Participants;
using Domus.Domain.Seasons;
using Domus.Domain.Rooms;
using Xunit;

namespace Domus.Domain.Tests;

public class ParticipantTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registro_normaliza_nome_e_nasce_sem_foto()
    {
        var participant = Participant.Register(Guid.CreateVersion7(), "  Leonardo  ", Now);

        Assert.Equal("Leonardo", participant.DisplayName);
        Assert.Equal("LEONARDO", participant.NormalizedDisplayName);
        Assert.Null(participant.AvatarUrl);
        Assert.False(participant.IsAdmin);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public void Nome_invalido_e_recusado(string name)
    {
        Assert.Throws<DomainValidationException>(() =>
            Participant.Register(Guid.CreateVersion7(), name, Now));
    }

    [Fact]
    public void Foto_precisa_ser_url_absoluta()
    {
        var participant = Participant.Register(Guid.CreateVersion7(), "Leonardo", Now);

        Assert.Throws<DomainValidationException>(() => participant.SetPhoto("foto.png"));
    }

    [Fact]
    public void Foto_aceita_url_absoluta_e_pode_ser_limpa()
    {
        var participant = Participant.Register(Guid.CreateVersion7(), "Leonardo", Now);

        participant.SetPhoto("https://lh3.googleusercontent.com/a/foto=s96-c");
        Assert.Equal("https://lh3.googleusercontent.com/a/foto=s96-c", participant.AvatarUrl);

        participant.SetPhoto(null);
        Assert.Null(participant.AvatarUrl);
    }

    [Fact]
    public void Exclusao_anonimiza_e_apaga_a_foto()
    {
        var participant = Participant.Register(Guid.CreateVersion7(), "Leonardo", Now, ParticipantRole.Admin);
        participant.SetPhoto("https://exemplo.local/foto.png");

        participant.Anonymize();

        Assert.True(participant.IsRemoved);
        Assert.Equal(Participant.RemovedDisplayName, participant.DisplayName);
        Assert.Null(participant.AvatarUrl);
        Assert.False(participant.IsAdmin);
        Assert.Throws<DomainRuleException>(() => participant.ChangeRole(ParticipantRole.Admin));
        Assert.Throws<DomainRuleException>(() => participant.SetPhoto("https://exemplo.local/outra.png"));
    }
}

public class SeasonTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid RoomId = Guid.CreateVersion7();

    private static Season NewSeason() =>
        Season.Create(RoomId, "3o trimestre de 2026", new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), Now);

    [Fact]
    public void Temporada_nasce_como_rascunho()
    {
        Assert.Equal(SeasonStatus.Draft, NewSeason().Status);
    }

    [Fact]
    public void Periodo_invertido_e_recusado()
    {
        Assert.Throws<DomainValidationException>(() =>
            Season.Create(RoomId, "Invalida", new DateOnly(2026, 9, 30), new DateOnly(2026, 7, 1), Now));
    }

    [Fact]
    public void Encerramento_congela_ate_tres_colocados()
    {
        var season = NewSeason();
        season.Activate();

        season.Finish(Now, [
            new PodiumCandidate(Guid.CreateVersion7(), "Ana", 300, 100_000),
            new PodiumCandidate(Guid.CreateVersion7(), "Bruno", 280, 90_000),
            new PodiumCandidate(Guid.CreateVersion7(), "Carla", 270, 95_000),
            new PodiumCandidate(Guid.CreateVersion7(), "Diego", 260, 99_000)
        ]);

        Assert.Equal(SeasonStatus.Finished, season.Status);
        Assert.Equal(3, season.Podium.Count);
        Assert.Equal([1, 2, 3], season.Podium.Select(p => p.Position).ToArray());
        Assert.Equal("Ana", season.Podium[0].DisplayName);
    }

    [Fact]
    public void Temporada_encerrada_e_imutavel()
    {
        var season = NewSeason();
        season.Finish(Now, []);

        Assert.Throws<DomainRuleException>(() => season.Activate());
        Assert.Throws<DomainRuleException>(() => season.Finish(Now, []));
        Assert.Throws<DomainRuleException>(() =>
            season.Update("Outro nome", new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)));
    }
}

public class RoomTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Convite_e_comparado_sem_diferenciar_caixa_ou_espacos()
    {
        var room = Room.Create("GC Domus", "domus26", Now);

        Assert.True(room.MatchesInvite("DOMUS26"));
        Assert.True(room.MatchesInvite("  domus26 "));
        Assert.False(room.MatchesInvite("outro"));
        Assert.False(room.MatchesInvite(null));
    }

    [Fact]
    public void Rotacao_invalida_o_codigo_anterior()
    {
        var room = Room.Create("GC Domus", "domus26", Now);

        room.RotateInvite("novo2026", Now.AddDays(30));

        Assert.False(room.MatchesInvite("domus26"));
        Assert.True(room.MatchesInvite("novo2026"));
        Assert.Equal(Now.AddDays(30), room.InviteRotatedAt);
    }

    [Fact]
    public void Codigo_gerado_e_valido()
    {
        var code = Room.GenerateCode();
        var room = Room.Create("GC Domus", code, Now);

        Assert.True(room.MatchesInvite(code));
        Assert.DoesNotContain('0', code);
        Assert.DoesNotContain('O', code);
    }

    [Theory]
    [InlineData("curto")]
    [InlineData("com espaco")]
    [InlineData("com-hifen")]
    public void Codigo_invalido_e_recusado(string code)
    {
        Assert.Throws<DomainValidationException>(() => Room.Create("GC Domus", code, Now));
    }

    [Fact]
    public void Filiacao_registra_sala_e_participante()
    {
        var room = Room.Create("GC Domus", "domus26", Now);
        var participantId = Guid.CreateVersion7();

        var membership = RoomMembership.Join(room, participantId, Now);

        Assert.Equal(room.Id, membership.RoomId);
        Assert.Equal(participantId, membership.ParticipantId);
        Assert.Equal(Now, membership.JoinedAt);
    }
}
