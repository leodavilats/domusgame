namespace Domus.Infrastructure.Seed;

/// <summary>Configuracao do seed idempetente executado no start (doc 04, secao 4).</summary>
public sealed class SeedOptions
{
    public string GcName { get; set; } = "GC Domus";

    /// <summary>Se vazio, um codigo aleatorio e gerado na primeira execucao.</summary>
    public string? InviteCode { get; set; }

    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string AdminDisplayName { get; set; } = "Administrador";

    /// <summary>Cria temporada, rodadas e participantes ficticios para desenvolvimento.</summary>
    public bool IncludeDemoData { get; set; }
}
