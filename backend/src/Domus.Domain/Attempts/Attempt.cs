using Domus.Domain.Common;
using Domus.Domain.Rounds;

namespace Domus.Domain.Attempts;

public enum AttemptStatus
{
    InProgress = 0,
    Completed = 1
}

/// <summary>Pergunta entregue ao participante, com os instantes que o cliente precisa para o cronometro.</summary>
public sealed record ServedQuestion(
    Question Question,
    DateTimeOffset ServedAt,
    DateTimeOffset DeadlineAt,
    int Order,
    int TotalQuestions);

/// <summary>
/// Resultado do envio de uma resposta. Deliberadamente NAO informa acerto (RN-21):
/// o participante so descobre no encerramento da rodada.
/// </summary>
public sealed record SubmitResult(
    Guid AnswerId,
    bool TimedOut,
    int? NextQuestionOrder,
    bool AttemptFinished);

/// <summary>
/// Participacao de um participante em uma rodada. Unica por (rodada, participante) (RN-14).
/// Concentra a maquina de estados das respostas e as somas de pontos e tempo.
/// </summary>
public sealed class Attempt : Entity
{
    private readonly List<AttemptAnswer> _answers = [];

    private Attempt() : base() => Scoring = RoundScoringSettings.Default;

    private Attempt(Round round, Guid participantId, DateTimeOffset now)
        : base(NewId())
    {
        RoundId = round.Id;
        ParticipantId = participantId;
        StartedAt = now;
        Status = AttemptStatus.InProgress;
        QuestionCount = round.Questions.Count;
        Scoring = round.Scoring.Copy();
    }

    public Guid RoundId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public AttemptStatus Status { get; private set; }

    /// <summary>Quantidade de perguntas congelada no inicio da tentativa.</summary>
    public int QuestionCount { get; private set; }

    public int TotalPoints { get; private set; }
    public int CorrectCount { get; private set; }
    public long TotalTimeMs { get; private set; }

    /// <summary>Copia dos parametros da rodada (RN-28): o historico não muda se a rodada mudar.</summary>
    public RoundScoringSettings Scoring { get; private set; }

    public IReadOnlyList<AttemptAnswer> Answers => _answers;

    public IReadOnlyList<AttemptAnswer> OrderedAnswers => [.. _answers.OrderBy(a => a.QuestionOrder)];

    public bool IsFinished => Status == AttemptStatus.Completed;

    public int AnsweredCount => _answers.Count(a => !a.IsPending);

    public int MaxPoints => QuestionCount * Scoring.MaxPointsPerQuestion;

    /// <summary>Ordem da proxima pergunta a entregar, ou null se a tentativa acabou.</summary>
    public int? NextQuestionOrder
    {
        get
        {
            var pending = _answers.FirstOrDefault(a => a.IsPending);
            if (pending is not null) return pending.QuestionOrder;

            var next = _answers.Count + 1;
            return next <= QuestionCount ? next : null;
        }
    }

    public static Attempt Start(Round round, Guid participantId, DateTimeOffset now)
    {
        Guard.Requires(participantId != Guid.Empty, "Participante inválido.");
        Guard.State(round.IsOpenAt(now), "Esta rodada não esta aberta para respostas.");
        Guard.State(round.Questions.Count > 0, "Esta rodada não tem perguntas.");

        return new Attempt(round, participantId, now);
    }

    /// <summary>
    /// Entrega a pergunta corrente, criando o registro de resposta pendente com o instante do
    /// servidor. Idempotente: chamada de novo dentro do prazo devolve a mesma pergunta (RN-19).
    /// </summary>
    public ServedQuestion? ServeCurrentQuestion(Round round, DateTimeOffset now)
    {
        EnsureSameRound(round);

        ExpirePendingIfNeeded(now);
        CompleteIfRoundClosed(round, now);

        if (IsFinished) return null;

        var pending = _answers.FirstOrDefault(a => a.IsPending);
        if (pending is not null)
        {
            var pendingQuestion = round.RequireQuestion(pending.QuestionId);
            return Serve(pendingQuestion, pending);
        }

        var nextOrder = _answers.Count + 1;
        if (nextOrder > QuestionCount)
        {
            Complete(now);
            return null;
        }

        var question = round.QuestionAtOrder(nextOrder)
            ?? throw new DomainRuleException("A rodada foi alterada e não tem mais esta pergunta.");

        var answer = AttemptAnswer.Serve(Id, question, now);
        _answers.Add(answer);

        return Serve(question, answer);
    }

