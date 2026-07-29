using Domus.Domain.Attempts;
using Domus.Domain.Common;
using Domus.Domain.Rounds;
using Xunit;

namespace Domus.Domain.Tests;

public class AttemptTests
{
    private static readonly Guid Participant = Guid.CreateVersion7();
    private static readonly DateTimeOffset During = TestData.Sunday13h.AddHours(2);

    [Fact]
    public void Nao_inicia_tentativa_com_rodada_fechada()
    {
        var round = TestData.PublishedRound();

        Assert.Throws<DomainRuleException>(() =>
            Attempt.Start(round, Participant, TestData.Saturday2359.AddMinutes(1)));

        Assert.Throws<DomainRuleException>(() =>
            Attempt.Start(round, Participant, TestData.Sunday13h.AddMinutes(-1)));
    }

    [Fact]
    public void Serve_entrega_perguntas_em_ordem()
    {
        var round = TestData.PublishedRound(questionCount: 3);
        var attempt = Attempt.Start(round, Participant, During);

        var first = attempt.ServeCurrentQuestion(round, During);
        Assert.NotNull(first);
        Assert.Equal(1, first!.Order);
        Assert.Equal(3, first.TotalQuestions);

        attempt.Submit(round, first.Question.Id, TestData.CorrectOptionOf(round, 1).Id, During.AddSeconds(5));

        var second = attempt.ServeCurrentQuestion(round, During.AddSeconds(6));
        Assert.Equal(2, second!.Order);
    }

    [Fact]
    public void Serve_repetido_dentro_do_prazo_devolve_a_mesma_pergunta()
    {
        var round = TestData.PublishedRound();
        var attempt = Attempt.Start(round, Participant, During);

        var first = attempt.ServeCurrentQuestion(round, During)!;
        var again = attempt.ServeCurrentQuestion(round, During.AddSeconds(10))!;

        Assert.Equal(first.Question.Id, again.Question.Id);
        Assert.Equal(first.ServedAt, again.ServedAt);
        Assert.Single(attempt.Answers);
    }

    [Fact]
    public void Resposta_correta_soma_base_e_bonus()
    {
        var round = TestData.PublishedRound(questionCount: 1);
        var attempt = Attempt.Start(round, Participant, During);
        var served = attempt.ServeCurrentQuestion(round, During)!;

        attempt.Submit(round, served.Question.Id, TestData.CorrectOptionOf(round, 1).Id, During.AddSeconds(15));

        Assert.Equal(13, attempt.TotalPoints);
        Assert.Equal(1, attempt.CorrectCount);
        Assert.Equal(15_000, attempt.TotalTimeMs);
        Assert.True(attempt.IsFinished);
    }

    [Fact]
    public void Resposta_errada_nao_pontua_e_nao_penaliza()
    {
        var round = TestData.PublishedRound(questionCount: 1);
        var attempt = Attempt.Start(round, Participant, During);
        var served = attempt.ServeCurrentQuestion(round, During)!;

        attempt.Submit(round, served.Question.Id, TestData.WrongOptionOf(round, 1).Id, During.AddSeconds(3));

        Assert.Equal(0, attempt.TotalPoints);
        Assert.Equal(0, attempt.CorrectCount);
        Assert.Equal(AnswerOutcome.Incorrect, attempt.Answers[0].Outcome);
    }

    [Fact]
    public void Resposta_em_branco_e_registrada_sem_pontos()
    {
        var round = TestData.PublishedRound(questionCount: 1);
        var attempt = Attempt.Start(round, Participant, During);
        var served = attempt.ServeCurrentQuestion(round, During)!;

        attempt.Submit(round, served.Question.Id, null, During.AddSeconds(44));

        Assert.Equal(AnswerOutcome.Blank, attempt.Answers[0].Outcome);
        Assert.Equal(0, attempt.TotalPoints);
    }

    [Fact]
    public void Resposta_fora_do_prazo_vira_tempo_esgotado()
    {
        var round = TestData.PublishedRound(questionCount: 1);
        var attempt = Attempt.Start(round, Participant, During);
        var served = attempt.ServeCurrentQuestion(round, During)!;

        var result = attempt.Submit(round, served.Question.Id, TestData.CorrectOptionOf(round, 1).Id, During.AddSeconds(60));

        Assert.True(result.TimedOut);
        Assert.Equal(AnswerOutcome.TimedOut, attempt.Answers[0].Outcome);
        Assert.Equal(0, attempt.TotalPoints);
        Assert.Equal(45_000, attempt.TotalTimeMs);
    }

    [Fact]
    public void Reenvio_da_mesma_pergunta_e_idempotente()
    {
        var round = TestData.PublishedRound(questionCount: 2);
        var attempt = Attempt.Start(round, Participant, During);
        var served = attempt.ServeCurrentQuestion(round, During)!;

        var first = attempt.Submit(round, served.Question.Id, TestData.CorrectOptionOf(round, 1).Id, During.AddSeconds(5));
        var repeat = attempt.Submit(round, served.Question.Id, TestData.WrongOptionOf(round, 1).Id, During.AddSeconds(6));

        Assert.Equal(first.AnswerId, repeat.AnswerId);
        Assert.Equal(14, attempt.TotalPoints);
        Assert.Single(attempt.Answers);
    }

