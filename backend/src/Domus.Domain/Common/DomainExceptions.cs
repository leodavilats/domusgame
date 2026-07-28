namespace Domus.Domain.Common;

/// <summary>Entrada invalida: viola uma invariante de formato/faixa. Mapeada para HTTP 400.</summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}

/// <summary>
/// Operação invalida para o estado atual do agregado (rodada publicada, tentativa concluida,
/// pergunta fora de ordem). Mapeada para HTTP 409.
/// </summary>
public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string message) : base(message) { }
}

/// <summary>Agregado inexistente. Mapeada para HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public static NotFoundException For(string resource) => new($"{resource} não encontrado(a).");
}