    /// <summary>
    /// Registra a resposta. Idempotente por (tentativa, pergunta) (I-A4 / RNF-05) e recusa
    /// perguntas que não foram entregues, o que impede pular ou voltar (I-A3 / RN-15).
    /// </summary>
    public SubmitResult Submit(Round round, Guid questionId, Guid? selectedOptionId, DateTimeOffset now)
    {
        EnsureSameRound(round);

        var answer = _answers.SingleOrDefault(a => a.QuestionId == questionId)
            ?? throw new DomainRuleException("Esta pergunta não foi entregue para a sua tentativa.");

        // Reenvio (duplo clique, retry de rede): devolve o mesmo resultado sem repontuar.
        if (!answer.IsPending) return BuildResult(answer);

        Guard.State(!IsFinished, "Esta tentativa ja foi concluida.");

        if (round.IsClosedAt(now))
        {
            answer.MarkTimedOut(now, Scoring);
            Recalculate();
            Complete(now);
            return BuildResult(answer);
        }

        Guard.State(round.IsOpenAt(now), "Esta rodada não esta aberta para respostas.");

        var question = round.RequireQuestion(questionId);
        answer.Resolve(question, selectedOptionId, now, Scoring);

        Recalculate();

        if (_answers.Count >= QuestionCount && _answers.All(a => !a.IsPending))
        {
            Complete(now);
        }

        return BuildResult(answer);
    }

    /// <summary>Fecha como tempo esgotado qualquer pergunta entregue cujo prazo ja passou (RN-18).</summary>
    public void ExpirePendingIfNeeded(DateTimeOffset now)
    {
        var expired = false;

        foreach (var answer in _answers.Where(a => a.HasExpiredAt(now, Scoring)).ToList())
        {
            answer.MarkTimedOut(now, Scoring);
            expired = true;
        }

        if (expired) Recalculate();
    }

    /// <summary>RN-20: rodada fechada finaliza a tentativa com o que ja foi pontuado.</summary>
    public void CompleteIfRoundClosed(Round round, DateTimeOffset now)
    {
        EnsureSameRound(round);

        if (IsFinished || !round.IsClosedAt(now)) return;

        foreach (var answer in _answers.Where(a => a.IsPending).ToList())
        {
            answer.MarkTimedOut(now, Scoring);
        }

        Recalculate();
        Complete(now);
    }

    private ServedQuestion Serve(Question question, AttemptAnswer answer) =>
        new(question, answer.ServedAt, answer.DeadlineAt(Scoring), question.Order, QuestionCount);

    private SubmitResult BuildResult(AttemptAnswer answer) =>
        new(
            answer.Id,
            answer.Outcome == AnswerOutcome.TimedOut,
            IsFinished ? null : NextQuestionOrder,
            IsFinished);

    private void Complete(DateTimeOffset now)
    {
        if (IsFinished) return;

        Status = AttemptStatus.Completed;
        CompletedAt = now;
    }

    /// <summary>I-A5: as somas sao sempre derivadas das respostas, nunca incrementadas soltas.</summary>
    private void Recalculate()
    {
        var resolved = _answers.Where(a => !a.IsPending).ToList();

        TotalPoints = resolved.Sum(a => a.Points);
        CorrectCount = resolved.Count(a => a.IsCorrect);
        TotalTimeMs = resolved.Sum(a => a.ElapsedMs);
    }

    private void EnsureSameRound(Round round) =>
        Guard.State(round.Id == RoundId, "Rodada não corresponde a esta tentativa.");
}
