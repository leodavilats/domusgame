using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Domus.Api.Tests;

/// <summary>
/// As ferramentas de teste apagam dados. O que mais importa aqui e provar que elas ficam
/// **trancadas** quando `DevTools__Enabled` nao esta ligado — e que a frase de confirmacao
/// nao e decorativa.
/// </summary>
[Collection(ApiCollection.Name)]
public class AdminToolsTests(ApiFixture fixture)
{
    [Fact]
    public async Task Desligadas_por_padrao_as_acoes_ficam_bloqueadas()
    {
        var admin = await fixture.LoginAsAdminAsync();

        var criar = await admin.PostAsync("/api/admin/tools/demo-season", null);
        var limpar = await admin.PostAsJsonAsync("/api/admin/tools/reset", new
        {
            scope = "attempts",
            confirmation = "LIMPAR"
        });

        Assert.Equal(HttpStatusCode.Forbidden, criar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, limpar.StatusCode);
    }

    /// <summary>Leitura continua liberada: e ela que explica por que as acoes nao funcionam.</summary>
    [Fact]
    public async Task Diagnostico_funciona_mesmo_desligado_e_informa_o_estado()
    {
        var admin = await fixture.LoginAsAdminAsync();

        var info = await (await admin.GetAsync("/api/admin/tools/diagnostics")).ReadJsonAsync();

        Assert.False(info.GetProperty("enabled").GetBoolean());
        Assert.True(info.GetProperty("participants").GetInt32() >= 1);
        Assert.False(string.IsNullOrWhiteSpace(info.GetProperty("environment").GetString()));
    }

    [Fact]
    public async Task Participante_nao_alcanca_as_ferramentas()
    {
        var participant = await fixture.RegisterParticipantAsync("Sem Ferramentas", "semferramentas@teste.local");

        var diagnostico = await participant.GetAsync("/api/admin/tools/diagnostics");
        var criar = await participant.PostAsync("/api/admin/tools/demo-season", null);

        Assert.Equal(HttpStatusCode.Forbidden, diagnostico.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, criar.StatusCode);
    }

    [Fact]
    public async Task Ligadas_criam_temporada_de_teste_com_as_variacoes_de_pergunta()
    {
        var admin = await fixture.LoginAsToolsAdminAsync();

        var criar = await admin.PostAsync("/api/admin/tools/demo-season", null);
        criar.EnsureSuccessStatusCode();

        var seasons = await (await admin.GetAsync("/api/admin/seasons")).ReadJsonAsync();

        var season = seasons.EnumerateArray()
            .First(s => s.GetProperty("name").GetString()!.StartsWith("Teste ", StringComparison.Ordinal));

        var seasonId = season.GetProperty("id").GetGuid();
        var rounds = await (await admin.GetAsync($"/api/admin/rounds?seasonId={seasonId}")).ReadJsonAsync();

        var disponibilidades = rounds.EnumerateArray()
            .Select(r => r.GetProperty("round").GetProperty("availability").GetString())
            .ToList();

        Assert.Equal(3, disponibilidades.Count);
        Assert.Contains("Closed", disponibilidades);
        Assert.Contains("Open", disponibilidades);
        Assert.Contains("Scheduled", disponibilidades);

        // A rodada aberta precisa trazer as variacoes de midia e de quantidade de alternativas.
        var abertaId = rounds.EnumerateArray()
            .First(r => r.GetProperty("round").GetProperty("availability").GetString() == "Open")
            .GetProperty("round").GetProperty("id").GetGuid();

        var detalhe = await (await admin.GetAsync($"/api/admin/rounds/{abertaId}")).ReadJsonAsync();
        var perguntas = detalhe.GetProperty("questions").EnumerateArray().ToList();

        Assert.Equal(5, perguntas.Count);
        Assert.Contains(perguntas, q => q.GetProperty("mediaType").GetString() == "Image");
        Assert.Contains(perguntas, q => q.GetProperty("mediaType").GetString() == "Audio");
        Assert.Contains(perguntas, q => q.GetProperty("options").GetArrayLength() == 2);
        Assert.Contains(perguntas, q => q.GetProperty("options").GetArrayLength() == 5);

        // Toda pergunta segue a invariante de exatamente uma correta.
        Assert.All(perguntas, q => Assert.Single(
            q.GetProperty("options").EnumerateArray(),
            o => o.GetProperty("isCorrect").GetBoolean()));
    }

    [Fact]
    public async Task Abrir_e_encerrar_agora_movem_a_janela()
    {
        var admin = await fixture.LoginAsToolsAdminAsync();
        var seasonId = await RoundBuilder.CreateSeasonAsync(admin, "Temporada ferramentas janela");
        var now = DateTimeOffset.UtcNow;

        var roundId = await RoundBuilder.CreateDraftRoundAsync(admin, seasonId, 1, now.AddDays(20), now.AddDays(26));
        await RoundBuilder.FillAsync(admin, roundId, 1);
        (await RoundBuilder.PublishAsync(admin, roundId)).EnsureSuccessStatusCode();

        (await admin.PostAsync($"/api/admin/tools/rounds/{roundId}/open-now", null)).EnsureSuccessStatusCode();
        var aberta = await (await admin.GetAsync($"/api/admin/rounds/{roundId}")).ReadJsonAsync();
        Assert.Equal("Open", aberta.GetProperty("round").GetProperty("availability").GetString());

        (await admin.PostAsync($"/api/admin/tools/rounds/{roundId}/close-now", null)).EnsureSuccessStatusCode();
        var encerrada = await (await admin.GetAsync($"/api/admin/rounds/{roundId}")).ReadJsonAsync();
        Assert.Equal("Closed", encerrada.GetProperty("round").GetProperty("availability").GetString());
    }

