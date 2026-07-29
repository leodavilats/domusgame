using Domus.Api.Common;
using Domus.Api.Features.Auth;
using Domus.Domain.Common;
using Domus.Domain.Participants;
using Domus.Domain.Settings;
using Domus.Infrastructure.Identity;
using Domus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Domus.Api.Features.Profile;

public sealed record UpdateProfileRequest(string DisplayName);

public sealed record DeleteAccountRequest(string Confirmation);

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile").RequireAuthorization();

        group.MapPut("/", UpdateAsync);

        group.MapPost("/delete", DeleteAsync);
    }

    private static async Task<IResult> UpdateAsync(
        UpdateProfileRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        DomusQueries queries,
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();

        var participant = await db.Participants.SingleOrDefaultAsync(p => p.Id == meId, ct)
            ?? throw NotFoundException.For("Participante");

        var displayName = request.DisplayName ?? string.Empty;

        await AuthEndpoints.EnsureDisplayNameIsFreeAsync(db, Participant.Normalize(displayName), meId, ct);

        participant.UpdateProfile(displayName);
        await db.SaveChangesAsync(ct);

        var user = await userManager.FindByIdAsync(meId.ToString());
        if (user is not null) await signInManager.RefreshSignInAsync(user);

        return Results.Ok(await MeMapper.BuildAsync(participant, queries, ct));
    }

    private static async Task<IResult> DeleteAsync(
        DeleteAccountRequest request,
        CurrentUser currentUser,
        DomusDbContext db,
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        TimeProvider clock,
        CancellationToken ct)
    {
        var meId = currentUser.RequireId();

        var participant = await db.Participants.SingleOrDefaultAsync(p => p.Id == meId, ct)
            ?? throw NotFoundException.For("Participante");

        if (!string.Equals(request.Confirmation?.Trim(), participant.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("Digite seu nome de exibição para confirmar a exclusao.");
        }

        if (participant.IsAdmin && await IsLastAdminAsync(db, meId, ct))
        {
            throw new DomainRuleException("Você e o único administrador. Promova outra pessoa antes de excluir a conta.");
        }

        var name = participant.DisplayName;
        participant.Anonymize();

        db.AuditLogs.Add(AuditLogEntry.Record(
            meId, name, AuditLogEntry.Actions.AccountDeleted, "Conta excluida pelo próprio participante", clock.GetUtcNow()));

        await db.SaveChangesAsync(ct);

        var user = await userManager.FindByIdAsync(meId.ToString());
        if (user is not null)
        {
            user.Email = null;
            user.NormalizedEmail = null;
            user.UserName = $"removido-{meId:N}";
            user.NormalizedUserName = user.UserName.ToUpperInvariant();
            user.PasswordHash = null;
            user.SecurityStamp = Guid.NewGuid().ToString();
            await userManager.UpdateAsync(user);

            foreach (var login in await userManager.GetLoginsAsync(user))
            {
                await userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
            }
        }

        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    internal static async Task<bool> IsLastAdminAsync(DomusDbContext db, Guid participantId, CancellationToken ct)
    {
        var anotherAdminExists = await db.Participants
            .AnyAsync(p => p.Role == ParticipantRole.Admin && !p.IsRemoved && p.Id != participantId, ct);

        return !anotherAdminExists;
    }
}
