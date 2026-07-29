using Domus.Domain.Common;

namespace Domus.Domain.Rounds;

public enum RoundStatus
{
    Draft = 0,
    Published = 1
}

/// <summary>Estado derivado do relogio (RN-07). Nunca e persistido.</summary>
public enum RoundAvailability
{
    /// <summary>Rascunho: invisivel para participantes (RN-09).</summary>
    Draft = 0,
    Scheduled = 1,
    Open = 2,
    Closed = 3
}

/// <summary>
/// Desafio de uma semana: licao, perguntas, janela de disponibilidade e parametros de pontuacao.
/// Toda mutacao exige status rascunho (RN-10).
/// </summary>
public sealed class Round : Entity
{
    private readonly List<Question> _questions = [];

    private Round() : base()
    {
        Title = string.Empty;
        Lesson = Lesson.Empty();
        Scoring = RoundScoringSettings.Default;
    }

    private Round(
        Guid seasonId,
        int weekNumber,
        string title,
        DateTimeOffset opensAt,
        DateTimeOffset closesAt,
        RoundScoringSettings scoring,
        DateTimeOffset now)
        : base(NewId())
    {
        Guard.Requires(seasonId != Guid.Empty, "Temporada invalida.");

        SeasonId = seasonId;
        WeekNumber = Guard.InRange(weekNumber, 1, 999, "Numero da semana");
        Title = Guard.Text(title, "Titulo da rodada", 120);
        ValidateWindow(opensAt, closesAt);
        OpensAt = opensAt;
        ClosesAt = closesAt;
        Scoring = scoring;
        Lesson = Lesson.Empty();
        Status = RoundStatus.Draft;
        CreatedAt = now;
    }

    public Guid SeasonId { get; private set; }
    public int WeekNumber { get; private set; }
    public string Title { get; private set; }
    public DateTimeOffset OpensAt { get; private set; }
    public DateTimeOffset ClosesAt { get; private set; }
    public RoundStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public Lesson Lesson { get; private set; }
    public RoundScoringSettings Scoring { get; private set; }

    public IReadOnlyList<Question> Questions => _questions;

    /// <summary>Perguntas na ordem de apresentacao. Use sempre este acesso em regras de negocio.</summary>
    public IReadOnlyList<Question> OrderedQuestions => [.. _questions.OrderBy(q => q.Order)];

    public bool IsDraft => Status == RoundStatus.Draft;

    public bool IsPublished => Status == RoundStatus.Published;

    /// <summary>I-R7: teto de pontos da rodada (RN-27).</summary>
    public int MaxPoints => _questions.Count * Scoring.MaxPointsPerQuestion;

    public static Round CreateDraft(
        Guid seasonId,
        int weekNumber,
        string title,
        DateTimeOffset opensAt,
        DateTimeOffset closesAt,
        RoundScoringSettings scoring,
        DateTimeOffset now) =>
        new(seasonId, weekNumber, title, opensAt, closesAt, scoring, now);

    // ---------------------------------------------------------------- disponibilidade

    /// <summary>I-R6 / RN-07: liberacao e encerramento automaticos, derivados do relogio.</summary>
    public RoundAvailability AvailabilityAt(DateTimeOffset now)
    {
        if (Status == RoundStatus.Draft) return RoundAvailability.Draft;
        if (now < OpensAt) return RoundAvailability.Scheduled;
        return now <= ClosesAt ? RoundAvailability.Open : RoundAvailability.Closed;
    }

    public bool IsOpenAt(DateTimeOffset now) => AvailabilityAt(now) == RoundAvailability.Open;

    public bool IsClosedAt(DateTimeOffset now) => AvailabilityAt(now) == RoundAvailability.Closed;

    /// <summary>RN-21: gabarito, explicacoes e ranking so apos o encerramento.</summary>
    public bool IsAnswerRevealedAt(DateTimeOffset now) => IsClosedAt(now);

    /// <summary>
    /// RN-10: rascunho e sempre editavel, e rodada publicada continua editavel **enquanto nao
    /// abriu**. Da abertura em diante ela e imutavel: ha respostas e pontuacao em jogo, e mudar
    /// enunciado, gabarito ou parametros no meio do caminho tornaria as tentativas incomparaveis.
    /// </summary>
    public bool IsEditableAt(DateTimeOffset now) =>
        IsDraft || AvailabilityAt(now) == RoundAvailability.Scheduled;

    // ------------------------------------------------- mutacoes (rascunho ou publicada agendada)

    public void UpdateDetails(int weekNumber, string title, DateTimeOffset now)
    {
        EnsureEditable(now);
        WeekNumber = Guard.InRange(weekNumber, 1, 999, "Numero da semana");
        Title = Guard.Text(title, "Titulo da rodada", 120);
    }

