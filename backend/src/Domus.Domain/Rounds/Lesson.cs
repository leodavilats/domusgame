using Domus.Domain.Common;

namespace Domus.Domain.Rounds;

public sealed class Lesson
{
    private Lesson()
    {
        Title = string.Empty;
        ScriptureReference = string.Empty;
        Content = string.Empty;
    }

    private Lesson(string title, string scriptureReference, string content, string? externalUrl)
    {
        Title = title;
        ScriptureReference = scriptureReference;
        Content = content;
        ExternalUrl = externalUrl;
    }

    public string Title { get; private set; }
    public string ScriptureReference { get; private set; }

    public string Content { get; private set; }

    public string? ExternalUrl { get; private set; }

    public static Lesson Empty() => new();

    public static Lesson Create(string title, string scriptureReference, string content, string? externalUrl) =>
        new(
            Guard.Text(title, "Titulo da licao", 160),
            Guard.Text(scriptureReference, "Referência bíblica", 160),
            Guard.Text(content, "Conteúdo da licao", 20_000),
            Guard.OptionalAbsoluteUrl(externalUrl, "Link da licao", 500));

    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Title) &&
        !string.IsNullOrWhiteSpace(ScriptureReference) &&
        !string.IsNullOrWhiteSpace(Content);
}
