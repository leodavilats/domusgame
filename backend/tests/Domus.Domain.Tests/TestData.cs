using Domus.Domain.Rounds;

namespace Domus.Domain.Tests;

/// <summary>Construtores de conveniencia para os testes de dominio.</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Sunday13h = new(2026, 8, 2, 16, 0, 0, TimeSpan.Zero);   // 13h de Brasilia
    public static readonly DateTimeOffset Saturday2359 = new(2026, 8, 9, 2, 59, 0, TimeSpan.Zero);

    public static Round PublishedRound(
        int questionCount = 3,
        RoundScoringSettings? scoring = null,
        DateTimeOffset? opensAt = null,
        DateTimeOffset? closesAt = null)
    {
        var round = DraftRound(questionCount, scoring, opensAt, closesAt);
        round.Publish(opensAt ?? Sunday13h);
        return round;
    }

    public static Round DraftRound(
        int questionCount = 3,
        RoundScoringSettings? scoring = null,
        DateTimeOffset? opensAt = null,
        DateTimeOffset? closesAt = null)
    {
        var createdAt = (opensAt ?? Sunday13h).AddDays(-1);

        var round = Round.CreateDraft(
            Guid.CreateVersion7(),
            weekNumber: 1,
            title: "Semana 1 - A graça",
            opensAt: opensAt ?? Sunday13h,
            closesAt: closesAt ?? Saturday2359,
            scoring: scoring ?? RoundScoringSettings.Default,
            now: createdAt);

        round.SetLesson(
            Lesson.Create("A graça de Deus", "Efesios 2.1-10", "Conteúdo da licao.", null),
            createdAt);

        for (var i = 1; i <= questionCount; i++)
        {
            round.AddQuestion(
                $"Pergunta {i}?",
                QuestionMediaType.None,
                null,
                $"Explicacao {i}",
                [
                    new AnswerOptionDraft($"Certa {i}", true),
                    new AnswerOptionDraft($"Errada {i}a", false),
                    new AnswerOptionDraft($"Errada {i}b", false)
                ],
                createdAt);
        }

        return round;
    }

    public static AnswerOption CorrectOptionOf(Round round, int order) =>
        round.QuestionAtOrder(order)!.CorrectOption;

    public static AnswerOption WrongOptionOf(Round round, int order) =>
        round.QuestionAtOrder(order)!.Options.First(o => !o.IsCorrect);
}