    [Fact]
    public void Nao_e_possivel_pular_pergunta()
    {
        var round = TestData.PublishedRound(questionCount: 3);
        var attempt = Attempt.Start(round, Participant, During);
        attempt.ServeCurrentQuestion(round, During);

        var third = round.QuestionAtOrder(3)!;

        Assert.Throws<DomainRuleException>(() =>
            attempt.Submit(round, third.Id, third.CorrectOption.Id, During.AddSeconds(2)));
    }

    [Fact]
    public void Alternativa_de_outra_pergunta_e_recusada()
    {
        var round = TestData.PublishedRound(questionCount: 2);
        var attempt = Attempt.Start(round, Participant, During);
        var served = attempt.ServeCurrentQuestion(round, During)!;

        var optionFromAnotherQuestion = TestData.CorrectOptionOf(round, 2).Id;

        Assert.Throws<DomainValidationException>(() =>
            attempt.Submit(round, served.Question.Id, optionFromAnotherQuestion, During.AddSeconds(2)));
    }

    [Fact]
    public void Retomada_expira_a_pergunta_abandonada_e_segue_para_a_proxima()
    {
        var round = TestData.PublishedRound(questionCount: 2);
        var attempt = Attempt.Start(round, Participant, During);
        attempt.ServeCurrentQuestion(round, During);

        var resumed = attempt.ServeCurrentQuestion(round, During.AddMinutes(10));

        Assert.Equal(AnswerOutcome.TimedOut, attempt.Answers[0].Outcome);
        Assert.Equal(2, resumed!.Order);
        Assert.False(attempt.IsFinished);
    }

    [Fact]
    public void Tentativa_conclui_ao_responder_a_ultima()
    {
        var round = TestData.PublishedRound(questionCount: 2);
        var attempt = Attempt.Start(round, Participant, During);

        for (var order = 1; order <= 2; order++)
        {
            var served = attempt.ServeCurrentQuestion(round, During)!;
            attempt.Submit(round, served.Question.Id, TestData.CorrectOptionOf(round, order).Id, During.AddSeconds(1));
        }

        Assert.True(attempt.IsFinished);
        Assert.NotNull(attempt.CompletedAt);
        Assert.Null(attempt.NextQuestionOrder);
        Assert.Null(attempt.ServeCurrentQuestion(round, During.AddSeconds(2)));
    }

    [Fact]
    public void Rodada_fechada_finaliza_a_tentativa_com_o_que_foi_pontuado()
    {
        var round = TestData.PublishedRound(questionCount: 3);
        var attempt = Attempt.Start(round, Participant, During);

        var served = attempt.ServeCurrentQuestion(round, During)!;
        attempt.Submit(round, served.Question.Id, TestData.CorrectOptionOf(round, 1).Id, During.AddSeconds(5));
        attempt.ServeCurrentQuestion(round, During.AddSeconds(6));

        attempt.CompleteIfRoundClosed(round, TestData.Saturday2359.AddMinutes(1));

        Assert.True(attempt.IsFinished);
        Assert.Equal(14, attempt.TotalPoints);
        Assert.Equal(AnswerOutcome.TimedOut, attempt.OrderedAnswers[1].Outcome);
    }

    [Fact]
    public void Tentativa_concluida_nao_aceita_nova_resposta()
    {
        var round = TestData.PublishedRound(questionCount: 1);
        var attempt = Attempt.Start(round, Participant, During);
        var served = attempt.ServeCurrentQuestion(round, During)!;
        attempt.Submit(round, served.Question.Id, TestData.CorrectOptionOf(round, 1).Id, During.AddSeconds(1));

        var other = round.QuestionAtOrder(1)!;
        var repeated = attempt.Submit(round, other.Id, null, During.AddSeconds(2));

        Assert.Equal(AnswerOutcome.Correct, attempt.Answers[0].Outcome);
        Assert.True(repeated.AttemptFinished);
    }

    [Fact]
    public void Parametros_da_rodada_sao_congelados_na_tentativa()
    {
        var round = TestData.PublishedRound(questionCount: 1);
        var attempt = Attempt.Start(round, Participant, During);

        Assert.Equal(round.Scoring.PointsPerCorrectAnswer, attempt.Scoring.PointsPerCorrectAnswer);
        Assert.NotSame(round.Scoring, attempt.Scoring);
        Assert.Equal(15, attempt.MaxPoints);
    }

    [Fact]
    public void Tentativa_de_outra_rodada_e_recusada()
    {
        var round = TestData.PublishedRound();
        var other = TestData.PublishedRound();
        var attempt = Attempt.Start(round, Participant, During);

        Assert.Throws<DomainRuleException>(() => attempt.ServeCurrentQuestion(other, During));
    }
}
