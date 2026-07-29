using Domus.Api.Common;
using Domus.Api.Features.Profile;
using Domus.Domain.Attempts;
using Domus.Domain.Common;
using Domus.Domain.Participants;
using Domus.Domain.Rounds;
using Domus.Domain.Settings;
using Domus.Infrastructure.Identity;
using Domus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Admin;

public sealed record AdminParticipantDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    ParticipantRole Role,
    bool ShowInRanking,
    bool IsRemoved,
    DateTimeOffset JoinedAt,
    int SeasonPoints,
    int RoundsPlayed,
    DateTimeOffset? LastAttemptAt);

public sealed record ChangeRoleRequest(ParticipantRole Role);

public sealed record ResetPasswordResult(string DisplayName, string TemporaryPassword);

public sealed record InviteDto(string GcName, string InviteCode, DateTimeOffset RotatedAt, int MemberCount);

public sealed record RotateInviteRequest(string? Code);

public sealed record QuestionStatDto(
    Guid QuestionId,
    int Order,
    string Text,
    int Answers,
    int Correct,
    double Accuracy,
    double AverageSeconds);

public sealed record RoundStatsDto(
    RoundSummaryDto Round,
    int ParticipantCount,
    int AttemptCount,
    int FinishedCount,
    double ParticipationRate,
    double AveragePoints,
    int MedianPoints,
    double AverageSecondsPerQuestion,
    IReadOnlyList<QuestionStatDto> Questions,
    IReadOnlyList<string> Missing);

public sealed record WeekParticipationDto(
    Guid RoundId,
    int WeekNumber,
    string Title,
    RoundAvailability Availability,
    int Attempts,
    double AveragePoints);

public sealed record OverviewDto(
    Guid? SeasonId,
    string? SeasonName,
    int ParticipantCount,
    int AdminCount,
    IReadOnlyList<WeekParticipationDto> Weeks);

public static class AdminManagementEndpoints
{
    public static void MapAdminManagementEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/participants", ListParticipantsAsync);
        admin.MapPut("/participants/{id:guid}/role", ChangeRoleAsync);
        admin.MapPost("/participants/{id:guid}/reset-password", ResetPasswordAsync);

        admin.MapGet("/invite", GetInviteAsync);
        admin.MapPost("/invite", RotateInviteAsync);

