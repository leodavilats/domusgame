using Domus.Api.Common;
using Domus.Domain.Attempts;
using Domus.Domain.Common;
using Domus.Domain.Rounds;
using Domus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Attempts;

public static class AttemptEndpoints
{
    public static void MapAttemptEndpoints(this IEndpointRouteBuilder app)
    {
        var rounds = app.MapGroup("/api/rounds").RequireAuthorization();
        rounds.MapPost("/{roundId:guid}/attempts", StartAsync);
        rounds.MapGet("/{roundId:guid}/attempts/current", CurrentAsync);

        var attempts = app.MapGroup("/api/attempts").RequireAuthorization();
        attempts.MapPost("/{attemptId:guid}/answers", SubmitAsync).RequireRateLimiting(RateLimitPolicies.Answers);
        attempts.MapGet("/{attemptId:guid}/result", ResultAsync);
    }

    /// <summary>
    /// UC-05. Idempotente: se ja existe tentativa, devolve o estado dela em vez de erro.
    /// A tentativa única e garantida pelo indice do banco (RNF-04), não por este if.
    /// </summary>
    private static async Task<IResult> StartAsync(
        Guid roundId,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();
        var now = queries.Now;
        var round = await queries.GetRoundWithQuestionsAsync(roundId, tracking: false, ct);

        var existing = await LoadAttemptAsync(db, roundId, meId, ct);
        if (existing is not null)
        {
            return Results.Ok(await AdvanceAsync(db, existing, round, now, ct));
        }

        var attempt = Attempt.Start(round, meId, now);
        db.Attempts.Add(attempt);
        attempt.ServeCurrentQuestion(round, now);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Corrida entre dois cliques: a segunda gravacao perde e reaproveitamos a primeira.
            db.ChangeTracker.Clear();

            var winner = await LoadAttemptAsync(db, roundId, meId, ct)
                ?? throw new DomainRuleException("Não foi possivel iniciar a tentativa. Tente novamente.");

            return Results.Ok(await AdvanceAsync(db, winner, round, now, ct));
        }

        return Results.Ok(ToState(attempt, round, now));
    }

    /// <summary>UC-07: retomada.</summary>
    private static async Task<IResult> CurrentAsync(
        Guid roundId,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();
        var now = queries.Now;

        var attempt = await LoadAttemptAsync(db, roundId, meId, ct)
            ?? throw new NotFoundException("Você ainda não iniciou esta rodada.");

        var round = await queries.GetRoundWithQuestionsAsync(roundId, tracking: false, ct);

        return Results.Ok(await AdvanceAsync(db, attempt, round, now, ct));
    }

    /// <summary>UC-06: o único lugar onde pontos sao atribuidos.</summary>
    private static async Task<IResult> SubmitAsync(
        Guid attemptId,
        SubmitAnswerRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();
        var now = queries.Now;

        var attempt = await db.Attempts
            .Include(a => a.Answers)
            .SingleOrDefaultAsync(a => a.Id == attemptId, ct)
            ?? throw NotFoundException.For("Tentativa");

        if (attempt.ParticipantId != meId) throw new ForbiddenException();

        var round = await queries.GetRoundWithQuestionsAsync(attempt.RoundId, tracking: false, ct);

        var result = attempt.Submit(round, request.QuestionId, request.SelectedOptionId, now);

        var next = attempt.IsFinished ? null : attempt.ServeCurrentQuestion(round, now);

        await db.SaveChangesAsync(ct);

        return Results.Ok(new SubmitAnswerResponse(
            result.AnswerId,
            result.TimedOut,
            attempt.IsFinished,
            next is null ? null : ToQuestion(attempt, next, now)));
    }

    /// <summary>UC-08.</summary>
    private static async Task<IResult> ResultAsync(
        Guid attemptId,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();
        var now = queries.Now;

        var attempt = await db.Attempts
            .Include(a => a.Answers)
            .SingleOrDefaultAsync(a => a.Id == attemptId, ct)
            ?? throw NotFoundException.For("Tentativa");

        if (attempt.ParticipantId != meId) throw new ForbiddenException();

        var round = await queries.GetRoundWithQuestionsAsync(attempt.RoundId, tracking: false, ct);

        // Se a rodada fechou enquanto a pessoa estava fora, consolidamos agora (RN-20).
        attempt.CompleteIfRoundClosed(round, now);
        await db.SaveChangesAsync(ct);

        int? position = null;
        var revealed = round.IsAnswerRevealedAt(now);

        if (revealed)
        {
            var ranking = await queries.GetRoundRankingAsync(round, meId, ct);
            position = ranking.Me?.Position;
        }

        return Results.Ok(new AttemptResultDto(
            attempt.Id,
            queries.ToSummary(round),
            attempt.Status,
            attempt.TotalPoints,
            attempt.MaxPoints,
            attempt.CorrectCount,
            attempt.QuestionCount,
            attempt.TotalTimeMs,
            revealed,
            position));
    }

    // ------------------------------------------------------------------ apoio

    private static Task<Attempt?> LoadAttemptAsync(DomusDbContext db, Guid roundId, Guid meId, CancellationToken ct) =>
        db.Attempts
            .Include(a => a.Answers)
            .SingleOrDefaultAsync(a => a.RoundId == roundId && a.ParticipantId == meId, ct);

    /// <summary>Expira pendencias, entrega a pergunta corrente e persiste o que mudou.</summary>
    private static async Task<AttemptStateDto> AdvanceAsync(
        DomusDbContext db,
        Attempt attempt,
        Round round,
        DateTimeOffset now,
        CancellationToken ct)
    {
        attempt.ServeCurrentQuestion(round, now);
        await db.SaveChangesAsync(ct);
        return ToState(attempt, round, now);
    }

    private static AttemptStateDto ToState(Attempt attempt, Round round, DateTimeOffset now)
    {
        var pending = attempt.Answers.FirstOrDefault(a => a.IsPending);

        AttemptQuestionDto? current = null;
        if (pending is not null)
        {
            var question = round.RequireQuestion(pending.QuestionId);
            current = ToQuestion(
                attempt,
                new ServedQuestion(question, pending.ServedAt, pending.DeadlineAt(attempt.Scoring), question.Order, attempt.QuestionCount),
                now);
        }

        return new AttemptStateDto(
            attempt.Id,
            attempt.RoundId,
            attempt.Status,
            attempt.QuestionCount,
            attempt.AnsweredCount,
            current);
    }

    /// <summary>
    /// RNF-02: este e o único mapeamento de pergunta usado durante a tentativa e ele não
    /// tem como expor a alternativa correta - o DTO simplesmente não tem esse campo.
    /// </summary>
    private static AttemptQuestionDto ToQuestion(Attempt attempt, ServedQuestion served, DateTimeOffset now)
    {
        var options = OptionShuffler.ShuffleFor(attempt.Id, served.Question)
            .Select(o => new AttemptOptionDto(o.Id, o.Text))
            .ToList();

        return new AttemptQuestionDto(
            served.Question.Id,
            served.Order,
            served.TotalQuestions,
            served.Question.Text,
            served.Question.MediaType,
            served.Question.MediaUrl,
            options,
            attempt.Scoring.QuestionTimeLimitSeconds,
            served.ServedAt,
            served.DeadlineAt,
            now);
    }
}
