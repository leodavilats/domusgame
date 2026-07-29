namespace Domus.Infrastructure.Seed;

public sealed class SeedOptions
{
    public string GcName { get; set; } = "GC Domus";

    public string? InviteCode { get; set; }

    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string AdminDisplayName { get; set; } = "Administrador";

    public bool IncludeDemoData { get; set; }
}
