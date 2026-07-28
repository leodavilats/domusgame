namespace Domus.Api.Common;

/// <summary>Nomes das politicas de rate limiting (RNF-07).</summary>
public static class RateLimitPolicies
{
    /// <summary>Cadastro e login: protege contra forca bruta.</summary>
    public const string Auth = "auth";

    /// <summary>Envio de respostas: protege contra automacao durante o quiz.</summary>
    public const string Answers = "answers";
}
