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


        Task<bool> RotateRefreshTokenAsync(
            Guid oldTokenId,
            DateTime nowUtc,
            RefreshToken newToken,
            CancellationToken cancellationToken = default);

    }
}
