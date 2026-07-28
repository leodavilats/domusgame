using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Domus.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo `dotnet ef` (migrations). Evita que as ferramentas precisem construir o host
/// da API, o que tornaria a geracao de migrations dependente de configuracao de execucao.
/// A connection string aqui não precisa apontar para um banco real: migrations sao geradas a
/// partir do modelo.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DomusDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=domus;Username=domus;Password=domus";

    public DomusDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<DomusDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DomusDbContext(options);
    }
}
