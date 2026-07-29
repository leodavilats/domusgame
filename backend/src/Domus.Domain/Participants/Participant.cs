using Domus.Domain.Common;

namespace Domus.Domain.Participants;

public enum ParticipantRole
{
    Participant = 0,
    Admin = 1
}

public sealed class Participant : Entity
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 40;
    public const string RemovedDisplayName = "Participante removido";

    private Participant() : base()
    {
        DisplayName = string.Empty;
        NormalizedDisplayName = string.Empty;
    }

    private Participant(Guid id, string displayName, ParticipantRole role, DateTimeOffset now)
        : base(id)
    {
        DisplayName = ValidateDisplayName(displayName);
        NormalizedDisplayName = Normalize(DisplayName);
        Role = role;
        JoinedAt = now;
    }

    public string DisplayName { get; private set; }

    public string NormalizedDisplayName { get; private set; }

    public string? AvatarUrl { get; private set; }

    public ParticipantRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public bool IsRemoved { get; private set; }

    public bool IsAdmin => Role == ParticipantRole.Admin;

    public static Participant Register(
        Guid id,
        string displayName,
        DateTimeOffset now,
        ParticipantRole role = ParticipantRole.Participant)
    {
        Guard.Requires(id != Guid.Empty, "Identificador do participante inválido.");
        return new Participant(id, displayName, role, now);
    }

    public void UpdateProfile(string displayName)
    {
        Guard.State(!IsRemoved, "Conta removida não pode ser alterada.");

        DisplayName = ValidateDisplayName(displayName);
        NormalizedDisplayName = Normalize(DisplayName);
    }

    public void SetPhoto(string? avatarUrl)
    {
        Guard.State(!IsRemoved, "Conta removida não pode ser alterada.");
        AvatarUrl = Guard.OptionalAbsoluteUrl(avatarUrl, "Foto", 500);
    }

    public void ChangeRole(ParticipantRole role)
    {
        Guard.State(!IsRemoved, "Conta removida não pode receber papel.");
        Role = role;
    }

    public void Anonymize()
    {
        if (IsRemoved) return;

        IsRemoved = true;
        Role = ParticipantRole.Participant;
        AvatarUrl = null;
        DisplayName = RemovedDisplayName;
        NormalizedDisplayName = Normalize($"{RemovedDisplayName} {Id:N}");
    }

    public static string Normalize(string displayName) => displayName.Trim().ToUpperInvariant();

    private static string ValidateDisplayName(string displayName)
    {
        var value = Guard.Text(displayName, "Nome de exibição", DisplayNameMaxLength, DisplayNameMinLength);

        if (value.Any(char.IsControl))
        {
            throw new DomainValidationException("Nome de exibição contem caracteres inválidos.");
        }

        return value;
    }
}
