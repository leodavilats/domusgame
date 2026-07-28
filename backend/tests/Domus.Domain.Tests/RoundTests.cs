using Domus.Domain.Common;
using Domus.Domain.Rounds;
using Xunit;

namespace Domus.Domain.Tests;

public class RoundTests
{
    [Fact]
    public void Rascunho_nao_fica_disponivel_para_participante()
    {
        var round = TestData.DraftRound();

        Assert.Equal(RoundAvailability.Draft, round.AvailabilityAt(TestData.Sunday13h.AddHours(1)));
        Assert.False(round.IsOpenAt(TestData.Sunday13h.AddHours(1)));
    }

    [Theory]
    [InlineData(-1, RoundAvailability.Scheduled)]
    [InlineData(0, RoundAvailability.Open)]
    [InlineData(1, RoundAvailability.Open)]
    public void Disponibilidade_e_derivada_do_relogio(int hoursFromOpening, RoundAvailability expected)
    {
        var round = TestData.PublishedRound();
        Assert.Equal(expected, round.AvailabilityAt(TestData.Sunday13h.AddHours(hoursFromOpening)));
    }

    [Fact]
    public void Fecha_exatamente_no_limite()
    {
        var round = TestData.PublishedRound();

        Assert.Equal(RoundAvailability.Open, round.AvailabilityAt(TestData.Saturday2359));
        Assert.Equal(RoundAvailability.Closed, round.AvailabilityAt(TestData.Saturday2359.AddSeconds(1)));
    }

    [Fact]
    public void Gabarito_so_e_revelado_depois_do_encerramento()
    {
        var round = TestData.PublishedRound();

        Assert.False(round.IsAnswerRevealedAt(TestData.Saturday2359));
        Assert.True(round.IsAnswerRevealedAt(TestData.Saturday2359.AddMinutes(1)));
    }

    [Fact]
    public void Rodada_publicada_nao_pode_ser_alterada()
    {
        var round = TestData.PublishedRound();

        Assert.Throws<DomainRuleException>(() => round.UpdateDetails(2, "Outro titulo"));
        Assert.Throws<DomainRuleException>(() => round.SetLesson(Lesson.Empty()));
        Assert.Throws<DomainRuleException>(() =>
            round.AddQuestion("Nova?", QuestionMediaType.None, null, null,
                [new AnswerOptionDraft("a", true), new AnswerOptionDraft("b", false)]));
    }

