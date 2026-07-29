using Domus.Domain.Rounds;

namespace Domus.Domain.Attempts;

public static class OptionShuffler
{
    public static IReadOnlyList<AnswerOption> ShuffleFor(Guid attemptId, Question question) =>
        [.. question.Options
            .OrderBy(option => StableHash(attemptId, option.Id))
            .ThenBy(option => option.Order)];

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