        admin.MapGet("/rounds/{id:guid}/stats", RoundStatsAsync);
        admin.MapGet("/stats/overview", OverviewAsync);
    }

    private static async Task<IResult> ListParticipantsAsync(
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var season = await queries.GetActiveSeasonAsync(ct);

        List<Guid> seasonRoundIds = season is null
            ? []
            : await db.Rounds.AsNoTracking()
                .Where(r => r.SeasonId == season.Id && r.Status == RoundStatus.Published)
                .Select(r => r.Id)
                .ToListAsync(ct);

        var participants = await db.Participants.AsNoTracking()
            .OrderBy(p => p.DisplayName)
            .ToListAsync(ct);

        var stats = await db.Attempts.AsNoTracking()
            .Where(a => seasonRoundIds.Contains(a.RoundId))
            .GroupBy(a => a.ParticipantId)
            .Select(g => new
            {
                ParticipantId = g.Key,
                Points = g.Sum(a => a.TotalPoints),
                Rounds = g.Count(),
                Last = g.Max(a => a.StartedAt)
            })
            .ToListAsync(ct);

        var items = participants.Select(p =>
        {
            var stat = stats.SingleOrDefault(s => s.ParticipantId == p.Id);
            return new AdminParticipantDto(
                p.Id, p.DisplayName, p.AvatarUrl, p.Role, p.ShowInRanking, p.IsRemoved, p.JoinedAt,
                stat?.Points ?? 0, stat?.Rounds ?? 0, stat?.Last);
        });

        return Results.Ok(items);
    }

    private static async Task<IResult> ChangeRoleAsync(
        Guid id,
        ChangeRoleRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        var participant = await db.Participants.SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Participante");

        if (request.Role == ParticipantRole.Participant &&
            participant.IsAdmin &&
            await ProfileEndpoints.IsLastAdminAsync(db, id, ct))
        {
            throw new DomainRuleException("Não e possivel remover o ultimo administrador.");
        }

        participant.ChangeRole(request.Role);

        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, AuditLogEntry.Actions.RoleChanged,
            $"{participant.DisplayName} -> {request.Role}", clock.GetUtcNow()));

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPasswordAsync(
        Guid id,
        CurrentUser currentUser,
        DomusDbContext db,
        UserManager<AppUser> userManager,
        TimeProvider clock,
        CancellationToken ct)
    {
        var participant = await db.Participants.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw NotFoundException.For("Participante");

        Guard.State(!participant.IsRemoved, "Conta removida nao pode ter a senha redefinida.");

        var user = await userManager.FindByIdAsync(id.ToString())
            ?? throw NotFoundException.For("Credenciais do participante");

        var temporary = GenerateTemporaryPassword();

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, temporary);

        if (!result.Succeeded)
        {
            throw new DomainRuleException(
                $"Nao foi possivel redefinir a senha: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);

        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, AuditLogEntry.Actions.PasswordReset,
            participant.DisplayName, clock.GetUtcNow()));

        await db.SaveChangesAsync(ct);

        return Results.Ok(new ResetPasswordResult(participant.DisplayName, temporary));
    }

    private static string GenerateTemporaryPassword()
    {
        const string consonants = "bcdfgjklmnprstvz";
        const string vowels = "aeiou";

        var letters = new char[4];
        for (var i = 0; i < 4; i++)
        {
            letters[i] = i % 2 == 0
                ? consonants[Random.Shared.Next(consonants.Length)]
                : vowels[Random.Shared.Next(vowels.Length)];
        }

        var digits = Random.Shared.Next(1000, 10000);

        return $"{new string(letters)}-{digits}";
    }

    private static async Task<IResult> GetInviteAsync(DomusDbContext db, DomusQueries queries, CancellationToken ct)
    {
        var settings = await queries.GetSettingsAsync(ct);
        var members = await db.Participants.AsNoTracking().CountAsync(p => !p.IsRemoved, ct);

        return Results.Ok(new InviteDto(settings.GcName, settings.InviteCode, settings.InviteRotatedAt, members));
    }

    private static async Task<IResult> RotateInviteAsync(
        RotateInviteRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        TimeProvider clock,
        CancellationToken ct)
    {
        var settings = await db.GcSettings.SingleOrDefaultAsync(s => s.Id == GcSettings.SingletonId, ct)
            ?? throw new NotFoundException("Configuracao do GC não encontrada.");

        var now = clock.GetUtcNow();
        var code = string.IsNullOrWhiteSpace(request.Code) ? GcSettings.GenerateCode() : request.Code;

        settings.RotateInvite(code, now);

        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, AuditLogEntry.Actions.InviteRotated, null, now));

        await db.SaveChangesAsync(ct);

        var members = await db.Participants.AsNoTracking().CountAsync(p => !p.IsRemoved, ct);
        return Results.Ok(new InviteDto(settings.GcName, settings.InviteCode, settings.InviteRotatedAt, members));
    }

    private static async Task<IResult> RoundStatsAsync(
        Guid id,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var round = await queries.GetRoundWithQuestionsAsync(id, tracking: false, ct);

        var attempts = await db.Attempts.AsNoTracking()
            .Where(a => a.RoundId == round.Id)
            .Select(a => new { a.ParticipantId, a.TotalPoints, a.TotalTimeMs, a.Status, a.QuestionCount })
            .ToListAsync(ct);

        var answers = await db.AttemptAnswers.AsNoTracking()
            .Where(a => db.Attempts.Any(at => at.Id == a.AttemptId && at.RoundId == round.Id))
            .Select(a => new { a.QuestionId, a.Outcome, a.ElapsedMs })
            .ToListAsync(ct);

        var activeParticipants = await db.Participants.AsNoTracking()
            .Where(p => !p.IsRemoved)
            .Select(p => new { p.Id, p.DisplayName })
            .ToListAsync(ct);

        var played = attempts.Select(a => a.ParticipantId).ToHashSet();

        var points = attempts.Select(a => a.TotalPoints).OrderBy(p => p).ToList();

        var questions = round.OrderedQuestions.Select(question =>
        {
            var forQuestion = answers.Where(a => a.QuestionId == question.Id).ToList();
            var resolved = forQuestion.Where(a => a.Outcome != AnswerOutcome.Pending).ToList();
            var correct = resolved.Count(a => a.Outcome == AnswerOutcome.Correct);

            return new QuestionStatDto(
                question.Id,
                question.Order,
                question.Text,
                resolved.Count,
                correct,
                resolved.Count == 0 ? 0 : Math.Round((double)correct / resolved.Count, 3),
                resolved.Count == 0 ? 0 : Math.Round(resolved.Average(a => a.ElapsedMs) / 1000.0, 1));
        }).ToList();

        var totalResolved = answers.Count(a => a.Outcome != AnswerOutcome.Pending);

        return Results.Ok(new RoundStatsDto(
            queries.ToSummary(round),
            activeParticipants.Count,
            attempts.Count,
            attempts.Count(a => a.Status == AttemptStatus.Completed),
            activeParticipants.Count == 0 ? 0 : Math.Round((double)attempts.Count / activeParticipants.Count, 3),
            attempts.Count == 0 ? 0 : Math.Round(attempts.Average(a => a.TotalPoints), 1),
            Median(points),
            totalResolved == 0 ? 0 : Math.Round(answers.Where(a => a.Outcome != AnswerOutcome.Pending).Average(a => a.ElapsedMs) / 1000.0, 1),
            questions,
            [.. activeParticipants.Where(p => !played.Contains(p.Id)).Select(p => p.DisplayName).Order()]));
    }

    private static async Task<IResult> OverviewAsync(
        Guid? seasonId,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var season = seasonId is null
            ? await queries.GetActiveSeasonAsync(ct)
            : await db.Seasons.AsNoTracking().SingleOrDefaultAsync(s => s.Id == seasonId, ct);

        var participantCount = await db.Participants.AsNoTracking().CountAsync(p => !p.IsRemoved, ct);
        var adminCount = await db.Participants.AsNoTracking()
            .CountAsync(p => !p.IsRemoved && p.Role == ParticipantRole.Admin, ct);

        if (season is null)
        {
            return Results.Ok(new OverviewDto(null, null, participantCount, adminCount, []));
        }

        var rounds = await db.Rounds.AsNoTracking()
            .Include(r => r.Questions)
            .Where(r => r.SeasonId == season.Id && r.Status == RoundStatus.Published)
            .OrderBy(r => r.WeekNumber)
            .ToListAsync(ct);

        var roundIds = rounds.Select(r => r.Id).ToList();

        var aggregates = await db.Attempts.AsNoTracking()
            .Where(a => roundIds.Contains(a.RoundId))
            .GroupBy(a => a.RoundId)
            .Select(g => new { RoundId = g.Key, Attempts = g.Count(), Average = g.Average(a => (double)a.TotalPoints) })
            .ToListAsync(ct);

        var weeks = rounds.Select(round =>
        {
            var aggregate = aggregates.SingleOrDefault(a => a.RoundId == round.Id);
            return new WeekParticipationDto(
                round.Id,
                round.WeekNumber,
                round.Title,
                round.AvailabilityAt(queries.Now),
                aggregate?.Attempts ?? 0,
                Math.Round(aggregate?.Average ?? 0, 1));
        }).ToList();

        return Results.Ok(new OverviewDto(season.Id, season.Name, participantCount, adminCount, weeks));
    }

    private static int Median(List<int> ordered)
    {
        if (ordered.Count == 0) return 0;

        var middle = ordered.Count / 2;

        return ordered.Count % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2;
    }
}