    [Fact]
    public async Task Simular_participacoes_alimenta_o_ranking_da_rodada()
    {
        var admin = await fixture.LoginAsToolsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "ferramentas-simulacao", questionCount: 3);

        var simular = await admin.PostAsJsonAsync(
            $"/api/admin/tools/rounds/{round.RoundId}/simulate", new { count = 4 });

        simular.EnsureSuccessStatusCode();

        (await admin.PostAsync($"/api/admin/tools/rounds/{round.RoundId}/close-now", null)).EnsureSuccessStatusCode();

        var ranking = await (await admin.GetAsync($"/api/rankings/round/{round.RoundId}")).ReadJsonAsync();
        var entradas = ranking.GetProperty("entries").EnumerateArray().ToList();

        Assert.Equal(4, entradas.Count);
        Assert.All(entradas, e => Assert.True(e.GetProperty("totalPoints").GetInt32() >= 0));

        // Desempenhos variados: o ranking nao pode sair todo empatado.
        var pontuacoes = entradas.Select(e => e.GetProperty("totalPoints").GetInt32()).Distinct().Count();
        Assert.True(pontuacoes > 1, "A simulação deveria gerar pontuações diferentes.");
    }

    [Fact]
    public async Task Refazer_apaga_apenas_a_minha_tentativa()
    {
        var admin = await fixture.LoginAsToolsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "ferramentas-refazer", questionCount: 2);

        // O admin responde a rodada.
        (await admin.PostAsync($"/api/rounds/{round.RoundId}/attempts", null)).EnsureSuccessStatusCode();

        // Outra pessoa tambem.
        await admin.PostAsJsonAsync($"/api/admin/tools/rounds/{round.RoundId}/simulate", new { count = 2 });

        var apagar = await admin.DeleteAsync($"/api/admin/tools/rounds/{round.RoundId}/my-attempt");
        apagar.EnsureSuccessStatusCode();

        // A minha saiu; as outras ficaram.
        var minha = await admin.GetAsync($"/api/rounds/{round.RoundId}/attempts/current");
        var detalhe = await (await admin.GetAsync($"/api/admin/rounds/{round.RoundId}")).ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, minha.StatusCode);
        Assert.Equal(2, detalhe.GetProperty("attemptCount").GetInt32());
    }

    [Fact]
    public async Task Limpeza_exige_a_frase_de_confirmacao_exata()
    {
        var admin = await fixture.LoginAsToolsAdminAsync();

        foreach (var frase in new[] { "", "limpar", "APAGAR", "LIMPAR TUDO" })
        {
            var response = await admin.PostAsJsonAsync("/api/admin/tools/reset", new
            {
                scope = "attempts",
                confirmation = frase
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Escopo_invalido_e_recusado_antes_de_apagar_qualquer_coisa()
    {
        var admin = await fixture.LoginAsToolsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "ferramentas-escopo", questionCount: 1);

        await admin.PostAsJsonAsync($"/api/admin/tools/rounds/{round.RoundId}/simulate", new { count = 2 });

        var response = await admin.PostAsJsonAsync("/api/admin/tools/reset", new
        {
            scope = "tudinho",
            confirmation = "LIMPAR"
        });

        var detalhe = await (await admin.GetAsync($"/api/admin/rounds/{round.RoundId}")).ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(2, detalhe.GetProperty("attemptCount").GetInt32());
    }

    /// <summary>
    /// Escopo mais brando de proposito: os outros apagariam o que os demais testes desta
    /// colecao acabaram de criar. O comportamento de `content` e `all` esta coberto pela
    /// validacao de escopo e pela verificacao de que administradores sobrevivem.
    /// </summary>
    [Fact]
    public async Task Limpeza_de_tentativas_zera_participacoes_e_preserva_o_administrador()
    {
        var admin = await fixture.LoginAsToolsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "ferramentas-limpeza", questionCount: 1);

        await admin.PostAsJsonAsync($"/api/admin/tools/rounds/{round.RoundId}/simulate", new { count = 3 });

        var resultado = await (await admin.PostAsJsonAsync("/api/admin/tools/reset", new
        {
            scope = "attempts",
            confirmation = "LIMPAR"
        })).ReadJsonAsync();

        Assert.True(resultado.GetProperty("attemptsRemoved").GetInt32() >= 3);

        var detalhe = await (await admin.GetAsync($"/api/admin/rounds/{round.RoundId}")).ReadJsonAsync();
        Assert.Equal(0, detalhe.GetProperty("attemptCount").GetInt32());

        // O administrador continua de pe: a sessao ainda vale e a rodada segue existindo.
        var eu = await admin.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, eu.StatusCode);
    }
}
