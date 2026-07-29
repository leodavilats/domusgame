using System.Net.Http.Json;
using System.Text.Json;

namespace Domus.Api.Tests;

internal sealed record PreparedRound(Guid SeasonId, Guid RoundId, int QuestionCount);

internal static class RoundBuilder
{
    public static async Task<Guid> CreateSeasonAsync(HttpClient admin, string name, bool activate = false)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/seasons", new
        {
            name,
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            endsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60))
        });

        var seasonId = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();

        if (activate)
        {
            var activated = await admin.PostAsync($"/api/admin/seasons/{seasonId}/activate", null);
            activated.EnsureSuccessStatusCode();
        }

        return seasonId;
    }

    public static async Task<Guid> CreateDraftRoundAsync(
        HttpClient admin,
        Guid seasonId,
        int weekNumber,
        DateTimeOffset opensAt,
        DateTimeOffset closesAt,
        int questionTimeLimitSeconds = 45)
    {
        var round = await admin.PostAsJsonAsync("/api/admin/rounds", new
        {
            seasonId,
            weekNumber,
            title = $"Semana {weekNumber}",
            opensAt,
            closesAt,
            pointsPerCorrectAnswer = 10,
            maxSpeedBonus = 5,
            questionTimeLimitSeconds
        });

        round.EnsureSuccessStatusCode();
        return (await round.ReadJsonAsync()).GetProperty("round").GetProperty("id").GetGuid();
    }

    public static async Task<PreparedRound> CreateOpenRoundAsync(
        HttpClient admin,
        string scenario,
        int questionCount = 3,
        int questionTimeLimitSeconds = 45,
        bool activateSeason = false)
    {
        var seasonId = await CreateSeasonAsync(admin, $"Temporada {scenario}", activateSeason);
        var now = DateTimeOffset.UtcNow;

        var roundId = await CreateDraftRoundAsync(
            admin, seasonId, weekNumber: 1, now.AddHours(-1), now.AddDays(6), questionTimeLimitSeconds);

        await FillAsync(admin, roundId, questionCount);
        await PublishAsync(admin, roundId);

        return new PreparedRound(seasonId, roundId, questionCount);
    }

    public static async Task FillAsync(HttpClient admin, Guid roundId, int questionCount)
    {
        var lesson = await admin.PutAsJsonAsync($"/api/admin/rounds/{roundId}/lesson", new
        {
            title = "Lição de teste",
            scriptureReference = "Joao 1.1",
            content = "Conteúdo da licao de teste.",
            externalUrl = (string?)null
        });
        lesson.EnsureSuccessStatusCode();

        for (var i = 1; i <= questionCount; i++)
        {
            var question = await admin.PostAsJsonAsync($"/api/admin/rounds/{roundId}/questions", new
            {
                text = $"Pergunta {i}?",
                mediaType = "None",
                mediaUrl = (string?)null,
                explanation = $"Explicação {i}",
                options = new[]
                {
                    new { text = $"Correta {i}", isCorrect = true },
                    new { text = $"Errada {i}A", isCorrect = false },
                    new { text = $"Errada {i}B", isCorrect = false }
                }
            });
            question.EnsureSuccessStatusCode();
        }
    }

    public static async Task<HttpResponseMessage> PublishAsync(HttpClient admin, Guid roundId)
    {
        var response = await admin.PostAsync($"/api/admin/rounds/{roundId}/publish", null);
        return response;
    }

    public static async Task<Guid> CorrectOptionIdAsync(HttpClient admin, Guid roundId, Guid questionId)
    {
        var detail = await (await admin.GetAsync($"/api/admin/rounds/{roundId}")).ReadJsonAsync();

        foreach (var question in detail.GetProperty("questions").EnumerateArray())
        {
            if (question.GetProperty("id").GetGuid() != questionId) continue;

            foreach (var option in question.GetProperty("options").EnumerateArray())
            {
                if (option.GetProperty("isCorrect").GetBoolean())
                {
                    return option.GetProperty("id").GetGuid();
                }
            }
        }

        throw new InvalidOperationException("Alternativa correta não encontrada.");
    }

    public static async Task<IReadOnlyList<Guid>> AllCorrectOptionIdsAsync(HttpClient admin, Guid roundId)
    {
        var detail = await (await admin.GetAsync($"/api/admin/rounds/{roundId}")).ReadJsonAsync();
        var ids = new List<Guid>();

        foreach (var question in detail.GetProperty("questions").EnumerateArray())
        {
            foreach (var option in question.GetProperty("options").EnumerateArray())
            {
                if (option.GetProperty("isCorrect").GetBoolean())
                {
                    ids.Add(option.GetProperty("id").GetGuid());
                }
            }
        }

        return ids;
    }

    public static JsonElement CurrentQuestion(JsonElement attemptState) =>
        attemptState.GetProperty("currentQuestion");
}
