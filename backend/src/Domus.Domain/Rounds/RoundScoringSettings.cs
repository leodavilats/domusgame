using Domus.Domain.Common;

namespace Domus.Domain.Rounds;

/// <summary>
/// Parametros de pontuacao da rodada. Copiados para dentro da tentativa no inicio dela,
/// para que o historico nunca mude se a rodada for reconfigurada (RN-28).
/// </summary>
public sealed class RoundScoringSettings
{
    public const int DefaultPointsPerCorrectAnswer = 10;
    public const int DefaultMaxSpeedBonus = 5;
    public const int DefaultQuestionTimeLimitSeconds = 45;

    private RoundScoringSettings() { }

    private RoundScoringSettings(int pointsPerCorrectAnswer, int maxSpeedBonus, int questionTimeLimitSeconds)
    {
        PointsPerCorrectAnswer = Guard.InRange(pointsPerCorrectAnswer, 1, 100, "Pontos por acerto");
        MaxSpeedBonus = Guard.InRange(maxSpeedBonus, 0, 50, "Bonus maximo de velocidade");
        QuestionTimeLimitSeconds = Guard.InRange(questionTimeLimitSeconds, 10, 300, "Tempo por pergunta");
    }

    public int PointsPerCorrectAnswer { get; private set; }
    public int MaxSpeedBonus { get; private set; }
    public int QuestionTimeLimitSeconds { get; private set; }

    public static RoundScoringSettings Default => new(
        DefaultPointsPerCorrectAnswer,
        DefaultMaxSpeedBonus,
        DefaultQuestionTimeLimitSeconds);

    public static RoundScoringSettings Create(int pointsPerCorrectAnswer, int maxSpeedBonus, int questionTimeLimitSeconds) =>
        new(pointsPerCorrectAnswer, maxSpeedBonus, questionTimeLimitSeconds);

    public RoundScoringSettings Copy() => new(PointsPerCorrectAnswer, MaxSpeedBonus, QuestionTimeLimitSeconds);

    public int MaxPointsPerQuestion => PointsPerCorrectAnswer + MaxSpeedBonus;
}
