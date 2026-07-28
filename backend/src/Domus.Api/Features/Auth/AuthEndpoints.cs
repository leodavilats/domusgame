using System.Security.Claims;
using Domus.Api.Common;
using Domus.Domain.Common;
using Domus.Domain.Participants;
using Domus.Infrastructure.Identity;
using Domus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Auth;

public sealed record RegisterRequest(string InviteCode, string DisplayName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public static class AuthEndpoints
{
    public const string GoogleScheme = "Google";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapGet("/me", MeAsync).RequireAuthorization();
        group.MapGet("/google/start", GoogleStartAsync);
        group.MapGet("/google/callback", GoogleCallbackAsync);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        DomusDbContext db,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var settings = await queries.GetSettingsAsync(ct);

        if (!settings.MatchesInvite(request.InviteCode))
        {
            throw new DomainValidationException("Codigo de convite invalido. Peca o codigo ao lider do GC.");
        }

        var id = Guid.CreateVersion7();

        // Valida o nome antes de criar credenciais, para nao deixar usuario orfao.
        var participant = Participant.Register(id, request.DisplayName, null, clock.GetUtcNow());

        await EnsureDisplayNameIsFreeAsync(db, participant.NormalizedDisplayName, null, ct);

        var email = Guard.Text(request.Email, "E-mail", 256, 5);
        var user = new AppUser(id, email) { EmailConfirmed = true };

        var result = await userManager.CreateAsync(user, request.Password ?? string.Empty);
        if (!result.Succeeded)
        {
            throw new DomainValidationException(Translate(result));
        }

        db.Participants.Add(participant);
        await db.SaveChangesAsync(ct);

        await signInManager.SignInAsync(user, isPersistent: true);

        return Results.Ok(new MeDto(
            participant.Id, participant.DisplayName, participant.AvatarUrl,
            participant.ShowInRanking, participant.IsAdmin, settings.GcName));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        DomusDbContext db,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        DomusQueries queries,
        CancellationToken ct)
    {
        const string invalid = "E-mail ou senha invalidos.";

        var user = await userManager.FindByEmailAsync((request.Email ?? string.Empty).Trim());
        if (user is null) throw new DomainValidationException(invalid);

        var participant = await db.Participants.AsNoTracking().SingleOrDefaultAsync(p => p.Id == user.Id, ct);
        if (participant is null || participant.IsRemoved) throw new DomainValidationException(invalid);

        var result = await signInManager.PasswordSignInAsync(
            user, request.Password ?? string.Empty, isPersistent: true, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            throw new DomainValidationException(result.IsLockedOut
                ? "Conta bloqueada temporariamente por excesso de tentativas. Tente de novo em alguns minutos."
                : invalid);
        }

        var settings = await queries.GetSettingsAsync(ct);

        return Results.Ok(new MeDto(
            participant.Id, participant.DisplayName, participant.AvatarUrl,
            participant.ShowInRanking, participant.IsAdmin, settings.GcName));
    }

    private static async Task<IResult> LogoutAsync(SignInManager<AppUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        CancellationToken ct)
    {
        var id = currentUser.RequireId();

        var participant = await db.Participants.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new UnauthorizedException();

        var settings = await queries.GetSettingsAsync(ct);

        return Results.Ok(new MeDto(
            participant.Id, participant.DisplayName, participant.AvatarUrl,
            participant.ShowInRanking, participant.IsAdmin, settings.GcName));
    }

    // ------------------------------------------------------------------ Google (opcional)

    private static async Task<IResult> GoogleStartAsync(
        SignInManager<AppUser> signInManager,
        IAuthenticationSchemeProvider schemes,
        string? inviteCode,
        string? displayName)
    {
        if (await schemes.GetSchemeAsync(GoogleScheme) is null)
        {
            return Results.Redirect("/entrar?erro=google-indisponivel");
        }

        var properties = signInManager.ConfigureExternalAuthenticationProperties(
            GoogleScheme, "/api/auth/google/callback");

        properties.Items["inviteCode"] = inviteCode ?? string.Empty;
        properties.Items["displayName"] = displayName ?? string.Empty;

        return Results.Challenge(properties, [GoogleScheme]);
    }

    private static async Task<IResult> GoogleCallbackAsync(
        DomusDbContext db,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        DomusQueries queries,
        TimeProvider clock,
        CancellationToken ct)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null) return Results.Redirect("/entrar?erro=google");

        var signedIn = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        if (signedIn.Succeeded) return Results.Redirect("/");

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email)) return Results.Redirect("/entrar?erro=google-sem-email");

        // Conta ja existente com o mesmo e-mail: apenas vincula o login do Google.
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            await userManager.AddLoginAsync(existing, info);
            await signInManager.SignInAsync(existing, isPersistent: true);
            return Results.Redirect("/");
        }

        var items = info.AuthenticationProperties?.Items ?? new Dictionary<string, string?>();
        items.TryGetValue("inviteCode", out var inviteCode);
        items.TryGetValue("displayName", out var requestedName);

        var settings = await queries.GetSettingsAsync(ct);
        if (!settings.MatchesInvite(inviteCode)) return Results.Redirect("/cadastro?erro=convite");

        var displayName = FirstNonEmpty(
            requestedName,
            info.Principal.FindFirstValue(ClaimTypes.Name),
            email.Split('@')[0]);

        var id = Guid.CreateVersion7();

        Participant participant;
        try
        {
            participant = Participant.Register(
                id, displayName, info.Principal.FindFirstValue("picture"), clock.GetUtcNow());
        }
        catch (DomainValidationException)
        {
            return Results.Redirect("/cadastro?erro=nome");
        }

        var taken = await db.Participants
            .AnyAsync(p => p.NormalizedDisplayName == participant.NormalizedDisplayName, ct);

        if (taken) return Results.Redirect("/cadastro?erro=nome-em-uso");

        var user = new AppUser(id, email) { EmailConfirmed = true };
        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded) return Results.Redirect("/cadastro?erro=conta");

        await userManager.AddLoginAsync(user, info);

        db.Participants.Add(participant);
        await db.SaveChangesAsync(ct);

        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.Redirect("/");
    }

    // ------------------------------------------------------------------ apoio

    internal static async Task EnsureDisplayNameIsFreeAsync(
        DomusDbContext db,
        string normalizedDisplayName,
        Guid? exceptParticipantId,
        CancellationToken ct)
    {
        var taken = await db.Participants.AnyAsync(
            p => p.NormalizedDisplayName == normalizedDisplayName &&
                 (exceptParticipantId == null || p.Id != exceptParticipantId),
            ct);

        if (taken)
        {
            throw new DomainValidationException("Ja existe alguem com esse nome de exibicao. Escolha outro.");
        }
    }

    private static string FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim() ?? "Participante";

    private static string Translate(IdentityResult result)
    {
        var messages = result.Errors.Select(error => error.Code switch
        {
            "DuplicateUserName" or "DuplicateEmail" => "Este e-mail ja esta cadastrado.",
            "InvalidEmail" => "Informe um e-mail valido.",
            "PasswordTooShort" => "A senha deve ter ao menos 8 caracteres.",
            "PasswordRequiresDigit" => "A senha deve conter ao menos um numero.",
            _ => error.Description
        });

        return string.Join(" ", messages.Distinct());
    }
}
