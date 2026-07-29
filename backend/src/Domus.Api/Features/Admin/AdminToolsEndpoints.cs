using Domus.Api.Common;
using Domus.Domain.Attempts;
using Domus.Domain.Common;
using Domus.Domain.Participants;
using Domus.Domain.Rounds;
using Domus.Domain.Seasons;
using Domus.Domain.Settings;
using Domus.Infrastructure.Identity;
using Domus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Admin;

// ---------------------------------------------------------------------- contratos

public sealed record ToolsDiagnosticsDto(
    bool Enabled,
    string Environment,
    DateTimeOffset ServerNowUtc,
    string TimeZoneHint,
    string? ActiveSeasonName,
    string? AppliedMigration,
    int Seasons,
    int Rounds,
    int Questions,
    int Participants,
    int Attempts,
    int Answers);

public sealed record AuditEntryDto(DateTimeOffset OccurredAt, string ActorName, string Action, string? Details);

public sealed record ResetRequest(string Scope, string Confirmation);

public sealed record ResetResultDto(string Scope, int SeasonsRemoved, int RoundsRemoved, int AttemptsRemoved, int ParticipantsRemoved);

public sealed record SimulateRequest(int Count);

public sealed record ToolActionResultDto(string Message);

/// <summary>
/// Ferramentas de teste do painel administrativo.
///
/// **Desligadas por padrao.** Só existem com `DevTools__Enabled=true`, e as acoes destrutivas
/// exigem uma frase de confirmacao no corpo da requisicao. O motivo e simples: um botao
/// "limpar tudo" ao lado das telas de uso diario apaga o historico do GC em um toque errado.
///
/// O diagnostico e a auditoria continuam disponiveis mesmo com as ferramentas desligadas:
/// sao leitura e ajudam a entender o estado do ambiente.
/// </summary>
public static class AdminToolsEndpoints
{
    public const string ResetConfirmationPhrase = "LIMPAR";

    public static void MapAdminToolsEndpoints(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/tools");

        group.MapGet("/diagnostics", DiagnosticsAsync);
        group.MapGet("/audit", AuditAsync);

        group.MapPost("/demo-season", CreateTestSeasonAsync);
        group.MapPost("/rounds/{id:guid}/open-now", OpenNowAsync);
        group.MapPost("/rounds/{id:guid}/close-now", CloseNowAsync);
        group.MapPost("/rounds/{id:guid}/simulate", SimulateAsync);
        group.MapDelete("/rounds/{id:guid}/my-attempt", DeleteMyAttemptAsync);
        group.MapPost("/reset", ResetAsync);
    }

    // ------------------------------------------------------------------ leitura

    private static async Task<IResult> DiagnosticsAsync(
        IConfiguration configuration,
        IHostEnvironment environment,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var season = await queries.GetActiveSeasonAsync(ct);
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault();

        return Results.Ok(new ToolsDiagnosticsDto(
            IsEnabled(configuration),
            environment.EnvironmentName,
            queries.Now,
            configuration["App:TimeZone"] ?? "America/Sao_Paulo",
            season?.Name,
            applied,
            await db.Seasons.CountAsync(ct),
            await db.Rounds.CountAsync(ct),
            await db.Questions.CountAsync(ct),
            await db.Participants.CountAsync(ct),
            await db.Attempts.CountAsync(ct),
            await db.AttemptAnswers.CountAsync(ct)));
    }

    private static async Task<IResult> AuditAsync(DomusDbContext db, CancellationToken ct)
    {
        var entries = await db.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.OccurredAt)
            .Take(30)
            .Select(a => new AuditEntryDto(a.OccurredAt, a.ActorName, a.Action, a.Details))
            .ToListAsync(ct);

