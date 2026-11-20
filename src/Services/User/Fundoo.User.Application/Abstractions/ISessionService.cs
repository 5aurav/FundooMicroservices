using Fundoo.User.Domain.Entity;

namespace Fundoo.User.Application.Abstractions;

public interface ISessionService
{
    Task<(UserSession Session, string RawToken)> CreateSessionAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<UserSession>> GetActiveSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<int> RevokeAllOtherSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default);
}
