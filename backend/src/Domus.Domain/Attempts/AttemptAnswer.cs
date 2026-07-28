using Domus.Domain.Common;
using Domus.Domain.Rounds;

namespace Domus.Domain.Attempts;

public enum AnswerOutcome
{
    /// <summary>Pergunta entregue, aguardando resposta.</summary>
    Pending = 0,
    Correct = 1,
    Incorrect = 2,

    /// <summary>Enviada sem escolher alternativa (cronometro zerou no cliente).</summary>
    Blank = 3,

    /// <summary>Prazo estourado: nao pontua (RN-18).</summary>
    TimedOut = 4
}

/// <summary>
/// Resposta de uma pergunta dentro de uma tentativa. Guarda o instante em que a pergunta foi
/// entregue pelo servidor, o tempo gasto e os pontos calculados no momento do envio (RN-28).
/// </summary>
public sealed class AttemptAnswer : Entity
{
    private AttemptAnswer() : base() { }

    private AttemptAnswer(Guid attemptId, Question question, DateTimeOffset servedAt)
        : base(NewId())
    {
        AttemptId = attemptId;
        QuestionId = question.Id;
        QuestionOrder = question.Order;
        ServedAt = servedAt;
        Outcome = AnswerOutcome.Pending;
    }

    public Guid AttemptId { get; private set; }
    public Guid QuestionId { get; private set; }

    /// <summary>Denormalizado para ordenar a revisao sem join.</summary>
    public int QuestionOrder { get; private set; }

    /// <summary>Relogio do servidor no instante da entrega (RNF-03).</summary>
    public DateTimeOffset ServedAt { get; private set; }

    public DateTimeOffset? AnsweredAt { get; private set; }
    public Guid? SelectedOptionId { get; private set; }
    public AnswerOutcome Outcome { get; private set; }
    public int BasePoints { get; private set; }
    public int SpeedBonus { get; private set; }
    public long ElapsedMs { get; private set; }

    public bool IsPending => Outcome == AnswerOutcome.Pending;

    public bool IsCorrect => Outcome == AnswerOutcome.Correct;

    public int Points => BasePoints + SpeedBonus;

    internal static AttemptAnswer Serve(Guid attemptId, Question question, DateTimeOffset servedAt) =>
        new(attemptId, question, servedAt);

    public DateTimeOffset DeadlineAt(RoundScoringSettings scoring) =>
        ServedAt.AddSeconds(scoring.QuestionTimeLimitSeconds);

    internal bool HasExpiredAt(DateTimeOffset now, RoundScoringSettings scoring) =>
        IsPending && !ScoringPolicy.IsWithinTimeLimit(ElapsedSince(now), scoring);

    internal void Resolve(Question question, Guid? selectedOptionId, DateTimeOffset now, RoundScoringSettings scoring)
    {
        // I-A4: resposta resolvida e imutavel.
        Guard.State(IsPending, "Esta pergunta ja foi respondida.");

        var elapsed = ElapsedSince(now);

        if (!ScoringPolicy.IsWithinTimeLimit(elapsed, scoring))
        {
            MarkTimedOut(now, scoring);
            return;
        }

        AnsweredAt = now;
        ElapsedMs = elapsed;

        if (selectedOptionId is null)
        {
            Outcome = AnswerOutcome.Blank;
            return;
        }

        var option = question.Options.SingleOrDefault(o => o.Id == selectedOptionId.Value)
            ?? throw new DomainValidationException("Alternativa nao pertence a esta pergunta.");

        SelectedOptionId = option.Id;
        Outcome = option.IsCorrect ? AnswerOutcome.Correct : AnswerOutcome.Incorrect;
        BasePoints = ScoringPolicy.BasePoints(option.IsCorrect, scoring);
        SpeedBonus = ScoringPolicy.SpeedBonus(option.IsCorrect, elapsed, scoring);
    }

    /// <summary>I-A7: tempo esgotado conta o limite cheio, para nao premiar quem abandona (RN-29).</summary>
    internal void MarkTimedOut(DateTimeOffset now, RoundScoringSettings scoring)
    {
        if (!IsPending) return;

        Outcome = AnswerOutcome.TimedOut;
        AnsweredAt = now;
        SelectedOptionId = null;
        BasePoints = 0;
        SpeedBonus = 0;
        ElapsedMs = scoring.QuestionTimeLimitSeconds * 1000L;
    }

    private long ElapsedSince(DateTimeOffset now)
    {
        var elapsed = (long)(now - ServedAt).TotalMilliseconds;
        return elapsed < 0 ? 0 : elapsed;
    }
}
