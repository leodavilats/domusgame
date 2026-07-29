using System.Security.Claims;
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
using Domus.Api.Features.Rooms;
using Domus.Api.Features.Rounds;
using Domus.Domain.Participants;
using Domus.Infrastructure;
using Domus.Infrastructure.Identity;
using Domus.Infrastructure.Persistence;
using Domus.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

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
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 10;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<DomusDbContext>()
    .AddSignInManager()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

var authentication = builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme);
authentication.AddIdentityCookies();

var googleClientId = configuration["Authentication:Google:ClientId"];
var googleClientSecret = configuration["Authentication:Google:ClientSecret"];

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddGoogle(AuthEndpoints.GoogleScheme, options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClaimActions.Add(new JsonKeyClaimAction("picture", ClaimValueTypes.String, "picture"));
    });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "domus.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(60);
    options.SlidingExpiration = true;

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

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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

app.UseForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseDefaultFiles();

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
app.MapRoomEndpoints();

var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthorizationPolicies.Admin);
admin.MapAdminSeasonEndpoints();
admin.MapAdminRoundEndpoints();
admin.MapAdminManagementEndpoints();
admin.MapAdminToolsEndpoints();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapFallbackToFile("index.html", staticFiles);

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

    await WaitForDatabaseAsync(db, logger);

    if (configuration.GetValue("Database:ApplyMigrationsOnStartup", app.Environment.IsDevelopment()))
    {
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
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

public partial class Program;
