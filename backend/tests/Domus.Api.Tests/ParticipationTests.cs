using System.Net;
using System.Net.Http.Json;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Domus.Api.Tests;

[Collection(ApiCollection.Name)]
public class ParticipationTests(ApiFixture fixture)
{
    /// <summary>RNF-02: o requisito de seguranca mais importante do projeto.</summary>
    [Fact]
    public async Task Rodada_aberta_nunca_revela_o_gabarito()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "vazamento");
        var correctIds = await RoundBuilder.AllCorrectOptionIdsAsync(admin, round.RoundId);

        var participant = await fixture.RegisterParticipantAsync("Vazamento", "vazamento@teste.local");

        var startBody = await (await participant.PostAsync($"/api/rounds/{round.RoundId}/attempts", null)).ReadRawAsync();
        var currentBody = await (await participant.GetAsync($"/api/rounds/{round.RoundId}/attempts/current")).ReadRawAsync();

        var state = await (await participant.GetAsync($"/api/rounds/{round.RoundId}/attempts/current")).ReadJsonAsync();
        var question = RoundBuilder.CurrentQuestion(state);

        var submitBody = await (await participant.PostAsJsonAsync(
            $"/api/attempts/{state.GetProperty("attemptId").GetGuid()}/answers",
            new
            {
                questionId = question.GetProperty("id").GetGuid(),
                selectedOptionId = question.GetProperty("options")[0].GetProperty("id").GetGuid()
            })).ReadRawAsync();

        foreach (var body in new[] { startBody, currentBody, submitBody })
        {
            Assert.DoesNotContain("isCorrect", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("correctOption", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("explanation", body, StringComparison.OrdinalIgnoreCase);
        }

        // A pergunta corrente traz seus proprios ids (inclusive o da correta, sem marcacao),
        // mas o gabarito das demais perguntas nunca pode aparecer.
        var leaked = correctIds.Count(id => startBody.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.True(leaked <= 1, $"Ids corretos expostos: {leaked}");
    }

    [Fact]
    public async Task Gabarito_e_ranking_ficam_bloqueados_enquanto_a_rodada_esta_aberta()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "bloqueio");
        var participant = await fixture.RegisterParticipantAsync("Curioso", "curioso@teste.local");

        var review = await participant.GetAsync($"/api/rounds/{round.RoundId}/review");
        var ranking = await participant.GetAsync($"/api/rankings/round/{round.RoundId}");

        Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, ranking.StatusCode);
    }

    /// <summary>RN-14 / RNF-04: duplo clique não pode gerar duas tentativas.</summary>
    [Fact]
    public async Task Tentativa_e_unica_mesmo_com_requisicoes_simultaneas()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "concorrencia");
        var participant = await fixture.RegisterParticipantAsync("Concorrente", "concorrente@teste.local");

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => participant.PostAsync($"/api/rounds/{round.RoundId}/attempts", null)));

        Assert.All(responses, response => Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict,
            $"Status inesperado: {(int)response.StatusCode}"));

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomusDbContext>();

        Assert.Equal(1, await db.Attempts.CountAsync(a => a.RoundId == round.RoundId));
    }

    /// <summary>RNF-05: reenvio da mesma resposta não repontua.</summary>
    [Fact]
    public async Task Reenvio_da_mesma_resposta_e_idempotente()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "idempotencia", questionCount: 2);
        var participant = await fixture.RegisterParticipantAsync("Idempotente", "idempotente@teste.local");

        var state = await (await participant.PostAsync($"/api/rounds/{round.RoundId}/attempts", null)).ReadJsonAsync();
        var attemptId = state.GetProperty("attemptId").GetGuid();
        var questionId = RoundBuilder.CurrentQuestion(state).GetProperty("id").GetGuid();
        var correctId = await RoundBuilder.CorrectOptionIdAsync(admin, round.RoundId, questionId);

        var payload = new { questionId, selectedOptionId = correctId };

        var first = await (await participant.PostAsJsonAsync($"/api/attempts/{attemptId}/answers", payload)).ReadJsonAsync();
        var repeat = await (await participant.PostAsJsonAsync($"/api/attempts/{attemptId}/answers", payload)).ReadJsonAsync();

        Assert.Equal(first.GetProperty("answerId").GetGuid(), repeat.GetProperty("answerId").GetGuid());

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomusDbContext>();

        Assert.Equal(1, await db.AttemptAnswers.CountAsync(a => a.AttemptId == attemptId && a.QuestionId == questionId));
    }

    [Fact]
    public async Task Pergunta_fora_de_ordem_e_recusada()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "ordem", questionCount: 3);
        var participant = await fixture.RegisterParticipantAsync("Pulador", "pulador@teste.local");

        var state = await (await participant.PostAsync($"/api/rounds/{round.RoundId}/attempts", null)).ReadJsonAsync();
        var attemptId = state.GetProperty("attemptId").GetGuid();
        var servedQuestionId = RoundBuilder.CurrentQuestion(state).GetProperty("id").GetGuid();

        var detail = await (await admin.GetAsync($"/api/admin/rounds/{round.RoundId}")).ReadJsonAsync();

        var otherQuestionId = detail.GetProperty("questions").EnumerateArray()
            .Select(q => q.GetProperty("id").GetGuid())
            .First(id => id != servedQuestionId);

        var response = await participant.PostAsJsonAsync($"/api/attempts/{attemptId}/answers", new
        {
            questionId = otherQuestionId,
            selectedOptionId = (Guid?)null
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Fluxo_completo_pontua_e_conclui_a_tentativa()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "fluxo", questionCount: 3);
        var participant = await fixture.RegisterParticipantAsync("Completo", "completo@teste.local");

        var state = await (await participant.PostAsync($"/api/rounds/{round.RoundId}/attempts", null)).ReadJsonAsync();
        var attemptId = state.GetProperty("attemptId").GetGuid();
        var question = RoundBuilder.CurrentQuestion(state);

        for (var i = 0; i < round.QuestionCount; i++)
        {
            var questionId = question.GetProperty("id").GetGuid();
            var correctId = await RoundBuilder.CorrectOptionIdAsync(admin, round.RoundId, questionId);

            var response = await (await participant.PostAsJsonAsync($"/api/attempts/{attemptId}/answers", new
            {
                questionId,
                selectedOptionId = correctId
            })).ReadJsonAsync();

            if (response.GetProperty("attemptFinished").GetBoolean()) break;

            question = response.GetProperty("nextQuestion");
        }

        var result = await (await participant.GetAsync($"/api/attempts/{attemptId}/result")).ReadJsonAsync();

        Assert.Equal("Completed", result.GetProperty("status").GetString());
        Assert.Equal(3, result.GetProperty("correctCount").GetInt32());
        Assert.Equal(45, result.GetProperty("maxPoints").GetInt32());
        Assert.True(result.GetProperty("totalPoints").GetInt32() >= 30);
        Assert.False(result.GetProperty("answersRevealed").GetBoolean());
    }

    [Fact]
    public async Task Painel_mostra_a_rodada_da_semana_e_o_streak()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "painel", questionCount: 2, activateSeason: true);
        var participant = await fixture.RegisterParticipantAsync("Painel", "painel@teste.local");

        var dashboard = await (await participant.GetAsync("/api/dashboard")).ReadJsonAsync();

        Assert.Equal(round.RoundId, dashboard.GetProperty("round").GetProperty("id").GetGuid());
        Assert.Equal("Open", dashboard.GetProperty("round").GetProperty("availability").GetString());
        Assert.True(dashboard.GetProperty("actions").GetProperty("canStart").GetBoolean());
        Assert.False(dashboard.GetProperty("actions").GetProperty("canReview").GetBoolean());
    }
}
