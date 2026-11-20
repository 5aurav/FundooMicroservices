using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.User.Infrastructure.Security
{
    public class UserRepository : IUserRepository
    {
        private readonly UserDbContext _context;

        public UserRepository(UserDbContext context)
        {
            _context = context;
        }

        public async Task<AppUser?> GetByNormalizedEmailAsync(string normalizedEmail,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(
                    user => user.NormalizedEmail == normalizedEmail,
                    cancellationToken);
        }

        public async Task<AppUser?> GetByIdAsync(Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(
                    user => user.Id == id,
                    cancellationToken);
        }

        public async Task AddAsync(AppUser user,
            CancellationToken cancellationToken = default)
        {
            await _context.Users.AddAsync(user, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task AddPasswordResetTokenAsync(PasswordResetToken token,
            CancellationToken cancellationToken = default)
        {
            await _context.PasswordResetTokens.AddAsync(
                token,
                cancellationToken);
        }

        public async Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            return await _context.PasswordResetTokens
                .FirstOrDefaultAsync(
                    token => token.TokenHash == tokenHash,
                    cancellationToken);
        }

        public async Task InvalidatePasswordResetTokensAsync(Guid userId,
            DateTime usedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var tokens = await _context.PasswordResetTokens
                .Where(token =>
                    token.UserId == userId &&
                    token.UsedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.UsedAtUtc = usedAtUtc;
            }
        }


        public async Task<bool> ConsumePasswordResetTokenAndUpdatePasswordAsync(
            Guid userId,
            Guid resetTokenId,
            string passwordHash,
            int passwordHashVersion,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            var tokenUpdated = await _context.PasswordResetTokens
                .Where(token =>
                    token.Id == resetTokenId &&
                    token.UserId == userId &&
                    token.UsedAtUtc == null &&
                    token.ExpiresAtUtc > nowUtc)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        token => token.UsedAtUtc,
                        (DateTime?)nowUtc),
                    cancellationToken);

            if (tokenUpdated != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var userUpdated = await _context.Users
                .Where(user =>
                    user.Id == userId &&
                    user.IsActive)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(user => user.PasswordHash, passwordHash)
                        .SetProperty(user => user.PasswordHashVersion, passwordHashVersion)
                        .SetProperty(user => user.UpdatedAtUtc, nowUtc),
                    cancellationToken);

            if (userUpdated != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await _context.PasswordResetTokens
                .Where(token =>
                    token.UserId == userId &&
                    token.Id != resetTokenId &&
                    token.UsedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        token => token.UsedAtUtc,
                        (DateTime?)nowUtc),
                    cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return true;
        }


        public async Task AddRefreshTokenAsync(
            RefreshToken token,
            CancellationToken cancellationToken = default)
        {
            await _context.RefreshTokens.AddAsync(
                token,
                cancellationToken);
        }

        public async Task<RefreshToken?> GetRefreshTokenByHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            return await _context.RefreshTokens
    .AsNoTracking()
    .FirstOrDefaultAsync(
        token => token.TokenHash == tokenHash,
        cancellationToken);
        }


        public async Task<bool> RotateRefreshTokenAsync(
            Guid oldTokenId,
            DateTime nowUtc,
            RefreshToken newToken,
            CancellationToken cancellationToken = default)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            var updated = await _context.RefreshTokens
                .Where(token =>
                    token.Id == oldTokenId &&
                    token.RevokedAtUtc == null &&
                    token.ExpiresAtUtc > nowUtc)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(token => token.RevokedAtUtc, nowUtc)
                        .SetProperty(
                            token => token.ReplacedByTokenHash,
                            newToken.TokenHash),
                    cancellationToken);

            if (updated != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            await _context.RefreshTokens.AddAsync(
                newToken,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return true;
        }



        public async Task RevokeRefreshTokenFamilyAsync(
            Guid familyId,
            DateTime revokedAtUtc,
            CancellationToken cancellationToken = default)
        {
            await _context.RefreshTokens
                .Where(token =>
                    token.FamilyId == familyId &&
                    token.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        token => token.RevokedAtUtc,
                        revokedAtUtc),
                    cancellationToken);
        }

        public async Task<bool> RehashPasswordAsync(
            Guid userId,
            string newPasswordHash,
            int newHashVersion,
            DateTime nowUtc,
            CancellationToken cancellationToken = default)
        {
            var updated = await _context.Users
                .Where(user => user.Id == userId && user.IsActive)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(user => user.PasswordHash, newPasswordHash)
                        .SetProperty(user => user.PasswordHashVersion, newHashVersion)
                        .SetProperty(user => user.UpdatedAtUtc, nowUtc),
                    cancellationToken);

            return updated == 1;
        }

        public async Task AddOutboxMessageAsync(
            OutboxMessage message,
            CancellationToken cancellationToken = default)
        {
            await _context.OutboxMessages.AddAsync(message, cancellationToken);
        }
    }
}
