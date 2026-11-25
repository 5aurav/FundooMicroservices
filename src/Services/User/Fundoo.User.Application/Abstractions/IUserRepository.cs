using Fundoo.User.Domain.Entity;


namespace Fundoo.User.Application.Abstractions
{
    public interface IUserRepository
    {
        Task<AppUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

        Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task AddAsync(AppUser user, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        Task AddPasswordResetTokenAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

        Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash,
            CancellationToken cancellationToken = default);

        Task InvalidatePasswordResetTokensAsync(Guid userId, DateTime usedAtUtc,
            CancellationToken cancellationToken = default);


        Task<bool> ConsumePasswordResetTokenAndUpdatePasswordAsync(
            Guid userId,
            Guid resetTokenId,
            string passwordHash,
            int passwordHashVersion,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);


        Task AddRefreshTokenAsync(
            RefreshToken token,
            CancellationToken cancellationToken = default);

        Task<RefreshToken?> GetRefreshTokenByHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default);


        Task RevokeRefreshTokenFamilyAsync(
            Guid familyId,
            DateTime revokedAtUtc,
            CancellationToken cancellationToken = default);

        Task<bool> RevokeRefreshTokenByHashAsync(
            string tokenHash,
            DateTime revokedAtUtc,
            CancellationToken cancellationToken = default);


        Task<bool> RotateRefreshTokenAsync(
            Guid oldTokenId,
            DateTime nowUtc,
            RefreshToken newToken,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the user's password hash and version atomically. Used during
        /// lazy BCrypt→PBKDF2 migration on successful login.
        /// </summary>
        Task<bool> RehashPasswordAsync(
            Guid userId,
            string newPasswordHash,
            int newHashVersion,
            DateTime nowUtc,
            CancellationToken cancellationToken = default);

        Task AddOutboxMessageAsync(
            OutboxMessage message,
            CancellationToken cancellationToken = default);
    }
}
