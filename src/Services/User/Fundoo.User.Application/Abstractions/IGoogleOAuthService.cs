using Fundoo.User.Domain.Entity;

namespace Fundoo.User.Application.Abstractions;

public interface IGoogleOAuthService
{
    string GetAuthorizationUrl(string state, string? redirectUri = null);
    Task<AuthResult> HandleCallbackAsync(string code, string? redirectUri = null, CancellationToken cancellationToken = default);
    Task<AuthResult> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
    Task<bool> LinkAccountAsync(Guid userId, string code, string? redirectUri = null, CancellationToken cancellationToken = default);
}
