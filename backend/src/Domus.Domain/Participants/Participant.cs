using Domus.Domain.Common;

namespace Domus.Domain.Participants;

public enum ParticipantRole
{
    Participant = 0,
    Admin = 1
}

/// <summary>
/// Identidade publica de quem participa (nome no ranking, foto, preferencias e papel).
/// As credenciais ficam em AppUser (Identity, na infraestrutura) compartilhando a mesma chave.
/// </summary>
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

    private Participant(Guid id, string displayName, string? avatarUrl, ParticipantRole role, DateTimeOffset now)
        : base(id)
    {
        DisplayName = ValidateDisplayName(displayName);
        NormalizedDisplayName = Normalize(DisplayName);
        AvatarUrl = Guard.OptionalAbsoluteUrl(avatarUrl, "Foto", 500);
        Role = role;
        ShowInRanking = true;
        JoinedAt = now;
    }

    public string DisplayName { get; private set; }

    /// <summary>Nome em caixa alta, usado para garantir unicidade sem depender de collation.</summary>
    public string NormalizedDisplayName { get; private set; }

    public string? AvatarUrl { get; private set; }

    /// <summary>Se falso, o participante não aparece nas listas publicas de ranking (RN-22).</summary>
    public bool ShowInRanking { get; private set; }

    public ParticipantRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public bool IsRemoved { get; private set; }

    public bool IsAdmin => Role == ParticipantRole.Admin;

    public static Participant Register(
        Guid id,
        string displayName,
        string? avatarUrl,
        DateTimeOffset now,
        ParticipantRole role = ParticipantRole.Participant)
    {
        Guard.Requires(id != Guid.Empty, "Identificador do participante invalido.");
        return new Participant(id, displayName, avatarUrl, role, now);
    }

    public void UpdateProfile(string displayName, string? avatarUrl, bool showInRanking)
    {
        Guard.State(!IsRemoved, "Conta removida não pode ser alterada.");

        DisplayName = ValidateDisplayName(displayName);
        NormalizedDisplayName = Normalize(DisplayName);
        AvatarUrl = Guard.OptionalAbsoluteUrl(avatarUrl, "Foto", 500);
        ShowInRanking = showInRanking;
    }

    public void ChangeRole(ParticipantRole role)
    {
        // I-P3: participante removido não pode ser promovido.
        Guard.State(!IsRemoved, "Conta removida não pode receber papel.");
        Role = role;
    }

    /// <summary>
    /// RN-38: apaga a identidade pessoal mas preserva as tentativas, para não furar o
    /// historico agregado das rodadas.
    /// </summary>
    public void Anonymize()
    {
        if (IsRemoved) return;

        IsRemoved = true;
        Role = ParticipantRole.Participant;
        ShowInRanking = false;
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
            throw new DomainValidationException("Nome de exibição contem caracteres invalidos.");
        }

        return value;
    }
}