    [Fact]
    public void Nao_publica_sem_licao_e_sem_perguntas()
    {
        var round = Round.CreateDraft(
            Guid.CreateVersion7(), 1, "Semana 1",
            TestData.Sunday13h, TestData.Saturday2359,
            RoundScoringSettings.Default, TestData.Sunday13h.AddDays(-1));

        var problems = round.ValidateForPublish();

        Assert.Contains(problems, p => p.Contains("licao", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(problems, p => p.Contains("pergunta", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<DomainRuleException>(() => round.Publish(TestData.Sunday13h));
    }

    [Fact]
    public void Publica_quando_esta_completa()
    {
        var round = TestData.DraftRound(questionCount: 2);

        Assert.Empty(round.ValidateForPublish());

        round.Publish(TestData.Sunday13h.AddDays(-1));

        Assert.True(round.IsPublished);
        Assert.NotNull(round.PublishedAt);
    }

    [Fact]
    public void Pergunta_exige_exatamente_uma_alternativa_correta()
    {
        var round = TestData.DraftRound(questionCount: 0);

        Assert.Throws<DomainValidationException>(() =>
            round.AddQuestion("Duas certas?", QuestionMediaType.None, null, null,
                [new AnswerOptionDraft("a", true), new AnswerOptionDraft("b", true)]));

        Assert.Throws<DomainValidationException>(() =>
            round.AddQuestion("Nenhuma certa?", QuestionMediaType.None, null, null,
                [new AnswerOptionDraft("a", false), new AnswerOptionDraft("b", false)]));
    }

    [Fact]
    public void Pergunta_exige_de_duas_a_cinco_alternativas()
    {
        var round = TestData.DraftRound(questionCount: 0);

        Assert.Throws<DomainValidationException>(() =>
            round.AddQuestion("Uma so?", QuestionMediaType.None, null, null,
                [new AnswerOptionDraft("a", true)]));

        Assert.Throws<DomainValidationException>(() =>
            round.AddQuestion("Seis?", QuestionMediaType.None, null, null,
            [
                new AnswerOptionDraft("a", true), new AnswerOptionDraft("b", false),
                new AnswerOptionDraft("c", false), new AnswerOptionDraft("d", false),
                new AnswerOptionDraft("e", false), new AnswerOptionDraft("f", false)
            ]));
    }

    [Fact]
    public void Midia_declarada_exige_url_absoluta()
    {
        var round = TestData.DraftRound(questionCount: 0);

        Assert.Throws<DomainValidationException>(() =>
            round.AddQuestion("Com imagem?", QuestionMediaType.Image, null, null,
                [new AnswerOptionDraft("a", true), new AnswerOptionDraft("b", false)]));

        Assert.Throws<DomainValidationException>(() =>
            round.AddQuestion("Com imagem?", QuestionMediaType.Image, "arquivo.png", null,
                [new AnswerOptionDraft("a", true), new AnswerOptionDraft("b", false)]));
    }

    [Fact]
    public void Remover_pergunta_mantem_ordem_contigua()
    {
        var round = TestData.DraftRound(questionCount: 4);
        var second = round.QuestionAtOrder(2)!;

        round.RemoveQuestion(second.Id);

        Assert.Equal([1, 2, 3], round.OrderedQuestions.Select(q => q.Order).ToArray());
    }

    [Fact]
    public void Mover_pergunta_troca_posicoes()
    {
        var round = TestData.DraftRound(questionCount: 3);
        var first = round.QuestionAtOrder(1)!;

        round.MoveQuestion(first.Id, 1);

        Assert.Equal(2, first.Order);
        Assert.Equal([1, 2, 3], round.OrderedQuestions.Select(q => q.Order).ToArray());
    }

    [Fact]
    public void Mover_pergunta_no_limite_nao_faz_nada()
    {
        var round = TestData.DraftRound(questionCount: 2);
        var first = round.QuestionAtOrder(1)!;

        round.MoveQuestion(first.Id, -1);

        Assert.Equal(1, first.Order);
    }

    [Fact]
    public void Janela_invalida_e_recusada()
    {
        Assert.Throws<DomainValidationException>(() => Round.CreateDraft(
            Guid.CreateVersion7(), 1, "Semana 1",
            TestData.Saturday2359, TestData.Sunday13h,
            RoundScoringSettings.Default, TestData.Sunday13h));
    }

    [Fact]
    public void Duplicar_copia_perguntas_como_rascunho()
    {
        var original = TestData.PublishedRound(questionCount: 3);

        var copy = original.DuplicateAsDraft(
            weekNumber: 2,
            opensAt: TestData.Sunday13h.AddDays(7),
            closesAt: TestData.Saturday2359.AddDays(7),
            now: TestData.Sunday13h);

        Assert.True(copy.IsDraft);
        Assert.Equal(3, copy.Questions.Count);
        Assert.Equal(2, copy.WeekNumber);
        Assert.All(copy.OrderedQuestions, q => Assert.Single(q.Options, o => o.IsCorrect));
        Assert.NotEqual(original.Id, copy.Id);
    }

    [Fact]
    public void Teto_de_pontos_considera_perguntas_e_parametros()
    {
        var round = TestData.DraftRound(questionCount: 8);
        Assert.Equal(8 * 15, round.MaxPoints);
    }

    [Theory]
    [InlineData(0, 5, 45)]
    [InlineData(10, 51, 45)]
    [InlineData(10, 5, 9)]
    [InlineData(10, 5, 301)]
    public void Parametros_de_pontuacao_fora_da_faixa_sao_recusados(int points, int bonus, int limit)
    {
        Assert.Throws<DomainValidationException>(() => RoundScoringSettings.Create(points, bonus, limit));
    }
}
