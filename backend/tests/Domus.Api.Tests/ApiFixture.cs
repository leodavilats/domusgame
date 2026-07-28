using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Domus.Api.Tests;

/// <summary>
/// Sobe um Postgres real em container e a API completa. Exige Docker em execucao;
/// no CI o servico ja esta disponivel.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string InviteCode = "TESTE123";
    public const string AdminEmail = "admin@teste.local";
    public const string AdminPassword = "Teste@12345";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("domus_test")
        .WithUsername("domus")
        .WithPassword("domus")
        .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
            builder.UseSetting("Seed:Demo", "false");
            builder.UseSetting("Gc:InviteCode", InviteCode);
            builder.UseSetting("Admin:Email", AdminEmail);
            builder.UseSetting("Admin:Password", AdminPassword);
            builder.UseSetting("Admin:DisplayName", "Administrador");

            // A suite faz dezenas de logins e cadastros a partir do mesmo IP; o rate limit
            // de producao (12/min) derrubaria os testes sem indicar nenhum defeito real.
            builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");
            builder.UseSetting("RateLimiting:AnswersPermitLimit", "10000");
        });

        // Forca o start da aplicacao (cria o esquema e roda o seed).
        using var client = Factory.CreateClient();
        var health = await client.GetAsync("/api/health");
        health.EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync()
    {
        // A inicializacao pode ter falhado antes de criar a factory; não mascare o erro original.
        if (Factory is not null) await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    public async Task<HttpClient> LoginAsAdminAsync()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AdminEmail,
            password = AdminPassword
        });

        response.EnsureSuccessStatusCode();
        return client;
    }

    public async Task<HttpClient> RegisterParticipantAsync(string displayName, string email)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            inviteCode = InviteCode,
            displayName,
            email,
            password = "Teste@12345"
        });

        response.EnsureSuccessStatusCode();
        return client;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}

internal static class HttpExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement.Clone();
    }

    public static async Task<string> ReadRawAsync(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
