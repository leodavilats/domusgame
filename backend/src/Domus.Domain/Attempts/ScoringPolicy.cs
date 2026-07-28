using Domus.Domain.Rounds;

namespace Domus.Domain.Attempts;

/// <summary>
/// Unico lugar do sistema que sabe transformar acerto + tempo em pontos (RN-23 a RN-27).
/// Puro e estatico: coberto pela tabela de verdade em Domus.Domain.Tests/ScoringPolicyTests.
/// </summary>
public static class ScoringPolicy
{
    /// <summary>Tolerancia de rede: uma resposta enviada ate 3s depois do limite ainda e aceita (RN-17).</summary>
    public const int NetworkGraceSeconds = 3;

    public static long DeadlineMs(RoundScoringSettings scoring) =>
        (scoring.QuestionTimeLimitSeconds + NetworkGraceSeconds) * 1000L;

    public static bool IsWithinTimeLimit(long elapsedMs, RoundScoringSettings scoring) =>
        elapsedMs <= DeadlineMs(scoring);

    public static int BasePoints(bool isCorrect, RoundScoringSettings scoring) =>
        isCorrect ? scoring.PointsPerCorrectAnswer : 0;

    /// <summary>
    /// Bonus proporcional ao tempo restante, apenas para respostas corretas.
    /// Responder instantaneamente rende o bonus cheio; responder no limite rende zero.
    /// </summary>
    public static int SpeedBonus(bool isCorrect, long elapsedMs, RoundScoringSettings scoring)
    {
        if (!isCorrect || scoring.MaxSpeedBonus == 0) return 0;

        var limitMs = scoring.QuestionTimeLimitSeconds * 1000.0;
        var remainingRatio = Math.Clamp(1.0 - elapsedMs / limitMs, 0.0, 1.0);

        return (int)Math.Round(scoring.MaxSpeedBonus * remainingRatio, MidpointRounding.AwayFromZero);
    }
}
