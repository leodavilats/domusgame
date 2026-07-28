using System.Security.Claims;
using Domus.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Domus.Infrastructure.Identity;

/// <summary>
/// Transforma Participants.Role em claim de papel no login, evitando as tabelas de role do Identity.
/// O principal e revalidado periodicamente pelo Identity, entao uma promocao vale sem novo login.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    IOptions<IdentityOptions> optionsAccessor,
    DomusDbContext db)
    : UserClaimsPrincipalFactory<AppUser>(userManager, optionsAccessor)
{
    public const string DisplayNameClaim = "display_name";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        var participant = await db.Participants
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == user.Id);

        if (participant is not null)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, participant.Role.ToString()));
            identity.AddClaim(new Claim(DisplayNameClaim, participant.DisplayName));
        }

        return identity;
    }
}
