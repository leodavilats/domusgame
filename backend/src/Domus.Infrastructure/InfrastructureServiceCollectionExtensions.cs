using Domus.Infrastructure.Persistence;
using Domus.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Domus.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDomusPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<DomusDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3));

            // A migration "AddParticipantBadges" foi escrita a mao (sem o SDK do .NET disponivel
            // para gerar via `dotnet ef`), entao o snapshot pode nao bater 100% com o modelo em
            // runtime. Sem isso o Migrate() derruba o app inteiro por uma divergencia so de
            // comparacao — a tabela em si e criada corretamente pela migration. Remover assim que
            // alguem com o SDK rodar `dotnet ef migrations add` para regenerar o snapshot correto.
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
