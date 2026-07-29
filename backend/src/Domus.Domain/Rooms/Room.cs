using Domus.Domain.Common;

namespace Domus.Domain.Rooms;

public sealed class Room : Entity
{
    public const int InviteCodeMinLength = 6;
    public const int InviteCodeMaxLength = 20;
    public const int NameMaxLength = 80;

    private Room() : base()
    {
        Name = string.Empty;
        InviteCode = string.Empty;
        NormalizedInviteCode = string.Empty;
    }

    private Room(string name, string inviteCode, DateTimeOffset now)
        : base(NewId())
    {
        Name = Guard.Text(name, "Nome da sala", NameMaxLength);
        InviteCode = ValidateCode(inviteCode);
        NormalizedInviteCode = Normalize(InviteCode);
        CreatedAt = now;
        InviteRotatedAt = now;
    }

    public string Name { get; private set; }
    public string InviteCode { get; private set; }
    public string NormalizedInviteCode { get; private set; }
    public DateTimeOffset InviteRotatedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Room Create(string name, string inviteCode, DateTimeOffset now) =>
        new(name, inviteCode, now);

    public void Rename(string name) => Name = Guard.Text(name, "Nome da sala", NameMaxLength);

    public void RotateInvite(string inviteCode, DateTimeOffset now)
    {
        InviteCode = ValidateCode(inviteCode);
        NormalizedInviteCode = Normalize(InviteCode);
        InviteRotatedAt = now;
    }

    public bool MatchesInvite(string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate) && Normalize(candidate) == NormalizedInviteCode;

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static string GenerateCode(int length = 8)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[Math.Clamp(length, InviteCodeMinLength, InviteCodeMaxLength)];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }

        return new string(chars);
    }

    private static string ValidateCode(string inviteCode)
    {
        var value = Guard.Text(inviteCode, "Codigo de convite", InviteCodeMaxLength, InviteCodeMinLength);

        Guard.Requires(
            value.All(char.IsLetterOrDigit),
            "O codigo de convite deve conter apenas letras e numeros.");

        return value.ToUpperInvariant();
    }
}
