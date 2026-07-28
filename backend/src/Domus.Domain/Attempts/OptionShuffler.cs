using Domus.Domain.Rounds;

namespace Domus.Domain.Attempts;

/// <summary>
/// RN-16: embaralha as alternativas por tentativa, de forma deterministica.
/// A mesma pessoa recarregando a pagina ve sempre a mesma ordem; pessoas diferentes veem ordens
/// diferentes. Nao usamos Guid.GetHashCode(), que varia entre processos.
/// </summary>
public static class OptionShuffler
{
    public static IReadOnlyList<AnswerOption> ShuffleFor(Guid attemptId, Question question) =>
        [.. question.Options
            .OrderBy(option => StableHash(attemptId, option.Id))
            .ThenBy(option => option.Order)];

    /// <summary>FNV-1a de 64 bits sobre os bytes dos dois identificadores.</summary>
    internal static ulong StableHash(Guid seed, Guid value)
    {
        Span<byte> buffer = stackalloc byte[32];
        seed.TryWriteBytes(buffer[..16]);
        value.TryWriteBytes(buffer[16..]);

        var hash = 14695981039346656037UL;
        foreach (var b in buffer)
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}
