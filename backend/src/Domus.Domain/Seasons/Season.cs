using Domus.Domain.Common;

namespace Domus.Domain.Seasons;

public enum SeasonStatus
{
    Draft = 0,
    Active = 1,
    Finished = 2
}

public sealed class SeasonPodiumEntry : Entity
{
    private SeasonPodiumEntry() : base() => DisplayName = string.Empty;

    internal SeasonPodiumEntry(Guid seasonId, int position, PodiumCandidate candidate)
        : base(NewId())
    {
        SeasonId = seasonId;
        Position = Guard.InRange(position, 1, 3, "Posição do pódio");
        ParticipantId = candidate.ParticipantId;
        DisplayName = Guard.Text(candidate.DisplayName, "Nome", Participants.Participant.DisplayNameMaxLength);
        TotalPoints = candidate.TotalPoints;
        TotalTimeMs = candidate.TotalTimeMs;
    }

    public Guid SeasonId { get; private set; }
    public int Position { get; private set; }
    public Guid ParticipantId { get; private set; }
    public string DisplayName { get; private set; }
    public int TotalPoints { get; private set; }
    public long TotalTimeMs { get; private set; }
}

public readonly record struct PodiumCandidate(Guid ParticipantId, string DisplayName, int TotalPoints, long TotalTimeMs);

public sealed class Season : Entity
{
    public const int MaxPodiumPositions = 3;

    private readonly List<SeasonPodiumEntry> _podium = [];

    private Season() : base() => Name = string.Empty;

    private Season(Guid roomId, string name, DateOnly startsOn, DateOnly endsOn, DateTimeOffset now)
        : base(NewId())
    {
        Guard.Requires(roomId != Guid.Empty, "Sala invalida.");

        RoomId = roomId;
        Name = Guard.Text(name, "Nome da temporada", 80);
        ValidatePeriod(startsOn, endsOn);
        StartsOn = startsOn;
        EndsOn = endsOn;
        Status = SeasonStatus.Draft;
        CreatedAt = now;
    }

    public Guid RoomId { get; private set; }
    public string Name { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public SeasonStatus Status { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<SeasonPodiumEntry> Podium => _podium;

    public bool IsFinished => Status == SeasonStatus.Finished;

    public static Season Create(Guid roomId, string name, DateOnly startsOn, DateOnly endsOn, DateTimeOffset now) =>
        new(roomId, name, startsOn, endsOn, now);

    public void Update(string name, DateOnly startsOn, DateOnly endsOn)
    {
        Guard.State(!IsFinished, "Temporada encerrada não pode ser alterada.");

        ValidatePeriod(startsOn, endsOn);
        Name = Guard.Text(name, "Nome da temporada", 80);
        StartsOn = startsOn;
        EndsOn = endsOn;
    }

    public void Activate()
    {
        Guard.State(!IsFinished, "Temporada encerrada não pode ser reativada.");
        Status = SeasonStatus.Active;
    }

    public void Deactivate()
    {
        Guard.State(!IsFinished, "Temporada encerrada não pode ser desativada.");
        Status = SeasonStatus.Draft;
    }

    public void Finish(DateTimeOffset now, IEnumerable<PodiumCandidate> orderedCandidates)
    {
        Guard.State(!IsFinished, "Temporada ja esta encerrada.");

        _podium.Clear();
        var position = 1;
        foreach (var candidate in orderedCandidates.Take(MaxPodiumPositions))
        {
            _podium.Add(new SeasonPodiumEntry(Id, position, candidate));
            position++;
        }

        Status = SeasonStatus.Finished;
        FinishedAt = now;
    }

    private static void ValidatePeriod(DateOnly startsOn, DateOnly endsOn) =>
        Guard.Requires(startsOn < endsOn, "A data de inicio deve ser anterior a data de fim.");
}
