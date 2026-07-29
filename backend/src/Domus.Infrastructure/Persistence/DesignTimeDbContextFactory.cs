using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Domus.Infrastructure.Persistence;

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
