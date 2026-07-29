using Microsoft.AspNetCore.Identity;

namespace Domus.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public AppUser() { }

    public AppUser(Guid id, string email)
    {
        Id = id;
        Email = email;
        UserName = email;
    }
}
