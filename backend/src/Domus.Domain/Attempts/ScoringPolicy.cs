using Domus.Domain.Rounds;

namespace Domus.Domain.Attempts;

public static class ScoringPolicy
{
    public const int NetworkGraceSeconds = 3;

    public static long DeadlineMs(RoundScoringSettings scoring) =>
        (scoring.QuestionTimeLimitSeconds + NetworkGraceSeconds) * 1000L;

    public static bool IsWithinTimeLimit(long elapsedMs, RoundScoringSettings scoring) =>
        elapsedMs <= DeadlineMs(scoring);

    public static int BasePoints(bool isCorrect, RoundScoringSettings scoring) =>
        isCorrect ? scoring.PointsPerCorrectAnswer : 0;

    public static int SpeedBonus(bool isCorrect, long elapsedMs, RoundScoringSettings scoring)
    {
        if (!isCorrect || scoring.MaxSpeedBonus == 0) return 0;

        var limitMs = scoring.QuestionTimeLimitSeconds * 1000.0;
        var remainingRatio = Math.Clamp(1.0 - elapsedMs / limitMs, 0.0, 1.0);

        return (int)Math.Round(scoring.MaxSpeedBonus * remainingRatio, MidpointRounding.AwayFromZero);
    }
}
