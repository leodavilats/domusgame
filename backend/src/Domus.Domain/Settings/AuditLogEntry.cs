using Domus.Domain.Common;

namespace Domus.Domain.Settings;

/// <summary>Acoes administrativas registradas para auditoria (RNF-08). Append-only.</summary>
public sealed class AuditLogEntry : Entity
{
    public static class Actions
    {
        public const string RoundPublished = "RoundPublished";
        public const string RoundDeleted = "RoundDeleted";
        public const string SeasonActivated = "SeasonActivated";
        public const string SeasonFinished = "SeasonFinished";
        public const string InviteRotated = "InviteRotated";
        public const string RoleChanged = "RoleChanged";
        public const string PasswordReset = "PasswordReset";
        public const string AccountDeleted = "AccountDeleted";
    }

    private AuditLogEntry() : base()
    {
        ActorName = string.Empty;
        Action = string.Empty;
    }

    private AuditLogEntry(Guid? actorId, string actorName, string action, string? details, DateTimeOffset now)
        : base(NewId())
    {
        ActorId = actorId;
        ActorName = Guard.Text(actorName, "Autor", 60);
        Action = Guard.Text(action, "Acao", 60);
        Details = Guard.OptionalText(details, "Detalhes", 1000);
        OccurredAt = now;
    }

    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? ActorId { get; private set; }
    public string ActorName { get; private set; }
    public string Action { get; private set; }
    public string? Details { get; private set; }

    public static AuditLogEntry Record(Guid? actorId, string actorName, string action, string? details, DateTimeOffset now) =>
        new(actorId, actorName, action, details, now);
}
