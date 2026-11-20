using Fundoo.Common.Messaging.Events;
using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entity;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fundoo.User.Infrastructure.Security
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService, IHttpClientFactory httpClientFactory, ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<AuthResult> RegisterAsync(string firstName, string lastName, string email, string password,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var existingUser =
                await _userRepository.GetByNormalizedEmailAsync(
                    normalizedEmail,
                    cancellationToken);

            if (existingUser is not null)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Email is already registered."
                };
            }

            var user = new AppUser
            {
                Id = Guid.NewGuid(),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Email = email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = _passwordHasher.Hash(password),
                PasswordHashVersion = 2, // PBKDF2
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, cancellationToken);

            var userRegisteredEvent = new UserRegisteredEvent
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                OccurredAtUtc = DateTime.UtcNow
            };

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = nameof(UserRegisteredEvent),
                Payload = JsonSerializer.Serialize(userRegisteredEvent),
                CorrelationId = user.Id.ToString(),
                CreatedAtUtc = DateTime.UtcNow
            };

            await _userRepository.AddOutboxMessageAsync(outboxMessage, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);

            var accessToken = _tokenService.CreateAccessToken(user);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Registration successful.",
                //AccessToken = accessToken,
                UserId = user.Id
            };
        }

        public async Task<AuthResult> LoginAsync(string email, string password,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user =
                await _userRepository.GetByNormalizedEmailAsync(
                    normalizedEmail,
                    cancellationToken);

            if (user is null ||
                !user.IsActive ||
                !_passwordHasher.Verify(password, user.PasswordHash))
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Invalid email or password."
                };
            }

            // Lazy migration: rehash BCrypt passwords to PBKDF2 on successful login
            if (NeedsPasswordRehash(user))
            {
                await RehashPasswordAsync(user.Id, password, cancellationToken);
            }

            var accessToken = _tokenService.CreateAccessToken(user);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Login successful.",
                AccessToken = accessToken,
                UserId = user.Id
            };
        }

        public async Task<bool> ForgotPasswordAsync(string email,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null || !user.IsActive)
                return true;

            var rawToken = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));

            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

            var now = DateTime.UtcNow;

            // Invalidate older, unused reset tokens.
            await _userRepository.InvalidatePasswordResetTokensAsync(
                user.Id,
                now,
                cancellationToken);

            var resetToken = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAtUtc = now.AddMinutes(15),
                UsedAtUtc = null
            };

            await _userRepository.AddPasswordResetTokenAsync(
                resetToken,
                cancellationToken);

            var resetEvent = new PasswordResetRequestedEvent
            {
                ResetTokenId = resetToken.Id,
                UserId = user.Id,
                RecipientEmail = user.Email,
                RecipientName = user.FirstName,
                ResetToken = rawToken,
                ExpiresAtUtc = resetToken.ExpiresAtUtc,
                OccurredAtUtc = now
            };

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = nameof(PasswordResetRequestedEvent),
                Payload = JsonSerializer.Serialize(resetEvent),
                CorrelationId = resetToken.Id.ToString(),
                CreatedAtUtc = now
            };

            await _userRepository.AddOutboxMessageAsync(
                outboxMessage,
                cancellationToken);

            await _userRepository.SaveChangesAsync(cancellationToken);

            try
            {
                var httpClient = _httpClientFactory.CreateClient("NotificationService");

                var notificationRequest = new PasswordResetNotificationRequest
                {
                    IdempotencyKey = resetToken.Id.ToString(),
                    RecipientEmail = user.Email,
                    RecipientName = user.FirstName,
                    ResetToken = rawToken
                };

                var response = await httpClient.PostAsJsonAsync(
                    "api/notifications/password-reset",
                    notificationRequest,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Direct notification HTTP call returned {StatusCode} for user {UserId}. Event is queued in Transactional Outbox for Kafka delivery.",
                        (int)response.StatusCode, user.Id);
                }

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Direct notification HTTP call failed for user {UserId}. Event is safely queued in Transactional Outbox for Kafka delivery.", user.Id);

                return true;
            }
        }


        public async Task<bool> ResetPasswordAsync(
            string email,
            string token,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                return false;
            }

            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null || !user.IsActive)
            {
                return false;
            }

            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(token)));

            var resetToken = await _userRepository.GetPasswordResetTokenByHashAsync(
                tokenHash,
                cancellationToken);

            var now = DateTime.UtcNow;

            if (resetToken is null ||
                resetToken.UserId != user.Id ||
                resetToken.UsedAtUtc is not null ||
                resetToken.ExpiresAtUtc <= now)
            {
                return false;
            }

            var newPasswordHash = _passwordHasher.Hash(newPassword);

            return await _userRepository
                .ConsumePasswordResetTokenAndUpdatePasswordAsync(
                    user.Id,
                    resetToken.Id,
                    newPasswordHash,
                    2, // PBKDF2
                    now,
                    cancellationToken);
        }


        public async Task<AuthResult> LoginV2Async(
            string email,
            string password,
            bool rememberMe,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null ||
                !user.IsActive ||
                !_passwordHasher.Verify(password, user.PasswordHash))
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Invalid email or password."
                };
            }

            // Lazy migration: rehash BCrypt passwords to PBKDF2 on successful login
            if (NeedsPasswordRehash(user))
            {
                await RehashPasswordAsync(user.Id, password, cancellationToken);
            }

            var accessToken = _tokenService.CreateAccessToken(user);

            string? refreshToken = null;
            DateTime? refreshTokenExpiresAtUtc = null;

            if (rememberMe)
            {
                var rawToken = Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32));

                var tokenHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

                var now = DateTime.UtcNow;
                var expiry = now.AddDays(30);

                var familyId = Guid.NewGuid();

                var refreshTokenEntity = new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    FamilyId = familyId,
                    TokenHash = tokenHash,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = expiry
                };

                await _userRepository.AddRefreshTokenAsync(
                    refreshTokenEntity,
                    cancellationToken);

                await _userRepository.SaveChangesAsync(cancellationToken);

                refreshToken = rawToken;
                refreshTokenExpiresAtUtc = expiry;
            }

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Login successful.",
                UserId = user.Id,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc
            };
        }


        public async Task<AuthResult> RefreshAccessTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Refresh token is required."
                };
            }

            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

            var existingToken = await _userRepository.GetRefreshTokenByHashAsync(
                tokenHash,
                cancellationToken);

            var now = DateTime.UtcNow;

            if (existingToken is null)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Refresh token is invalid."
                };
            }

            if (existingToken.RevokedAtUtc is not null)
            {
                if (!string.IsNullOrWhiteSpace(existingToken.ReplacedByTokenHash))
                {
                    await _userRepository.RevokeRefreshTokenFamilyAsync(
                        existingToken.FamilyId,
                        now,
                        cancellationToken);

                    _logger.LogWarning(
                        "Refresh token replay detected for user {UserId}, family {FamilyId}",
                        existingToken.UserId,
                        existingToken.FamilyId);
                }

                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Refresh token is invalid, expired, or revoked."
                };
            }

            if (existingToken.ExpiresAtUtc <= now)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Refresh token has expired."
                };
            }

            var user = await _userRepository.GetByIdAsync(
                existingToken.UserId,
                cancellationToken);

            if (user is null || !user.IsActive)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "User account is unavailable."
                };
            }

            var newRawToken = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32));

            var newTokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(newRawToken)));

            var expiry = now.AddDays(30);

            var newRefreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FamilyId = existingToken.FamilyId,
                TokenHash = newTokenHash,
                CreatedAtUtc = now,
                ExpiresAtUtc = expiry
            };

            var rotated = await _userRepository.RotateRefreshTokenAsync(
                existingToken.Id,
                now,
                newRefreshToken,
                cancellationToken);


            if (!rotated)
            {
                var latestToken = await _userRepository.GetRefreshTokenByHashAsync(
                    tokenHash,
                    cancellationToken);

                if (latestToken is not null &&
                    latestToken.RevokedAtUtc is not null &&
                    !string.IsNullOrWhiteSpace(latestToken.ReplacedByTokenHash))
                {
                    await _userRepository.RevokeRefreshTokenFamilyAsync(
                        latestToken.FamilyId,
                        DateTime.UtcNow,
                        cancellationToken);

                    _logger.LogWarning(
                        "Concurrent refresh token replay detected for user {UserId}, family {FamilyId}",
                        latestToken.UserId,
                        latestToken.FamilyId);
                }

                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Refresh token has already been used or expired."
                };
            }

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Access token refreshed successfully.",
                UserId = user.Id,
                AccessToken = _tokenService.CreateAccessToken(user),
                RefreshToken = newRawToken,
                RefreshTokenExpiresAtUtc = expiry
            };
        }

        private bool NeedsPasswordRehash(AppUser user)
        {
            // Null or version 1 means BCrypt
            if (!user.PasswordHashVersion.HasValue || user.PasswordHashVersion.Value == 1)
            {
                // Double-check the hash format
                return user.PasswordHash.StartsWith("$2", StringComparison.Ordinal);
            }

            return false;
        }

        private async Task RehashPasswordAsync(
            Guid userId,
            string plainPassword,
            CancellationToken cancellationToken)
        {
            try
            {
                var newHash = _passwordHasher.Hash(plainPassword);
                var rehashed = await _userRepository.RehashPasswordAsync(
                    userId,
                    newHash,
                    2, // PBKDF2
                    DateTime.UtcNow,
                    cancellationToken);

                if (rehashed)
                {
                    _logger.LogInformation(
                        "Rehashed password for user {UserId} from BCrypt to PBKDF2",
                        userId);
                }
            }
            catch (Exception ex)
            {
                // Log but don't block login if rehash fails
                _logger.LogWarning(
                    ex,
                    "Failed to rehash password for user {UserId}",
                    userId);
            }
        }

    }
}