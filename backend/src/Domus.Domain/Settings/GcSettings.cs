using Domus.Domain.Common;

namespace Domus.Domain.Settings;

/// <summary>
/// Configuracao do GC: nome exibido e codigo de convite. Tabela de linha unica (Id = 1).
/// Decisao consciente de nao criar a entidade GC na v1 (doc 01, secao 8).
/// </summary>
public sealed class GcSettings
{
    public const int SingletonId = 1;
    public const int InviteCodeMinLength = 6;
    public const int InviteCodeMaxLength = 20;

    private GcSettings()
    {
        GcName = string.Empty;
        InviteCode = string.Empty;
        NormalizedInviteCode = string.Empty;
    }

    private GcSettings(string gcName, string inviteCode, DateTimeOffset now)
    {
        Id = SingletonId;
        GcName = Guard.Text(gcName, "Nome do GC", 80);
        InviteCode = ValidateCode(inviteCode);
        NormalizedInviteCode = Normalize(InviteCode);
        InviteRotatedAt = now;
    }

    public int Id { get; private set; }
    public string GcName { get; private set; }
    public string InviteCode { get; private set; }
    public string NormalizedInviteCode { get; private set; }
    public DateTimeOffset InviteRotatedAt { get; private set; }

    public static GcSettings Create(string gcName, string inviteCode, DateTimeOffset now) =>
        new(gcName, inviteCode, now);

    public void Rename(string gcName) => GcName = Guard.Text(gcName, "Nome do GC", 80);

    public void RotateInvite(string inviteCode, DateTimeOffset now)
    {
        InviteCode = ValidateCode(inviteCode);
        NormalizedInviteCode = Normalize(InviteCode);
        InviteRotatedAt = now;
    }

    /// <summary>Comparacao insensivel a caixa e a espacos (RN-34).</summary>
    public bool MatchesInvite(string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate) && Normalize(candidate) == NormalizedInviteCode;

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    /// <summary>Gera um codigo legivel, sem caracteres ambiguos (0/O, 1/I).</summary>
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
