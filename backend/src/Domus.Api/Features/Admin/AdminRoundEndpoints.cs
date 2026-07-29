using Domus.Api.Common;
using Domus.Domain.Common;
using Domus.Domain.Rounds;
using Domus.Domain.Settings;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Admin;

public sealed record CreateRoundRequest(
    Guid SeasonId,
    int WeekNumber,
    string Title,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    int PointsPerCorrectAnswer,
    int MaxSpeedBonus,
    int QuestionTimeLimitSeconds);

public sealed record UpdateRoundRequest(
    int WeekNumber,
    string Title,
    DateTimeOffset OpensAt,
    DateTimeOffset ClosesAt,
    int PointsPerCorrectAnswer,
    int MaxSpeedBonus,
    int QuestionTimeLimitSeconds);

public sealed record LessonRequest(string Title, string ScriptureReference, string Content, string? ExternalUrl);

public sealed record OptionRequest(string Text, bool IsCorrect);

public sealed record QuestionRequest(
    string Text,
    QuestionMediaType MediaType,
    string? MediaUrl,
    string? Explanation,
    IReadOnlyList<OptionRequest> Options);

public sealed record MoveQuestionRequest(int Offset);

public sealed record DuplicateRoundRequest(int WeekNumber, DateTimeOffset OpensAt, DateTimeOffset ClosesAt);

public sealed record AdminOptionDto(Guid Id, int Order, string Text, bool IsCorrect);

public sealed record AdminQuestionDto(
    Guid Id,
    int Order,
    string Text,
    QuestionMediaType MediaType,
    string? MediaUrl,
    string? Explanation,
    IReadOnlyList<AdminOptionDto> Options);

public sealed record AdminRoundListItemDto(
    RoundSummaryDto Round,
    RoundStatus Status,
    int AttemptCount,
    bool CanEdit,
    bool CanDelete);

public sealed record AdminRoundDto(
    RoundSummaryDto Round,
    RoundStatus Status,
    LessonDto Lesson,
    IReadOnlyList<AdminQuestionDto> Questions,
    IReadOnlyList<string> Problems,
    int AttemptCount,
    bool CanEdit,
    bool CanDelete);

public static class AdminRoundEndpoints
{
    public static void MapAdminRoundEndpoints(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/rounds");

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPut("/{id:guid}/lesson", SetLessonAsync);
        group.MapGet("/{id:guid}/validate", ValidateAsync);
        group.MapPost("/{id:guid}/publish", PublishAsync);
        group.MapPost("/{id:guid}/duplicate", DuplicateAsync);

        group.MapPost("/{id:guid}/questions", AddQuestionAsync);
        group.MapPut("/{id:guid}/questions/{questionId:guid}", UpdateQuestionAsync);
        group.MapDelete("/{id:guid}/questions/{questionId:guid}", RemoveQuestionAsync);
        group.MapPost("/{id:guid}/questions/{questionId:guid}/move", MoveQuestionAsync);
    }

