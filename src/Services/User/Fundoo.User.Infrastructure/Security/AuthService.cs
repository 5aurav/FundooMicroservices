using Fundoo.User.Application.Abstractions;
using Fundoo.User.Domain.Entity;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

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
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user, cancellationToken);
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
                    _logger.LogError(
                        "Notification service returned {StatusCode} for password reset of user {UserId}",
                        (int)response.StatusCode, user.Id);

                    return false;
                }

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send password reset email for user {UserId}", user.Id);

                return false;
            }
        }

        public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword,
            CancellationToken cancellationToken = default)
        {
            var normalizedEmail = email.Trim().ToUpperInvariant();

            var user = await _userRepository.GetByNormalizedEmailAsync(
                normalizedEmail,
                cancellationToken);

            if (user is null || !user.IsActive)
                return false;

            if (string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                return false;
            }

            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

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

            await _userRepository.UpdatePasswordAsync(
                user,
                newPasswordHash,
                now,
                cancellationToken);

            resetToken.UsedAtUtc = now;

            await _userRepository.SaveChangesAsync(cancellationToken);

            return true;
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

                var refreshTokenEntity = new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
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

            if (existingToken is null || !existingToken.IsActive)
            {
                return new AuthResult
                {
                    IsSuccess = false,
                    Message = "Refresh token is invalid, expired, or revoked."
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

            var newExpiry = now.AddDays(30);

            existingToken.RevokedAtUtc = now;
            existingToken.ReplacedByTokenHash = newTokenHash;

            var newRefreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = newTokenHash,
                CreatedAtUtc = now,
                ExpiresAtUtc = newExpiry
            };

            await _userRepository.AddRefreshTokenAsync(
                newRefreshToken,
                cancellationToken);

            var newAccessToken = _tokenService.CreateAccessToken(user);

            await _userRepository.SaveChangesAsync(cancellationToken);

            return new AuthResult
            {
                IsSuccess = true,
                Message = "Access token refreshed successfully.",
                UserId = user.Id,
                AccessToken = newAccessToken,
                RefreshToken = newRawToken,
                RefreshTokenExpiresAtUtc = newExpiry
            };
        }
    }
}