        return Results.Ok(entries);
    }

    // ------------------------------------------------------------------ temporada de teste

    /// <summary>
    /// Cria uma temporada com tres rodadas de um dia — uma encerrada, uma aberta e uma agendada —
    /// para exercitar gabarito, ranking e contagem regressiva sem esperar uma semana.
    ///
    /// As perguntas cobrem as variacoes que a producao vai ter: 2 a 5 alternativas, sem midia,
    /// com imagem e com audio. A midia e servida pelo proprio app (wwwroot), para nao depender
    /// de link externo que pode sair do ar.
    /// </summary>
    private static async Task<IResult> CreateTestSeasonAsync(
        HttpContext http,
        IConfiguration configuration,
        CurrentUser currentUser,
        DomusDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        EnsureEnabled(configuration);

        var now = clock.GetUtcNow();
        var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";

        var season = Season.Create(
            $"Teste {now:dd/MM HH:mm}",
            DateOnly.FromDateTime(now.UtcDateTime.AddDays(-3)),
            DateOnly.FromDateTime(now.UtcDateTime.AddDays(3)),
            now);

        db.Seasons.Add(season);

        // Janelas de um dia, sem sobreposicao (RN-12).
        var rounds = new[]
        {
            BuildRound(season.Id, 1, "Rodada encerrada (para ver gabarito)", now.AddDays(-2), now.AddDays(-1), now, baseUrl),
            BuildRound(season.Id, 2, "Rodada aberta (para responder agora)", now.AddMinutes(-5), now.AddHours(23), now, baseUrl),
            BuildRound(season.Id, 3, "Rodada agendada (para ver a contagem)", now.AddDays(1), now.AddDays(2), now, baseUrl)
        };

        db.Rounds.AddRange(rounds);

        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, "ToolsTestSeasonCreated", season.Name, now));

        await db.SaveChangesAsync(ct);

        return Results.Ok(new ToolActionResultDto(
            $"Temporada '{season.Name}' criada com 3 rodadas de um dia. " +
            "Ative-a em Temporadas para que apareça no painel dos participantes."));
    }

    private static Round BuildRound(
        Guid seasonId,
        int week,
        string title,
        DateTimeOffset opensAt,
        DateTimeOffset closesAt,
        DateTimeOffset now,
        string baseUrl)
    {
        var round = Round.CreateDraft(
            seasonId, week, title, opensAt, closesAt, RoundScoringSettings.Default, now);

        round.SetLesson(
            Lesson.Create(
                title,
                "Salmos 119.105",
                "## Lição de teste\n\n" +
                "Rodada gerada pelo painel de **ferramentas**. As perguntas são fáceis de propósito: " +
                "a ideia é exercitar o fluxo, não o conhecimento.\n\n" +
                "- uma pergunta sem mídia\n" +
                "- uma com imagem\n" +
                "- uma com áudio\n" +
                "- uma com só duas alternativas\n" +
                "- uma com cinco alternativas\n",
                null),
            now);

        // 1. simples, sem midia
        round.AddQuestion(
            "Quanto é 2 + 2?",
            QuestionMediaType.None, null,
            "Soma básica: 2 + 2 = 4.",
            [
                new AnswerOptionDraft("4", true),
                new AnswerOptionDraft("3", false),
                new AnswerOptionDraft("5", false),
                new AnswerOptionDraft("22", false)
            ],
            now);

        // 2. com imagem
        round.AddQuestion(
            "Na imagem, qual é a cor do sol desenhado?",
            QuestionMediaType.Image, $"{baseUrl}/exemplo-imagem.svg",
            "O sol está desenhado em amarelo, no canto superior direito.",
            [
                new AnswerOptionDraft("Amarelo", true),
                new AnswerOptionDraft("Azul", false),
                new AnswerOptionDraft("Verde", false)
            ],
            now);

        // 3. com audio
        round.AddQuestion(
            "No áudio, quantas notas você ouve?",
            QuestionMediaType.Audio, $"{baseUrl}/exemplo-audio.wav",
            "São duas notas curtas, separadas por uma pausa.",
            [
                new AnswerOptionDraft("Duas", true),
                new AnswerOptionDraft("Uma", false),
                new AnswerOptionDraft("Quatro", false)
            ],
            now);

        // 4. minimo de alternativas
        round.AddQuestion(
            "O céu, em um dia claro, é azul?",
            QuestionMediaType.None, null,
            "Verdadeiro. Serve para testar o mínimo de duas alternativas.",
            [
                new AnswerOptionDraft("Verdadeiro", true),
                new AnswerOptionDraft("Falso", false)
            ],
            now);

        // 5. maximo de alternativas
        round.AddQuestion(
            "Qual destes é um dia da semana?",
            QuestionMediaType.None, null,
            "Terça-feira. Serve para testar o máximo de cinco alternativas.",
            [
                new AnswerOptionDraft("Terça-feira", true),
                new AnswerOptionDraft("Janeiro", false),
                new AnswerOptionDraft("Verão", false),
                new AnswerOptionDraft("Páscoa", false),
                new AnswerOptionDraft("Manhã", false)
            ],
            now);

        round.Publish(now);
        return round;
    }

    // ------------------------------------------------------------------ janela da rodada

    private static Task<IResult> OpenNowAsync(
        Guid id, IConfiguration configuration, CurrentUser currentUser,
        DomusDbContext db, DomusQueries queries, TimeProvider clock, CancellationToken ct) =>
        ShiftWindowAsync(id, configuration, currentUser, db, queries, clock, ct, open: true);

    private static Task<IResult> CloseNowAsync(
        Guid id, IConfiguration configuration, CurrentUser currentUser,
        DomusDbContext db, DomusQueries queries, TimeProvider clock, CancellationToken ct) =>
        ShiftWindowAsync(id, configuration, currentUser, db, queries, clock, ct, open: false);

    private static async Task<IResult> ShiftWindowAsync(
        Guid id,
        IConfiguration configuration,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct,
        bool open)
    {
        EnsureEnabled(configuration);

        var round = await queries.GetRoundWithQuestionsAsync(id, tracking: true, ct);
        var now = clock.GetUtcNow();

        Guard.State(round.IsPublished, "Publique a rodada antes de mover a janela.");

        if (open)
        {
            round.OverrideWindowForTesting(now.AddMinutes(-1), now.AddDays(1));
        }
        else
        {
            round.OverrideWindowForTesting(now.AddDays(-1), now.AddSeconds(-1));
        }

        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName,
            open ? "ToolsRoundOpened" : "ToolsRoundClosed",
            $"Semana {round.WeekNumber}: {round.Title}", now));

        await db.SaveChangesAsync(ct);

        return Results.Ok(new ToolActionResultDto(open
            ? "Rodada aberta agora. Ela fecha em 24 horas."
            : "Rodada encerrada agora. Gabarito e ranking liberados."));
    }

    // ------------------------------------------------------------------ tentativas

    /// <summary>
    /// A tentativa é única (RN-14), o que torna testar o quiz duas vezes impossível sem isto.
    /// Apaga apenas a tentativa de quem está pedindo.
    /// </summary>
    private static async Task<IResult> DeleteMyAttemptAsync(
        Guid id,
        IConfiguration configuration,
        CurrentUser currentUser,
        DomusDbContext db,
        CancellationToken ct)
    {
        EnsureEnabled(configuration);

        var meId = currentUser.RequireAdminId();

        var removed = await db.Attempts
            .Where(a => a.RoundId == id && a.ParticipantId == meId)
            .ExecuteDeleteAsync(ct);

        return Results.Ok(new ToolActionResultDto(removed == 0
            ? "Você não tinha tentativa nesta rodada."
            : "Sua tentativa foi apagada. Você pode responder de novo."));
    }

    /// <summary>Popula ranking e estatísticas com participações fictícias, usando as regras reais.</summary>
    private static async Task<IResult> SimulateAsync(
        Guid id,
        SimulateRequest request,
        IConfiguration configuration,
        CurrentUser currentUser,
        DomusDbContext db,
        UserManager<AppUser> userManager,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        EnsureEnabled(configuration);

        var count = Guard.InRange(request.Count, 1, 30, "Quantidade");
        var round = await queries.GetRoundWithQuestionsAsync(id, tracking: false, ct);
        var now = clock.GetUtcNow();

        Guard.State(round.Questions.Count > 0, "A rodada não tem perguntas.");

        // A simulação usa o instante em que a rodada esteve aberta, e não "agora": assim
        // funciona também para uma rodada já encerrada.
        var startedAt = round.OpensAt.AddMinutes(1);
        Guard.State(startedAt < round.ClosesAt, "Janela da rodada muito curta para simular.");

        var created = 0;

        for (var i = 0; i < count; i++)
        {
            var suffix = $"{now.ToUnixTimeSeconds()}-{i}";
            var email = $"teste-{suffix}@domus.local";

            var user = new AppUser(Guid.CreateVersion7(), email) { EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, "Teste12345");
            if (!result.Succeeded) continue;

            var participant = Participant.Register(user.Id, $"Teste {suffix}", null, now);
            db.Participants.Add(participant);
            await db.SaveChangesAsync(ct);

            // Desempenhos variados para o ranking não ficar plano.
            var accuracy = 0.4 + (i % 7) * 0.1;
            var seconds = 3 + (i % 5) * 6;

            db.Attempts.Add(Simulate(round, participant.Id, startedAt.AddMinutes(i), accuracy, seconds));
            await db.SaveChangesAsync(ct);

            created++;
        }

        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, "ToolsSimulated",
            $"{created} participações na semana {round.WeekNumber}", now));

        await db.SaveChangesAsync(ct);

        return Results.Ok(new ToolActionResultDto(
            $"{created} participação(ões) fictícia(s) criada(s) na semana {round.WeekNumber}."));
    }

    private static Attempt Simulate(
        Round round, Guid participantId, DateTimeOffset startedAt, double accuracy, int secondsPerQuestion)
    {
        var attempt = Attempt.Start(round, participantId, startedAt);
        var cursor = startedAt;
        var index = 0;

        while (true)
        {
            var served = attempt.ServeCurrentQuestion(round, cursor);
            if (served is null) break;

            var hit = (index % 10) < (int)Math.Round(accuracy * 10);

            var option = hit
                ? served.Question.CorrectOption
                : served.Question.Options.First(o => !o.IsCorrect);

            cursor = cursor.AddSeconds(secondsPerQuestion);
            attempt.Submit(round, served.Question.Id, option.Id, cursor);
            cursor = cursor.AddSeconds(2);
            index++;
        }

        return attempt;
    }

    // ------------------------------------------------------------------ limpeza

    /// <summary>
    /// Tres escopos, do menos para o mais destrutivo. Administradores e a configuracao do GC
    /// nunca sao apagados: sem eles ninguem entra no sistema para consertar o estrago.
    /// </summary>
    private static async Task<IResult> ResetAsync(
        ResetRequest request,
        IConfiguration configuration,
        CurrentUser currentUser,
        DomusDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        EnsureEnabled(configuration);

        // Validar ANTES de apagar qualquer coisa: um escopo com erro de digitacao nao pode
        // levar as tentativas embora no caminho.
        var scope = (request.Scope ?? string.Empty).Trim().ToLowerInvariant();

        if (scope is not ("attempts" or "content" or "all"))
        {
            throw new DomainValidationException("Escopo invalido. Use attempts, content ou all.");
        }

        if (!string.Equals(request.Confirmation?.Trim(), ResetConfirmationPhrase, StringComparison.Ordinal))
        {
            throw new DomainValidationException($"Digite {ResetConfirmationPhrase} para confirmar a limpeza.");
        }

        var now = clock.GetUtcNow();

        var rounds = 0;
        var seasons = 0;
        var participants = 0;

        // A ordem respeita as chaves estrangeiras: tentativas, depois conteudo, depois pessoas.
        var attempts = await db.Attempts.ExecuteDeleteAsync(ct);

        if (scope is "content" or "all")
        {
            rounds = await db.Rounds.ExecuteDeleteAsync(ct);
            seasons = await db.Seasons.ExecuteDeleteAsync(ct);
        }

        if (scope == "all")
        {
            var meId = currentUser.RequireAdminId();

            var removable = await db.Participants
                .Where(p => p.Role != ParticipantRole.Admin && p.Id != meId)
                .Select(p => p.Id)
                .ToListAsync(ct);

            if (removable.Count > 0)
            {
                participants = await db.Participants
                    .Where(p => removable.Contains(p.Id))
                    .ExecuteDeleteAsync(ct);

                // As credenciais e logins saem em cascata a partir de AspNetUsers.
                await db.Users.Where(u => removable.Contains(u.Id)).ExecuteDeleteAsync(ct);
            }
        }

        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, "ToolsReset",
            $"escopo={scope} tentativas={attempts} rodadas={rounds} temporadas={seasons} pessoas={participants}",
            now));

        await db.SaveChangesAsync(ct);

        return Results.Ok(new ResetResultDto(scope, seasons, rounds, attempts, participants));
    }

    // ------------------------------------------------------------------ apoio

    private static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue("DevTools:Enabled", false);

    private static void EnsureEnabled(IConfiguration configuration)
    {
        if (!IsEnabled(configuration))
        {
            throw new ForbiddenException(
                "Ferramentas de teste estão desligadas. Defina DevTools__Enabled=true para habilitar.");
        }
    }
}