    private static async Task<IResult> ListAsync(
        Guid? seasonId,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var room = await queries.RequireMyRoomAsync(currentUser.RequireAdminId(), ct);
        var targetSeasonId = seasonId ?? (await queries.GetActiveSeasonAsync(room.Id, ct))?.Id;
        if (targetSeasonId is null) return Results.Ok(Array.Empty<AdminRoundListItemDto>());

        var rounds = await db.Rounds.AsNoTracking()
            .Include(r => r.Questions)
            .Where(r => r.SeasonId == targetSeasonId)
            .OrderByDescending(r => r.WeekNumber)
            .ToListAsync(ct);

        var roundIds = rounds.Select(r => r.Id).ToList();

        var attemptCounts = await db.Attempts.AsNoTracking()
            .Where(a => roundIds.Contains(a.RoundId))
            .GroupBy(a => a.RoundId)
            .Select(g => new { RoundId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var items = rounds.Select(round => new AdminRoundListItemDto(
            queries.ToSummary(round),
            round.Status,
            attemptCounts.SingleOrDefault(c => c.RoundId == round.Id)?.Count ?? 0,
            round.IsEditableAt(queries.Now),
            round.IsEditableAt(queries.Now) &&
                (attemptCounts.SingleOrDefault(c => c.RoundId == round.Id)?.Count ?? 0) == 0));

        return Results.Ok(items);
    }

    private static async Task<IResult> CreateAsync(
        CreateRoundRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var room = await queries.RequireMyRoomAsync(currentUser.RequireAdminId(), ct);

        var season = await db.Seasons.SingleOrDefaultAsync(s => s.Id == request.SeasonId && s.RoomId == room.Id, ct)
            ?? throw NotFoundException.For("Temporada");

        Guard.State(!season.IsFinished, "Temporada encerrada não recebe novas rodadas.");

        var round = Round.CreateDraft(
            season.Id,
            request.WeekNumber,
            request.Title,
            request.OpensAt.ToUniversalTime(),
            request.ClosesAt.ToUniversalTime(),
            RoundScoringSettings.Create(
                request.PointsPerCorrectAnswer,
                request.MaxSpeedBonus,
                request.QuestionTimeLimitSeconds),
            clock.GetUtcNow());

        await EnsureWeekIsFreeAsync(db, round, ct);

        db.Rounds.Add(round);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/rounds/{round.Id}", await ToDetailAsync(db, queries, round, ct));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: false, ct);
        return Results.Ok(await ToDetailAsync(db, queries, round, ct));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateRoundRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: true, ct);

        round.UpdateDetails(request.WeekNumber, request.Title, queries.Now);
        round.UpdateWindow(request.OpensAt.ToUniversalTime(), request.ClosesAt.ToUniversalTime(), queries.Now);
        round.UpdateScoring(RoundScoringSettings.Create(
            request.PointsPerCorrectAnswer, request.MaxSpeedBonus, request.QuestionTimeLimitSeconds), queries.Now);

        await EnsureWeekIsFreeAsync(db, round, ct);

        if (round.IsPublished) await EnsureWindowDoesNotOverlapAsync(db, round, ct);

        await db.SaveChangesAsync(ct);

        return Results.Ok(await ToDetailAsync(db, queries, round, ct));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: true, ct);

        Guard.State(
            round.IsEditableAt(queries.Now),
            "Rodada que já abriu não pode ser excluída.");

        var attempts = await db.Attempts.AsNoTracking().CountAsync(a => a.RoundId == round.Id, ct);
        Guard.State(attempts == 0, "Esta rodada já tem participações e não pode ser excluída.");

        db.Rounds.Remove(round);
        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, AuditLogEntry.Actions.RoundDeleted,
            $"Semana {round.WeekNumber}: {round.Title}", clock.GetUtcNow()));

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetLessonAsync(
        Guid id,
        LessonRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: true, ct);

        round.SetLesson(Lesson.Create(
            request.Title, request.ScriptureReference, request.Content, request.ExternalUrl), queries.Now);

        await db.SaveChangesAsync(ct);
        return Results.Ok(await ToDetailAsync(db, queries, round, ct));
    }

    private static async Task<IResult> ValidateAsync(
        Guid id,
        CurrentUser currentUser,
        DomusQueries queries,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: false, ct);
        return Results.Ok(round.ValidateForPublish());
    }

    private static async Task<IResult> PublishAsync(
        Guid id,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: true, ct);
        var now = clock.GetUtcNow();

        await EnsureWeekIsFreeAsync(db, round, ct);
        await EnsureWindowDoesNotOverlapAsync(db, round, ct);

        round.Publish(now);

        db.AuditLogs.Add(AuditLogEntry.Record(
            currentUser.Id, currentUser.DisplayName, AuditLogEntry.Actions.RoundPublished,
            $"Semana {round.WeekNumber}: {round.Title}", now));

        await db.SaveChangesAsync(ct);

        return Results.Ok(await ToDetailAsync(db, queries, round, ct));
    }

    private static async Task<IResult> DuplicateAsync(
        Guid id,
        DuplicateRoundRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var source = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: false, ct);

        var copy = source.DuplicateAsDraft(
            request.WeekNumber,
            request.OpensAt.ToUniversalTime(),
            request.ClosesAt.ToUniversalTime(),
            clock.GetUtcNow());

        await EnsureWeekIsFreeAsync(db, copy, ct);

        db.Rounds.Add(copy);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/rounds/{copy.Id}", await ToDetailAsync(db, queries, copy, ct));
    }

    private static async Task<IResult> AddQuestionAsync(
        Guid id,
        QuestionRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: true, ct);

        round.AddQuestion(
            request.Text, request.MediaType, request.MediaUrl, request.Explanation, ToDrafts(request.Options), queries.Now);

        await db.SaveChangesAsync(ct);
        return Results.Ok(await ToDetailAsync(db, queries, round, ct));
    }

    private static async Task<IResult> UpdateQuestionAsync(
        Guid id,
        Guid questionId,
        QuestionRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: true, ct);

        round.UpdateQuestion(
            questionId, request.Text, request.MediaType, request.MediaUrl, request.Explanation,
            ToDrafts(request.Options), queries.Now);

        await db.SaveChangesAsync(ct);
        return Results.Ok(await ToDetailAsync(db, queries, round, ct));
    }

    private static async Task<IResult> RemoveQuestionAsync(
        Guid id,
        Guid questionId,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: true, ct);

        round.RemoveQuestion(questionId, queries.Now);

        await db.SaveChangesAsync(ct);
        return Results.Ok(await ToDetailAsync(db, queries, round, ct));
    }

    private static async Task<IResult> MoveQuestionAsync(
        Guid id,
        Guid questionId,
        MoveQuestionRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var round = await queries.RequireRoundInMyRoomAsync(id, currentUser.RequireAdminId(), tracking: true, ct);

        round.MoveQuestion(questionId, request.Offset, queries.Now);

        await db.SaveChangesAsync(ct);
        return Results.Ok(await ToDetailAsync(db, queries, round, ct));
    }

    private static IReadOnlyList<AnswerOptionDraft> ToDrafts(IReadOnlyList<OptionRequest>? options) =>
        options is null
            ? []
            : [.. options.Select(o => new AnswerOptionDraft(o.Text, o.IsCorrect))];

    private static async Task<AdminRoundDto> ToDetailAsync(
        DomusDbContext db,
        DomusQueries queries,
        Round round,
        CancellationToken ct)
    {
        var attemptCount = await db.Attempts.AsNoTracking().CountAsync(a => a.RoundId == round.Id, ct);

        var questions = round.OrderedQuestions.Select(q => new AdminQuestionDto(
            q.Id,
            q.Order,
            q.Text,
            q.MediaType,
            q.MediaUrl,
            q.Explanation,
            [.. q.Options
                .OrderBy(o => o.Order)
                .Select(o => new AdminOptionDto(o.Id, o.Order, o.Text, o.IsCorrect))])).ToList();

        return new AdminRoundDto(
            queries.ToSummary(round),
            round.Status,
            new LessonDto(
                round.Lesson.Title, round.Lesson.ScriptureReference, round.Lesson.Content, round.Lesson.ExternalUrl),
            questions,
            round.ValidateForPublish(),
            attemptCount,
            round.IsEditableAt(queries.Now),
            round.IsEditableAt(queries.Now) && attemptCount == 0);
    }

    private static async Task EnsureWeekIsFreeAsync(DomusDbContext db, Round round, CancellationToken ct)
    {
        var duplicated = await db.Rounds.AsNoTracking().AnyAsync(
            r => r.SeasonId == round.SeasonId && r.WeekNumber == round.WeekNumber && r.Id != round.Id, ct);

        if (duplicated)
        {
            throw new DomainRuleException($"Já existe uma rodada para a semana {round.WeekNumber} nesta temporada.");
        }
    }

    private static async Task EnsureWindowDoesNotOverlapAsync(DomusDbContext db, Round round, CancellationToken ct)
    {
        var overlapping = await db.Rounds.AsNoTracking().AnyAsync(
            r => r.SeasonId == round.SeasonId &&
                 r.Id != round.Id &&
                 r.Status == RoundStatus.Published &&
                 r.OpensAt < round.ClosesAt &&
                 round.OpensAt < r.ClosesAt,
            ct);

        if (overlapping)
        {
            throw new DomainRuleException(
                "A janela desta rodada se sobrepoe a de outra rodada publicada. Ajuste as datas antes de publicar.");
        }
    }
}
