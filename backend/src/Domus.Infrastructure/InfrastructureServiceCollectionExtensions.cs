using Domus.Infrastructure.Persistence;
using Domus.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Domus.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddDomusPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<DomusDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(3)));

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
