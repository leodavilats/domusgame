namespace Domus.Api.Common;

public static class AuthorizationPolicies
{
    /// <summary>Exige o papel de administrador (claim gerada no login a partir de Participants.Role).</summary>
    public const string Admin = "Admin";
}
