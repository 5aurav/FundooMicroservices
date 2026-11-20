namespace Fundoo.User.Api.Contracts;

public class OAuthCallbackRequest
{
    public string Code { get; set; } = string.Empty;
    public string? RedirectUri { get; set; }
}
