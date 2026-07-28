using Domus.Domain.Attempts;
using Domus.Domain.Participants;
using Domus.Domain.Rounds;
using Domus.Domain.Seasons;
using Domus.Domain.Settings;
using Domus.Infrastructure.Identity;
using Domus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domus.Infrastructure.Seed;

/// <summary>
/// Seed idempotente: pode rodar em todo start sem duplicar nada.
/// Cria a configuracao do GC, o administrador inicial e, opcionalmente, dados de demonstracao.
/// </summary>
public sealed class DatabaseSeeder(
    DomusDbContext db,
    UserManager<AppUser> userManager,
    TimeProvider clock,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(SeedOptions options, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();

        await SeedSettingsAsync(options, now, cancellationToken);
        await SeedAdminAsync(options, now, cancellationToken);

        if (options.IncludeDemoData)
        {
            await SeedDemoAsync(now, cancellationToken);
        }
    }

    private async Task SeedSettingsAsync(SeedOptions options, DateTimeOffset now, CancellationToken ct)
    {
        var settings = await db.GcSettings.SingleOrDefaultAsync(s => s.Id == GcSettings.SingletonId, ct);

        if (settings is null)
        {
            var code = string.IsNullOrWhiteSpace(options.InviteCode)
                ? GcSettings.GenerateCode()
                : options.InviteCode;

            db.GcSettings.Add(GcSettings.Create(options.GcName, code, now));
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Configuracao do GC criada. Codigo de convite: {InviteCode}", code);
            return;
        }

        // O nome pode ser ajustado por configuracao; o codigo so muda por acao explicita do admin.
        if (settings.GcName != options.GcName && !string.IsNullOrWhiteSpace(options.GcName))
        {
            settings.Rename(options.GcName);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task SeedAdminAsync(SeedOptions options, DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            if (!await db.Participants.AnyAsync(p => p.Role == ParticipantRole.Admin, ct))
            {
                logger.LogWarning(
                    "Nenhum administrador cadastrado e Admin__Email/Admin__Password nao foram configurados.");
            }

            return;
        }

        var existing = await userManager.FindByEmailAsync(options.AdminEmail);
        if (existing is not null)
        {
            await EnsureAdminRoleAsync(existing.Id, options, now, ct);
            return;
        }

        var user = new AppUser(Guid.CreateVersion7(), options.AdminEmail) { EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, options.AdminPassword);

        if (!result.Succeeded)
        {
            logger.LogError(
                "Falha ao criar o administrador inicial: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        db.Participants.Add(Participant.Register(
            user.Id,
            options.AdminDisplayName,
            avatarUrl: null,
            now,
            ParticipantRole.Admin));

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Administrador inicial criado: {Email}", options.AdminEmail);
    }

    private async Task EnsureAdminRoleAsync(Guid userId, SeedOptions options, DateTimeOffset now, CancellationToken ct)
    {
        var participant = await db.Participants.SingleOrDefaultAsync(p => p.Id == userId, ct);

        if (participant is null)
        {
            db.Participants.Add(Participant.Register(
                userId, options.AdminDisplayName, null, now, ParticipantRole.Admin));
        }
        else if (!participant.IsAdmin)
        {
            participant.ChangeRole(ParticipantRole.Admin);
        }
        else
        {
            return;
        }

        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ demonstracao

    private async Task SeedDemoAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (await db.Seasons.AnyAsync(ct))
        {
            return;
        }

        logger.LogInformation("Criando dados de demonstracao.");

        var season = Season.Create(
            "Temporada de demonstracao",
            DateOnly.FromDateTime(now.UtcDateTime.AddDays(-30)),
            DateOnly.FromDateTime(now.UtcDateTime.AddDays(60)),
            now);
        season.Activate();
        db.Seasons.Add(season);

        var closed = BuildRound(season.Id, 1, "A graca que transforma", "Efesios 2.1-10",
            now.AddDays(-14), now.AddDays(-8), now.AddDays(-15));
        var open = BuildRound(season.Id, 2, "Oracao que persevera", "Lucas 18.1-8",
            now.AddDays(-2), now.AddDays(4), now.AddDays(-3));
        var scheduled = BuildRound(season.Id, 3, "Comunhao no corpo", "Atos 2.42-47",
            now.AddDays(5), now.AddDays(11), now);

        db.Rounds.AddRange(closed, open, scheduled);
        await db.SaveChangesAsync(ct);

        var participants = await CreateDemoParticipantsAsync(now, ct);

        // Tentativas na rodada encerrada, com desempenhos diferentes para o ranking ficar interessante.
        var profiles = new[] { (0.9, 4), (1.0, 8), (0.6, 12), (0.75, 20), (0.4, 30), (1.0, 25) };

        for (var i = 0; i < participants.Count; i++)
        {
            var (accuracy, secondsPerQuestion) = profiles[i % profiles.Length];
            db.Attempts.Add(SimulateAttempt(closed, participants[i].Id, closed.OpensAt.AddHours(6 + i), accuracy, secondsPerQuestion));
        }

        // Metade do grupo ja respondeu a rodada aberta.
        for (var i = 0; i < participants.Count / 2; i++)
        {
            var (accuracy, secondsPerQuestion) = profiles[i % profiles.Length];
            db.Attempts.Add(SimulateAttempt(open, participants[i].Id, open.OpensAt.AddHours(3 + i), accuracy, secondsPerQuestion));
        }

        await db.SaveChangesAsync(ct);
    }

    private static Round BuildRound(
        Guid seasonId,
        int week,
        string title,
        string reference,
        DateTimeOffset opensAt,
        DateTimeOffset closesAt,
        DateTimeOffset createdAt)
    {
        var round = Round.CreateDraft(seasonId, week, title, opensAt, closesAt, RoundScoringSettings.Default, createdAt);

        round.SetLesson(Lesson.Create(
            title,
            reference,
            $"## {title}\n\nEstudo da semana {week} baseado em **{reference}**.\n\n" +
            "Leia o texto antes de responder ao desafio. As perguntas seguem a ordem do estudo.",
            null));

        for (var i = 1; i <= 8; i++)
        {
            round.AddQuestion(
                $"[Semana {week}] Pergunta {i}: o que o texto ensina neste ponto?",
                QuestionMediaType.None,
                null,
                $"A resposta correta esta no versiculo {i} do trecho estudado.",
                [
                    new AnswerOptionDraft($"Alternativa correta da pergunta {i}", true),
                    new AnswerOptionDraft($"Alternativa incorreta A da pergunta {i}", false),
                    new AnswerOptionDraft($"Alternativa incorreta B da pergunta {i}", false),
                    new AnswerOptionDraft($"Alternativa incorreta C da pergunta {i}", false)
                ]);
        }

        round.Publish(createdAt);
        return round;
    }

    private async Task<List<Participant>> CreateDemoParticipantsAsync(DateTimeOffset now, CancellationToken ct)
    {
        string[] names = ["Ana Clara", "Bruno Reis", "Carla Menezes", "Diego Alves", "Elis Prado", "Felipe Nunes"];
        var created = new List<Participant>();

        for (var i = 0; i < names.Length; i++)
        {
            var email = $"demo{i + 1}@domus.local";
            var existing = await userManager.FindByEmailAsync(email);

            if (existing is not null)
            {
                var known = await db.Participants.SingleOrDefaultAsync(p => p.Id == existing.Id, ct);
                if (known is not null)
                {
                    created.Add(known);
                    continue;
                }
            }

            var user = existing ?? new AppUser(Guid.CreateVersion7(), email) { EmailConfirmed = true };

            if (existing is null)
            {
                var result = await userManager.CreateAsync(user, "Demo@123");
                if (!result.Succeeded) continue;
            }

            var participant = Participant.Register(user.Id, names[i], null, now.AddDays(-20 + i));
            db.Participants.Add(participant);
            created.Add(participant);
        }

        await db.SaveChangesAsync(ct);
        return created;
    }

    /// <summary>
    /// Reproduz uma participacao usando as regras reais do dominio (nada de pontuacao fabricada),
    /// para que os dados de demonstracao sejam consistentes com o que o app calcula.
    /// </summary>
    private static Attempt SimulateAttempt(
        Round round,
        Guid participantId,
        DateTimeOffset startedAt,
        double accuracy,
        int secondsPerQuestion)
    {
        var attempt = Attempt.Start(round, participantId, startedAt);
        var cursor = startedAt;
        var index = 0;

        while (true)
        {
            var served = attempt.ServeCurrentQuestion(round, cursor);
            if (served is null) break;

            // Determinístico: usa o indice para decidir acerto, sem depender de aleatoriedade.
            var shouldHit = (index % 10) < (int)Math.Round(accuracy * 10);
            var option = shouldHit
                ? served.Question.CorrectOption
                : served.Question.Options.First(o => !o.IsCorrect);

            cursor = cursor.AddSeconds(secondsPerQuestion);
            attempt.Submit(round, served.Question.Id, option.Id, cursor);
            cursor = cursor.AddSeconds(2);
            index++;
        }

        return attempt;
    }
}
