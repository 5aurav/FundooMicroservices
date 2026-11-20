namespace Fundoo.User.Api.Contracts;

public class TwoFactorVerifyRequest
{
    public string Code { get; set; } = string.Empty;
}

public class TwoFactorLoginRequest
{
    public Guid UserId { get; set; }
    public string Code { get; set; } = string.Empty;
}