    public void UpdateWindow(DateTimeOffset opensAt, DateTimeOffset closesAt, DateTimeOffset now)
    {
        EnsureEditable(now);
        ValidateWindow(opensAt, closesAt);
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public void UpdateScoring(RoundScoringSettings scoring, DateTimeOffset now)
    {
        EnsureEditable(now);
        Scoring = scoring;
    }

    public void SetLesson(Lesson lesson, DateTimeOffset now)
    {
        EnsureEditable(now);
        Lesson = lesson;
    }

    public Question AddQuestion(
        string text,
        QuestionMediaType mediaType,
        string? mediaUrl,
        string? explanation,
        IReadOnlyList<AnswerOptionDraft> options,
        DateTimeOffset now)
    {
        EnsureEditable(now);

        var question = Question.Create(Id, _questions.Count + 1, text, mediaType, mediaUrl, explanation, options);
        _questions.Add(question);
        return question;
    }

    public void UpdateQuestion(
        Guid questionId,
        string text,
        QuestionMediaType mediaType,
        string? mediaUrl,
        string? explanation,
        IReadOnlyList<AnswerOptionDraft> options,
        DateTimeOffset now)
    {
        EnsureEditable(now);
        RequireQuestion(questionId).Update(text, mediaType, mediaUrl, explanation, options);
    }

    public void RemoveQuestion(Guid questionId, DateTimeOffset now)
    {
        EnsureEditable(now);
        _questions.Remove(RequireQuestion(questionId));
        Renumber();
    }

    /// <summary>Move a pergunta uma posição para cima (-1) ou para baixo (+1), mantendo a ordem contigua.</summary>
    public void MoveQuestion(Guid questionId, int offset, DateTimeOffset now)
    {
        EnsureEditable(now);
        Guard.Requires(offset is -1 or 1, "Movimento inválido.");

        var ordered = OrderedQuestions.ToList();
        var index = ordered.FindIndex(q => q.Id == questionId);
        if (index < 0) throw NotFoundException.For("Pergunta");

        var target = index + offset;
        if (target < 0 || target >= ordered.Count) return;

        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

        var order = 1;
        foreach (var question in ordered)
        {
            question.SetOrder(order);
            order++;
        }
    }

    // ---------------------------------------------------------------- publicacao

    /// <summary>I-R5 / RN-08: lista dos problemas que impedem a publicacao. Vazia = pode publicar.</summary>
    public IReadOnlyList<string> ValidateForPublish()
    {
        var problems = new List<string>();

        if (IsPublished) problems.Add("A rodada ja esta publicada.");
        if (!Lesson.IsComplete) problems.Add("Preencha titulo, referencia bíblica e conteúdo da licao.");
        if (_questions.Count == 0) problems.Add("Cadastre ao menos uma pergunta.");
        if (ClosesAt <= OpensAt) problems.Add("O fechamento deve ser posterior a abertura.");

        foreach (var question in OrderedQuestions)
        {
            if (question.Options.Count is < Question.MinOptions or > Question.MaxOptions)
            {
                problems.Add($"Pergunta {question.Order}: precisa de {Question.MinOptions} a {Question.MaxOptions} alternativas.");
            }
            else if (question.Options.Count(o => o.IsCorrect) != 1)
            {
                problems.Add($"Pergunta {question.Order}: marque exatamente uma alternativa correta.");
            }

            if (question.MediaType != QuestionMediaType.None && string.IsNullOrWhiteSpace(question.MediaUrl))
            {
                problems.Add($"Pergunta {question.Order}: informe a URL da midia.");
            }
        }

        var expectedOrders = Enumerable.Range(1, _questions.Count);
        if (!_questions.Select(q => q.Order).OrderBy(o => o).SequenceEqual(expectedOrders))
        {
            problems.Add("A ordem das perguntas esta inconsistente.");
        }

        return problems;
    }

    public void Publish(DateTimeOffset now)
    {
        var problems = ValidateForPublish();
        if (problems.Count > 0)
        {
            throw new DomainRuleException($"Não foi possivel publicar: {string.Join(" ", problems)}");
        }

        Status = RoundStatus.Published;
        PublishedAt = now;
    }

    /// <summary>Copia perguntas e parametros para um novo rascunho (UC-25).</summary>
    public Round DuplicateAsDraft(int weekNumber, DateTimeOffset opensAt, DateTimeOffset closesAt, DateTimeOffset now)
    {
        var copy = new Round(SeasonId, weekNumber, Title, opensAt, closesAt, Scoring.Copy(), now);

        foreach (var question in OrderedQuestions)
        {
            var options = question.Options
                .OrderBy(o => o.Order)
                .Select(o => new AnswerOptionDraft(o.Text, o.IsCorrect))
                .ToList();

            copy.AddQuestion(
                question.Text, question.MediaType, question.MediaUrl, question.Explanation, options, now);
        }

        return copy;
    }

    /// <summary>
    /// ESCAPE HATCH DE TESTE. Desloca a janela ignorando RN-10, para que o painel de
    /// ferramentas possa abrir ou encerrar uma rodada na hora sem esperar o relogio.
    ///
    /// Nenhum fluxo de produto chama isto: o unico caminho e o grupo /api/admin/tools, que
    /// so existe quando DevTools__Enabled esta ligado. O nome e deliberadamente feio para
    /// que apareca em qualquer revisao e nao seja confundido com UpdateWindow.
    /// </summary>
    public void OverrideWindowForTesting(DateTimeOffset opensAt, DateTimeOffset closesAt)
    {
        ValidateWindow(opensAt, closesAt);
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public Question RequireQuestion(Guid questionId) =>
        _questions.SingleOrDefault(q => q.Id == questionId) ?? throw NotFoundException.For("Pergunta");

    public Question? QuestionAtOrder(int order) => _questions.SingleOrDefault(q => q.Order == order);

    private void EnsureEditable(DateTimeOffset now) =>
        Guard.State(
            IsEditableAt(now),
            "Rodada que já abriu não pode ser alterada.");

    private void Renumber()
    {
        var order = 1;
        foreach (var question in _questions.OrderBy(q => q.Order).ToList())
        {
            question.SetOrder(order);
            order++;
        }
    }

    private static void ValidateWindow(DateTimeOffset opensAt, DateTimeOffset closesAt) =>
        Guard.Requires(closesAt > opensAt, "O fechamento deve ser posterior a abertura.");
}
