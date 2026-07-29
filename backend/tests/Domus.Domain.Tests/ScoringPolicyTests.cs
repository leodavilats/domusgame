using Domus.Domain.Attempts;
using Domus.Domain.Rounds;
using Xunit;

namespace Domus.Domain.Tests;

public class ScoringPolicyTests
{
    private static readonly RoundScoringSettings Default = RoundScoringSettings.Default;

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5_000, 4)]
    [InlineData(15_000, 3)]
    [InlineData(30_000, 2)]
    [InlineData(45_000, 0)]
    [InlineData(47_000, 0)]
    public void SpeedBonus_decai_com_o_tempo(long elapsedMs, int expectedBonus)
    {
        Assert.Equal(expectedBonus, ScoringPolicy.SpeedBonus(isCorrect: true, elapsedMs, Default));
    }

    [Fact]
    public void Resposta_errada_nao_recebe_bonus()
    {
        Assert.Equal(0, ScoringPolicy.SpeedBonus(isCorrect: false, elapsedMs: 0, Default));
        Assert.Equal(0, ScoringPolicy.BasePoints(isCorrect: false, Default));
    }

    [Fact]
    public void Resposta_certa_recebe_pontos_base()
    {
        Assert.Equal(10, ScoringPolicy.BasePoints(isCorrect: true, Default));
    }

    [Fact]
    public void Bonus_zero_configurado_desliga_o_bonus()
    {
        var scoring = RoundScoringSettings.Create(10, 0, 45);
        Assert.Equal(0, ScoringPolicy.SpeedBonus(isCorrect: true, elapsedMs: 0, scoring));
    }

    [Theory]
    [InlineData(44_999, true)]
    [InlineData(45_000, true)]
    [InlineData(48_000, true)]
    [InlineData(48_001, false)]
    public void Tolerancia_de_rede_de_tres_segundos(long elapsedMs, bool within)
    {
        Assert.Equal(within, ScoringPolicy.IsWithinTimeLimit(elapsedMs, Default));
    }

    [Fact]
    public void Teto_por_pergunta_soma_base_e_bonus()
    {
        Assert.Equal(15, Default.MaxPointsPerQuestion);
    }
}
