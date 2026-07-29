using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Domus.Api.Tests;

[Collection(ApiCollection.Name)]
public class AuthorizationTests(ApiFixture fixture)
{
    [Fact]
    public async Task Rotas_protegidas_exigem_sessao()
    {
        var anonymous = fixture.CreateClient();

        foreach (var route in new[] { "/api/dashboard", "/api/rounds", "/api/rankings/season", "/api/admin/seasons" })
        {
            var response = await anonymous.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Participante_nao_acessa_area_administrativa()
    {
        var participant = await fixture.RegisterParticipantAsync("Sem Poder", "sempoder@teste.local");

        var seasons = await participant.GetAsync("/api/admin/seasons");
        var invite = await participant.GetAsync("/api/admin/invite");

        Assert.Equal(HttpStatusCode.Forbidden, seasons.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, invite.StatusCode);
    }

    [Fact]
    public async Task Cadastro_nao_exige_codigo_de_convite()
    {
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Recem Chegado",
            email = "recemchegado@teste.local",
            password = "Teste@12345"
        });

        var me = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(HasValue(me, "room"));
    }

    [Fact]
    public async Task Sala_recusa_codigo_invalido()
    {
        var client = await fixture.RegisterWithoutRoomAsync("Codigo Errado", "codigoerrado@teste.local");

        var response = await client.PostAsJsonAsync("/api/rooms/join", new { inviteCode = "ERRADO" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sem_sala_o_participante_nao_ve_conteudo_do_gc()
    {
        var client = await fixture.RegisterWithoutRoomAsync("Sem Sala", "semsala@teste.local");

        var dashboard = await (await client.GetAsync("/api/dashboard")).ReadJsonAsync();
        var ranking = await client.GetAsync("/api/rankings/season");
        var rounds = await client.GetAsync("/api/rounds");

        Assert.False(HasValue(dashboard, "room"));
        Assert.False(HasValue(dashboard, "season"));
        Assert.Equal(HttpStatusCode.Forbidden, ranking.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rounds.StatusCode);
    }

    [Fact]
    public async Task Entrar_na_sala_e_idempotente()
    {
        var client = await fixture.RegisterWithoutRoomAsync("Entrou Duas Vezes", "duasvezes@teste.local");

        var first = await (await client.PostAsJsonAsync(
            "/api/rooms/join", new { inviteCode = ApiFixture.InviteCode })).ReadJsonAsync();

        var second = await (await client.PostAsJsonAsync(
            "/api/rooms/join", new { inviteCode = ApiFixture.InviteCode.ToLowerInvariant() })).ReadJsonAsync();

        Assert.Equal(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.Equal(first.GetProperty("memberCount").GetInt32(), second.GetProperty("memberCount").GetInt32());

        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        Assert.Equal(first.GetProperty("id").GetGuid(), me.GetProperty("room").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Nome_de_exibicao_e_unico_ignorando_caixa()
    {
        await fixture.RegisterParticipantAsync("Nome Repetido", "repetido1@teste.local");

        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "nome repetido",
            email = "repetido2@teste.local",
            password = "Teste@12345"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rodada_aberta_nao_aceita_edicao()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "edicao", questionCount: 2);

        var response = await admin.PostAsJsonAsync($"/api/admin/rounds/{round.RoundId}/questions", new
        {
            text = "Pergunta extra?",
            mediaType = "None",
            mediaUrl = (string?)null,
            explanation = (string?)null,
            options = new[]
            {
                new { text = "A", isCorrect = true },
                new { text = "B", isCorrect = false }
            }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Semana_duplicada_na_mesma_temporada_e_recusada()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var seasonId = await RoundBuilder.CreateSeasonAsync(admin, "Temporada semana duplicada");
        var now = DateTimeOffset.UtcNow;

        await RoundBuilder.CreateDraftRoundAsync(admin, seasonId, 7, now.AddDays(10), now.AddDays(16));

        var duplicated = await admin.PostAsJsonAsync("/api/admin/rounds", new
        {
            seasonId,
            weekNumber = 7,
            title = "Outra semana 7",
            opensAt = now.AddDays(20),
            closesAt = now.AddDays(26),
            pointsPerCorrectAnswer = 10,
            maxSpeedBonus = 5,
            questionTimeLimitSeconds = 45
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicated.StatusCode);
    }

    [Fact]
    public async Task Publicacao_bloqueia_janela_sobreposta()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var seasonId = await RoundBuilder.CreateSeasonAsync(admin, "Temporada sobreposicao");
        var now = DateTimeOffset.UtcNow;

        var first = await RoundBuilder.CreateDraftRoundAsync(admin, seasonId, 1, now.AddDays(1), now.AddDays(7));
        await RoundBuilder.FillAsync(admin, first, 1);
        (await RoundBuilder.PublishAsync(admin, first)).EnsureSuccessStatusCode();

        var overlapping = await RoundBuilder.CreateDraftRoundAsync(admin, seasonId, 2, now.AddDays(5), now.AddDays(11));
        await RoundBuilder.FillAsync(admin, overlapping, 1);

        var publish = await RoundBuilder.PublishAsync(admin, overlapping);

        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);
    }

    [Fact]
    public async Task Publicacao_lista_o_que_falta_quando_a_rodada_esta_incompleta()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var seasonId = await RoundBuilder.CreateSeasonAsync(admin, "Temporada incompleta");
        var now = DateTimeOffset.UtcNow;

        var roundId = await RoundBuilder.CreateDraftRoundAsync(admin, seasonId, 1, now.AddDays(1), now.AddDays(7));

        var problems = await (await admin.GetAsync($"/api/admin/rounds/{roundId}/validate")).ReadJsonAsync();
        var publish = await RoundBuilder.PublishAsync(admin, roundId);

        Assert.True(problems.GetArrayLength() >= 2);
        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);
    }

    [Fact]
    public async Task Apenas_uma_temporada_fica_ativa()
    {
        var admin = await fixture.LoginAsAdminAsync();

        var first = await RoundBuilder.CreateSeasonAsync(admin, "Ativa A", activate: true);
        var second = await RoundBuilder.CreateSeasonAsync(admin, "Ativa B", activate: true);

        var seasons = await (await admin.GetAsync("/api/admin/seasons")).ReadJsonAsync();

        var actives = seasons.EnumerateArray()
            .Where(s => s.GetProperty("status").GetString() == "Active")
            .Select(s => s.GetProperty("id").GetGuid())
            .ToList();

        Assert.Single(actives);
        Assert.Equal(second, actives[0]);
        Assert.NotEqual(first, actives[0]);
    }

    [Fact]
    public async Task Rodada_agendada_aceita_edicao_e_exclusao()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var seasonId = await RoundBuilder.CreateSeasonAsync(admin, "Temporada agendada editavel");
        var now = DateTimeOffset.UtcNow;

        var roundId = await RoundBuilder.CreateDraftRoundAsync(admin, seasonId, 1, now.AddDays(3), now.AddDays(9));
        await RoundBuilder.FillAsync(admin, roundId, 2);
        (await RoundBuilder.PublishAsync(admin, roundId)).EnsureSuccessStatusCode();

        var published = await (await admin.GetAsync($"/api/admin/rounds/{roundId}")).ReadJsonAsync();
        Assert.Equal("Scheduled", published.GetProperty("round").GetProperty("availability").GetString());
        Assert.True(published.GetProperty("canEdit").GetBoolean());
        Assert.True(published.GetProperty("canDelete").GetBoolean());

        var update = await admin.PutAsJsonAsync($"/api/admin/rounds/{roundId}", new
        {
            weekNumber = 1,
            title = "Titulo corrigido depois de publicar",
            opensAt = now.AddDays(4),
            closesAt = now.AddDays(10),
            pointsPerCorrectAnswer = 10,
            maxSpeedBonus = 5,
            questionTimeLimitSeconds = 45
        });
        update.EnsureSuccessStatusCode();

        var question = await admin.PostAsJsonAsync($"/api/admin/rounds/{roundId}/questions", new
        {
            text = "Pergunta acrescentada apos publicar?",
            mediaType = "None",
            mediaUrl = (string?)null,
            explanation = (string?)null,
            options = new[]
            {
                new { text = "A", isCorrect = true },
                new { text = "B", isCorrect = false }
            }
        });
        question.EnsureSuccessStatusCode();

        var detail = await (await admin.GetAsync($"/api/admin/rounds/{roundId}")).ReadJsonAsync();
        Assert.Equal("Titulo corrigido depois de publicar", detail.GetProperty("round").GetProperty("title").GetString());
        Assert.Equal(3, detail.GetProperty("questions").GetArrayLength());
        Assert.Equal("Published", detail.GetProperty("status").GetString());

        var delete = await admin.DeleteAsync($"/api/admin/rounds/{roundId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var gone = await admin.GetAsync($"/api/admin/rounds/{roundId}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Rodada_aberta_nao_pode_ser_excluida()
    {
        var admin = await fixture.LoginAsAdminAsync();
        var round = await RoundBuilder.CreateOpenRoundAsync(admin, "exclusao-bloqueada", questionCount: 1);

        var detail = await (await admin.GetAsync($"/api/admin/rounds/{round.RoundId}")).ReadJsonAsync();
        Assert.False(detail.GetProperty("canEdit").GetBoolean());
        Assert.False(detail.GetProperty("canDelete").GetBoolean());

        var delete = await admin.DeleteAsync($"/api/admin/rounds/{round.RoundId}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task Quem_nao_esta_na_sala_fica_fora_da_lista_de_pessoas()
    {
        var admin = await fixture.LoginAsAdminAsync();
        await fixture.RegisterWithoutRoomAsync("Fora Da Sala", "foradasala@teste.local");
        await fixture.RegisterParticipantAsync("Dentro Da Sala", "dentrodasala@teste.local");

        var participants = await (await admin.GetAsync("/api/admin/participants")).ReadJsonAsync();

        var names = participants.EnumerateArray()
            .Select(p => p.GetProperty("displayName").GetString())
            .ToList();

        Assert.Contains("Dentro Da Sala", names);
        Assert.DoesNotContain("Fora Da Sala", names);
    }

    private static bool HasValue(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null;
}
