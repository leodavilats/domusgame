using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Domus.Api.Common;
using Domus.Api.Features.Admin;
using Domus.Api.Features.Attempts;
using Domus.Api.Features.Auth;
using Domus.Api.Features.Dashboard;
using Domus.Api.Features.Profile;
using Domus.Api.Features.Rankings;
using Domus.Api.Features.Rounds;
using Domus.Domain.Participants;
using Domus.Infrastructure;
using Domus.Infrastructure.Identity;
using Domus.Infrastructure.Persistence;
using Domus.Infrastructure.Seed;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// ---------------------------------------------------------------- servicos

var connectionString = DatabaseConnection.Resolve(configuration);

builder.Services.AddDomusPersistence(connectionString);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<DomusQueries>();
builder.Services.AddProblemDetails();

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireDigit = false;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 10;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<DomusDbContext>()
    .AddSignInManager()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "domus.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(60);
    options.SlidingExpiration = true;

    // A API responde status, nunca redireciona para tela de login.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
    options.AddPolicy(AuthorizationPolicies.Admin, policy =>
        policy.RequireRole(nameof(ParticipantRole.Admin))));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Atras de proxy (Railway, Render, Fly, Caddy) a requisição chega como HTTP.
// Sem isto o cookie de sessão sairia sem a flag Secure mesmo com o site em HTTPS.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Em PaaS o proxy não tem IP fixo conhecido; a rede da plataforma e a fronteira de confianca.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Limites configuraveis: os padroes servem a producao, e os testes de integracao (que batem
// no login dezenas de vezes a partir do mesmo IP) sobem o teto por configuracao.
var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 12);
var answersPermitLimit = configuration.GetValue("RateLimiting:AnswersPermitLimit", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy(RateLimitPolicies.Answers, context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = answersPermitLimit, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// ---------------------------------------------------------------- pipeline

// Precisa vir antes de tudo: define esquema e IP reais da requisição.
app.UseForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseDefaultFiles();

// O Vite gera nomes com hash de conteudo (index-AbC123.js), entao o arquivo nunca muda:
// pode ser cacheado para sempre. Sem isto o navegador revalida em toda visita e paga uma
// ida e volta na rede antes de renderizar. O index.html, ao contrario, precisa ser sempre
// revalidado - e ele que aponta para os assets novos depois de um deploy.
var staticFiles = new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var headers = context.Context.Response.GetTypedHeaders();
        var path = context.Context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
        {
            headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(365),
                Extensions = { new Microsoft.Net.Http.Headers.NameValueHeaderValue("immutable") }
            };
        }
        else
        {
            headers.CacheControl =
                new Microsoft.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, MustRevalidate = true };
        }
    }
};

app.UseStaticFiles(staticFiles);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapDashboardEndpoints();
app.MapRoundEndpoints();
app.MapAttemptEndpoints();
app.MapRankingEndpoints();
app.MapProfileEndpoints();

var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthorizationPolicies.Admin);
admin.MapAdminSeasonEndpoints();
admin.MapAdminRoundEndpoints();
admin.MapAdminManagementEndpoints();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// O SPA cuida das rotas que não sao /api.
app.MapFallbackToFile("index.html", staticFiles);

// ---------------------------------------------------------------- banco

await InitializeDatabaseAsync(app);

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var configuration = app.Configuration;
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation(
        "Conectando ao banco em {Target}",
        DatabaseConnection.Describe(DatabaseConnection.Resolve(configuration)));

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<DomusDbContext>();

    // Bancos gerenciados podem estar acordando quando o container sobe. Esperamos um pouco
    // antes de desistir, em vez de entrar em crash loop.
    await WaitForDatabaseAsync(db, logger);

    if (configuration.GetValue("Database:ApplyMigrationsOnStartup", app.Environment.IsDevelopment()))
    {
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            // Ainda não ha migrations geradas: cria o esquema a partir do modelo para o
            // projeto rodar de imediato. Em producao, gere as migrations (ver README).
            logger.LogWarning(
                "Nenhuma migration encontrada. Criando o esquema com EnsureCreated. " +
                "Gere as migrations antes de ir para producao.");

            await db.Database.EnsureCreatedAsync();
        }
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

    await seeder.SeedAsync(new SeedOptions
    {
        GcName = configuration["Gc:Name"] ?? "GC Domus",
        InviteCode = configuration["Gc:InviteCode"],
        AdminEmail = configuration["Admin:Email"],
        AdminPassword = configuration["Admin:Password"],
        AdminDisplayName = configuration["Admin:DisplayName"] ?? "Administrador",
        IncludeDemoData = configuration.GetValue("Seed:Demo", false)
    });
}

static async Task WaitForDatabaseAsync(DomusDbContext db, ILogger logger)
{
    const int maxAttempts = 12;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            if (await db.Database.CanConnectAsync()) return;
        }
        catch (Exception exception) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                "Banco indisponivel (tentativa {Attempt}/{Max}): {Message}",
                attempt, maxAttempts, exception.Message);
        }

        if (attempt == maxAttempts)
        {
            throw new InvalidOperationException(
                "Não foi possivel conectar ao banco de dados. Verifique ConnectionStrings__Postgres " +
                "(ou DATABASE_URL) e se o banco aceita conexoes deste servico.");
        }

        await Task.Delay(TimeSpan.FromSeconds(Math.Min(5, attempt)));
    }
}

/// <summary>Exposto para os testes de integracao (WebApplicationFactory).</summary>
public partial class Program;
