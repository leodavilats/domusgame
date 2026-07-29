using Domus.Domain.Common;

namespace Domus.Domain.Rounds;

public enum QuestionMediaType
{
    None = 0,
    Image = 1,
    Audio = 2
}

public sealed class AnswerOption : Entity
{
    private AnswerOption() : base() => Text = string.Empty;

    internal AnswerOption(Guid questionId, int order, string text, bool isCorrect)
        : base(NewId())
    {
        QuestionId = questionId;
        Order = order;
        Text = Guard.Text(text, "Texto da alternativa", 300);
        IsCorrect = isCorrect;
    }

    public Guid QuestionId { get; private set; }
    public int Order { get; private set; }
    public string Text { get; private set; }
    public bool IsCorrect { get; private set; }

    internal void SetOrder(int order) => Order = order;
}

public readonly record struct AnswerOptionDraft(string Text, bool IsCorrect);

public sealed class Question : Entity
{
    public const int MinOptions = 2;
    public const int MaxOptions = 5;

    private readonly List<AnswerOption> _options = [];

    private Question() : base() => Text = string.Empty;

    private Question(
        Guid roundId,
        int order,
        string text,
        QuestionMediaType mediaType,
        string? mediaUrl,
        string? explanation)
        : base(NewId())
    {
        RoundId = roundId;
        Order = order;
        Text = Guard.Text(text, "Enunciado", 500);
        (MediaType, MediaUrl) = ValidateMedia(mediaType, mediaUrl);
        Explanation = Guard.OptionalText(explanation, "Explicação", 1000);
    }

    public Guid RoundId { get; private set; }

    public int Order { get; private set; }

    public string Text { get; private set; }
    public QuestionMediaType MediaType { get; private set; }
    public string? MediaUrl { get; private set; }

    public string? Explanation { get; private set; }

    public IReadOnlyList<AnswerOption> Options => _options;

    public AnswerOption CorrectOption =>
        _options.SingleOrDefault(o => o.IsCorrect)
        ?? throw new DomainRuleException($"A pergunta {Order} não tem exatamente uma alternativa correta.");

    internal static Question Create(
        Guid roundId,
        int order,
        string text,
        QuestionMediaType mediaType,
        string? mediaUrl,
        string? explanation,
        IReadOnlyList<AnswerOptionDraft> options)
    {
        var question = new Question(roundId, order, text, mediaType, mediaUrl, explanation);
        question.ReplaceOptions(options);
        return question;
    }

    internal void Update(
        string text,
        QuestionMediaType mediaType,
        string? mediaUrl,
        string? explanation,
        IReadOnlyList<AnswerOptionDraft> options)
    {
        Text = Guard.Text(text, "Enunciado", 500);
        (MediaType, MediaUrl) = ValidateMedia(mediaType, mediaUrl);
        Explanation = Guard.OptionalText(explanation, "Explicação", 1000);
        ReplaceOptions(options);
    }

    internal void ReplaceOptions(IReadOnlyList<AnswerOptionDraft> options)
    {
        Guard.Requires(
            options.Count >= MinOptions && options.Count <= MaxOptions,
            $"Cada pergunta precisa de {MinOptions} a {MaxOptions} alternativas.");

        var correctCount = options.Count(o => o.IsCorrect);
        Guard.Requires(correctCount == 1, "Marque exatamente uma alternativa como correta.");

        _options.Clear();
        var order = 1;
        foreach (var draft in options)
        {
            _options.Add(new AnswerOption(Id, order, draft.Text, draft.IsCorrect));
            order++;
        }
    }

    internal void SetOrder(int order) => Order = order;

    public bool HasOption(Guid optionId) => _options.Any(o => o.Id == optionId);

    private static (QuestionMediaType, string?) ValidateMedia(QuestionMediaType mediaType, string? mediaUrl)
    {
        var url = Guard.OptionalAbsoluteUrl(mediaUrl, "URL da midia", 500);

        if (mediaType == QuestionMediaType.None) return (QuestionMediaType.None, null);

        Guard.Requires(url is not null, "Informe a URL da imagem ou do audio.");
        return (mediaType, url);
    }
}
