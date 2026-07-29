using Domus.Domain.Attempts;
using Domus.Domain.Participants;
using Domus.Domain.Rounds;
using Domus.Domain.Rooms;
using Domus.Domain.Seasons;
using Domus.Domain.Settings;
using Domus.Infrastructure.Identity;
using Domus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domus.Infrastructure.Seed;

public sealed class DatabaseSeeder(
    DomusDbContext db,
    UserManager<AppUser> userManager,
    TimeProvider clock,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(SeedOptions options, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();

        var room = await SeedRoomAsync(options, now, cancellationToken);

        await TryAsync("administrador inicial", () => SeedAdminAsync(options, room, now, cancellationToken));

        if (options.IncludeDemoData)
        {
            await TryAsync("dados de demonstracao", () => SeedDemoAsync(room, now, cancellationToken));
        }
    }

    private async Task TryAsync(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            db.ChangeTracker.Clear();
            logger.LogError(exception, "Falha ao preparar {Step}. A aplicacao continua no ar.", step);
        }
    }

    private async Task<Room> SeedRoomAsync(SeedOptions options, DateTimeOffset now, CancellationToken ct)
    {
        var room = await db.Rooms.OrderBy(r => r.CreatedAt).FirstOrDefaultAsync(ct);

        if (room is null)
        {
            var code = string.IsNullOrWhiteSpace(options.InviteCode)
                ? Room.GenerateCode()
                : options.InviteCode;

            room = Room.Create(options.GcName, code, now);
            db.Rooms.Add(room);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Sala '{Room}' criada. Codigo de convite: {InviteCode}", room.Name, room.InviteCode);
            return room;
        }

        if (room.Name != options.GcName && !string.IsNullOrWhiteSpace(options.GcName))
        {
            room.Rename(options.GcName);
            await db.SaveChangesAsync(ct);
        }

        return room;
    }

    private async Task SeedAdminAsync(SeedOptions options, Room room, DateTimeOffset now, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            if (!await db.Participants.AnyAsync(p => p.Role == ParticipantRole.Admin, ct))
            {
                logger.LogWarning(
                    "Nenhum administrador cadastrado e Admin__Email/Admin__Password não foram configurados.");
            }

            return;
        }

        var existing = await userManager.FindByEmailAsync(options.AdminEmail);
        if (existing is not null)
        {
            await EnsureAdminRoleAsync(existing.Id, options, room, now, ct);
            await SyncAdminPasswordAsync(existing, options.AdminPassword);
            return;
        }

        var normalized = Participant.Normalize(options.AdminDisplayName);
        if (await db.Participants.AnyAsync(p => p.NormalizedDisplayName == normalized, ct))
        {
            logger.LogWarning(
                "Já existe participante com o nome de exibição '{DisplayName}'. " +
                "Ajuste Admin__DisplayName ou promova a conta existente pelo painel.",
                options.AdminDisplayName);
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
        await EnsureMembershipAsync(room, user.Id, now, ct);

        logger.LogInformation("Administrador inicial criado: {Email}", options.AdminEmail);
    }

    private async Task SyncAdminPasswordAsync(AppUser user, string password)
    {
        if (await userManager.CheckPasswordAsync(user, password))
        {
            await ClearLockoutAsync(user);
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, password);

        if (result.Succeeded)
        {
            await ClearLockoutAsync(user);
            logger.LogWarning(
                "Senha do administrador {Email} sincronizada com Admin__Password.", user.Email);
            return;
        }

        logger.LogError(
            "Nao foi possivel sincronizar a senha do administrador: {Errors}",
            string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private async Task ClearLockoutAsync(AppUser user)
    {
        if (user.AccessFailedCount == 0 && user.LockoutEnd is null) return;

        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
    }

    private async Task EnsureAdminRoleAsync(Guid userId, SeedOptions options, Room room, DateTimeOffset now, CancellationToken ct)
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
            await EnsureMembershipAsync(room, userId, now, ct);
            return;
        }

        await db.SaveChangesAsync(ct);
        await EnsureMembershipAsync(room, userId, now, ct);
    }

    private async Task EnsureMembershipAsync(Room room, Guid participantId, DateTimeOffset now, CancellationToken ct)
    {
        var joined = await db.RoomMemberships
            .AnyAsync(m => m.RoomId == room.Id && m.ParticipantId == participantId, ct);

        if (joined) return;

        db.RoomMemberships.Add(RoomMembership.Join(room, participantId, now));
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedDemoAsync(Room room, DateTimeOffset now, CancellationToken ct)
    {
        if (await db.Seasons.AnyAsync(ct))
        {
            return;
        }

        logger.LogInformation("Criando dados de demonstracao.");

        var season = Season.Create(
            room.Id,
            "Temporada de demonstracao",
            DateOnly.FromDateTime(now.UtcDateTime.AddDays(-30)),
            DateOnly.FromDateTime(now.UtcDateTime.AddDays(60)),
            now);
        season.Activate();
        db.Seasons.Add(season);

        var closed = BuildRound(season.Id, 1, "A graça que transforma", "Efésios 2.1-10",
            now.AddDays(-14), now.AddDays(-8), now.AddDays(-15));
        var open = BuildRound(season.Id, 2, "Oracao que persevera", "Lucas 18.1-8",
            now.AddDays(-2), now.AddDays(4), now.AddDays(-3));
        var scheduled = BuildRound(season.Id, 3, "Comunhao no corpo", "Atos 2.42-47",
            now.AddDays(5), now.AddDays(11), now);

        db.Rounds.AddRange(closed, open, scheduled);
        await db.SaveChangesAsync(ct);

        var participants = await CreateDemoParticipantsAsync(room, now, ct);

        var profiles = new[] { (0.9, 4), (1.0, 8), (0.6, 12), (0.75, 20), (0.4, 30), (1.0, 25) };

        for (var i = 0; i < participants.Count; i++)
        {
            var (accuracy, secondsPerQuestion) = profiles[i % profiles.Length];
            db.Attempts.Add(SimulateAttempt(closed, participants[i].Id, closed.OpensAt.AddHours(6 + i), accuracy, secondsPerQuestion));
        }

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
            null), createdAt);

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
                ],
                createdAt);
        }

        round.Publish(createdAt);
        return round;
    }

    private async Task<List<Participant>> CreateDemoParticipantsAsync(Room room, DateTimeOffset now, CancellationToken ct)
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
            await db.SaveChangesAsync(ct);

            await EnsureMembershipAsync(room, participant.Id, now, ct);
            created.Add(participant);
        }

        await db.SaveChangesAsync(ct);
        return created;
    }

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
