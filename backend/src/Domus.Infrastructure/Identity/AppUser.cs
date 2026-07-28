using Microsoft.AspNetCore.Identity;

namespace Domus.Infrastructure.Identity;

/// <summary>
/// Credenciais e login externo. Compartilha a chave primaria com Participant:
/// o dominio guarda a identidade publica, o Identity guarda apenas o acesso.
/// </summary>
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
