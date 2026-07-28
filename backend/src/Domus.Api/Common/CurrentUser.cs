using System.Security.Claims;
using Domus.Domain.Participants;

namespace Domus.Api.Common;

/// <summary>Lancada quando a rota exige sessão e não ha usuário autenticado.</summary>
public sealed class UnauthorizedException(string message = "Sessão expirada. Entre novamente.") : Exception(message);

/// <summary>Lancada quando o usuário esta autenticado mas não tem permissão.</summary>
public sealed class ForbiddenException(string message = "Você não tem permissão para esta ação.") : Exception(message);

/// <summary>Acesso tipado ao usuário da requisição.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsAdmin => Principal?.IsInRole(nameof(ParticipantRole.Admin)) == true;

    public Guid? Id
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid RequireId() => Id ?? throw new UnauthorizedException();

    public Guid RequireAdminId()
    {
        var id = RequireId();
        if (!IsAdmin) throw new ForbiddenException();
        return id;
    }

    public string DisplayName =>
        Principal?.FindFirstValue("display_name") ?? "Desconhecido";
}
