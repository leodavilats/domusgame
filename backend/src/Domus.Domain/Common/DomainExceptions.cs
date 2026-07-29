namespace Domus.Domain.Common;

public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}

public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string message) : base(message) { }
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public static NotFoundException For(string resource) => new($"{resource} não encontrado(a).");
}
