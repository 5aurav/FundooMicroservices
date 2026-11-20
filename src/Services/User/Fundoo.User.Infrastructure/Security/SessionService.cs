using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Fundoo.User.Infrastructure.Security;

public class SessionService : ISessionService
{
    private readonly UserDbContext _context;

    public SessionService(UserDbContext context)
    {
        _context = context;
    }

    public async Task<(UserSession Session, string RawToken)> CreateSessionAsync(
        Guid userId,
        string? ipAddress,
        string? userAgent,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionTokenHash = tokenHash,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceName = ParseDevice(userAgent),
            CreatedAtUtc = DateTime.UtcNow,
            LastActiveAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromDays(7)),
            IsRevoked = false
        };

        await _context.UserSessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return (session, rawToken);
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _context.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAtUtc > now)
            .OrderByDescending(s => s.LastActiveAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session == null || session.IsRevoked) return false;

        session.IsRevoked = true;
        session.RevokedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> RevokeAllOtherSessionsAsync(
        Guid userId,
        Guid currentSessionId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var otherSessions = await _context.UserSessions
            .Where(s => s.UserId == userId && s.Id != currentSessionId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var s in otherSessions)
        {
            s.IsRevoked = true;
            s.RevokedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return otherSessions.Count;
    }

    private static string ParseDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown Device";
        if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)) return "Mobile Device";
        if (userAgent.Contains("Postman", StringComparison.OrdinalIgnoreCase)) return "Postman Client";
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows PC";
        if (userAgent.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)) return "Mac";
        if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase)) return "Linux PC";
        return "Web Browser";
    }
}
