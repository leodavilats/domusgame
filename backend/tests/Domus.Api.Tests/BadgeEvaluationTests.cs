using Domus.Domain.Badges;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Domus.Api.Tests;

[Collection(ApiCollection.Name)]
public class BadgeEvaluationTests(ApiFixture fixture)
{
    [Fact]
    public async Task Concede_sarca_ardente_e_tabuas_da_lei_na_primeira_rodada_perfeita()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "selo-perfeito", questionCount: 1);
        var participant = await fixture.RegisterParticipantAsync("Selado", "selado@teste.local");

        var state = await (await participant.PostAsync($"/api/rounds/{round.RoundId}/attempts", null)).ReadJsonAsync();
        var attemptId = state.GetProperty("attemptId").GetGuid();
        var question = RoundBuilder.CurrentQuestion(state);
        var questionId = question.GetProperty("id").GetGuid();
        var correctId = await RoundBuilder.CorrectOptionIdAsync(admin, round.RoundId, questionId);

        await participant.PostAsJsonAsync($"/api/attempts/{attemptId}/answers", new { questionId, selectedOptionId = correctId });

        var result = await (await participant.GetAsync($"/api/attempts/{attemptId}/result")).ReadJsonAsync();
        var newlyAwarded = result.GetProperty("newlyAwardedBadges").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        Assert.Contains("SarcaArdente", newlyAwarded);
        Assert.Contains("TabuasDaLei", newlyAwarded);

        var me = await (await participant.GetAsync("/api/auth/me")).ReadJsonAsync();
        var codes = me.GetProperty("badges").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString())
            .ToList();

        Assert.Contains("SarcaArdente", codes);
        Assert.Contains("TabuasDaLei", codes);
    }

    [Fact]
    public async Task Nao_concede_tabuas_da_lei_quando_a_tentativa_erra_uma_pergunta()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "selo-errado", questionCount: 1);
        var participant = await fixture.RegisterParticipantAsync("Errante", "errante@teste.local");

        var state = await (await participant.PostAsync($"/api/rounds/{round.RoundId}/attempts", null)).ReadJsonAsync();
        var attemptId = state.GetProperty("attemptId").GetGuid();
        var question = RoundBuilder.CurrentQuestion(state);
        var questionId = question.GetProperty("id").GetGuid();
        var correctId = await RoundBuilder.CorrectOptionIdAsync(admin, round.RoundId, questionId);
        var wrongOptionId = question.GetProperty("options").EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid())
            .First(id => id != correctId);

        await participant.PostAsJsonAsync(
            $"/api/attempts/{attemptId}/answers", new { questionId, selectedOptionId = wrongOptionId });

        var result = await (await participant.GetAsync($"/api/attempts/{attemptId}/result")).ReadJsonAsync();
        var newlyAwarded = result.GetProperty("newlyAwardedBadges").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        Assert.Contains("SarcaArdente", newlyAwarded);
        Assert.DoesNotContain("TabuasDaLei", newlyAwarded);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DomusDbContext>();

        Assert.Equal(0, await db.ParticipantBadges.CountAsync(b => b.Code == BadgeCode.TabuasDaLei));
    }
}
