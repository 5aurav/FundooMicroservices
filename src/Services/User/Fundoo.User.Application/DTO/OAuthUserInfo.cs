namespace Fundoo.User.Application.DTO;

public class OAuthUserInfo
{
    public string Subject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string GivenName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string PreferredUsername { get; set; } = string.Empty;
    public string Provider { get; set; } = "Keycloak";
}
