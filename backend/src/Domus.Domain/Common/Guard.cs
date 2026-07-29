namespace Domus.Domain.Common;

public static class Guard
{
    public static string Text(string? value, string field, int maxLength, int minLength = 1)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length < minLength)
        {
            throw new DomainValidationException(minLength == 1
                ? $"{field} e obrigatorio."
                : $"{field} deve ter ao menos {minLength} caracteres.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException($"{field} deve ter no maximo {maxLength} caracteres.");
        }

        return trimmed;
    }

    public static string? OptionalText(string? value, string field, int maxLength)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0) return null;
        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException($"{field} deve ter no maximo {maxLength} caracteres.");
        }

        return trimmed;
    }

    public static string? OptionalAbsoluteUrl(string? value, string field, int maxLength)
    {
        var trimmed = OptionalText(value, field, maxLength);
        if (trimmed is null) return null;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainValidationException($"{field} deve ser uma URL comecando com http:// ou https://.");
        }

        return trimmed;
    }

    public static int InRange(int value, int min, int max, string field)
    {
        if (value < min || value > max)
        {
            throw new DomainValidationException($"{field} deve estar entre {min} e {max}.");
        }

        return value;
    }

    public static void Requires(bool condition, string message)
    {
        if (!condition) throw new DomainValidationException(message);
    }

    public static void State(bool condition, string message)
    {
        if (!condition) throw new DomainRuleException(message);
    }
}
