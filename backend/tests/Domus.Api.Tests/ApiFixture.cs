using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Domus.Api.Tests;

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

    public WebApplicationFactory<Program> ToolsFactory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Factory = Configure(new WebApplicationFactory<Program>(), toolsEnabled: false);

        ToolsFactory = Configure(new WebApplicationFactory<Program>(), toolsEnabled: true);

        using var client = Factory.CreateClient();
        var health = await client.GetAsync("/api/health");
        health.EnsureSuccessStatusCode();
    }

    private WebApplicationFactory<Program> Configure(WebApplicationFactory<Program> factory, bool toolsEnabled) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
            builder.UseSetting("Seed:Demo", "false");
            builder.UseSetting("Gc:InviteCode", InviteCode);
            builder.UseSetting("Admin:Email", AdminEmail);
            builder.UseSetting("Admin:Password", AdminPassword);
            builder.UseSetting("Admin:DisplayName", "Administrador");
            builder.UseSetting("DevTools:Enabled", toolsEnabled ? "true" : "false");

            builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");
            builder.UseSetting("RateLimiting:AnswersPermitLimit", "10000");
        });

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        if (ToolsFactory is not null) await ToolsFactory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    public async Task<HttpClient> LoginAsToolsAdminAsync()
    {
        var client = ToolsFactory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AdminEmail,
            password = AdminPassword
        });

        response.EnsureSuccessStatusCode();
        return client;
    }

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
        var client = await RegisterWithoutRoomAsync(displayName, email);

        var joined = await client.PostAsJsonAsync("/api/rooms/join", new { inviteCode = InviteCode });
        joined.EnsureSuccessStatusCode();

        return client;
    }

    public async Task<HttpClient> RegisterWithoutRoomAsync(string displayName, string email)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
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
