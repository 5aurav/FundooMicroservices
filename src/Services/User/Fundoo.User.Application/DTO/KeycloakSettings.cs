namespace Fundoo.User.Application.DTO;

public class KeycloakSettings
{
    public const string SectionName = "Keycloak";

    public string Authority { get; set; } = "http://localhost:8080/realms/fundoo";
    public string ClientId { get; set; } = "fundoo-web";
    public string? ClientSecret { get; set; }
    public string RedirectUri { get; set; } = "http://localhost:7000/api/v1/auth/external/callback";
}
