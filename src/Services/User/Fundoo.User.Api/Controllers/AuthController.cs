using Asp.Versioning;
using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Fundoo.User.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ISessionService _sessionService;
        private readonly IGoogleOAuthService _googleOAuthService;
        private readonly ITwoFactorAuthService _twoFactorService;
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;

        public AuthController(
            IAuthService authService,
            ISessionService sessionService,
            IGoogleOAuthService googleOAuthService,
            ITwoFactorAuthService twoFactorService,
            IUserRepository userRepository,
            ITokenService tokenService)
        {
            _authService = authService;
            _sessionService = sessionService;
            _googleOAuthService = googleOAuthService;
            _twoFactorService = twoFactorService;
            _userRepository = userRepository;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authService.RegisterAsync(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Password,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Conflict(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = result.Message,
                userId = result.UserId,
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var normalizedEmail = request.Email.Trim().ToUpperInvariant();
            var user = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);

            var result = await _authService.LoginAsync(
                request.Email,
                request.Password,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            // Check if 2FA is enabled
            if (user != null && user.IsTwoFactorEnabled)
            {
                return Ok(new
                {
                    isSuccess = true,
                    requiresTwoFactor = true,
                    userId = user.Id,
                    message = "Two-factor authentication required. Submit verification code to /api/v1/auth/login-2fa to complete login."
                });
            }

            // Record device session
            var (session, _) = await _sessionService.CreateSessionAsync(
                result.UserId!.Value,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                TimeSpan.FromDays(7),
                cancellationToken);

            return Ok(new
            {
                isSuccess = true,
                requiresTwoFactor = false,
                message = result.Message,
                userId = result.UserId,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                refreshTokenExpiresAtUtc = result.RefreshTokenExpiresAtUtc,
                sessionId = session.Id,
                tokenType = "Bearer"
            });
        }

        [HttpPost("login-2fa")]
        public async Task<IActionResult> LoginTwoFactor(
            [FromBody] TwoFactorLoginRequest request,
            CancellationToken cancellationToken)
        {
            var isValid = await _twoFactorService.VerifyTwoFactorCodeAsync(request.UserId, request.Code, cancellationToken);
            if (!isValid)
            {
                return Unauthorized(new { isSuccess = false, message = "Invalid two-factor code or recovery code." });
            }

            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { isSuccess = false, message = "User account unavailable." });
            }

            var accessToken = _tokenService.CreateAccessToken(user);

            var rawToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
            var now = DateTime.UtcNow;
            var expiry = now.AddDays(30);

            var refreshTokenEntity = new Fundoo.User.Domain.Entity.RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FamilyId = Guid.NewGuid(),
                TokenHash = tokenHash,
                CreatedAtUtc = now,
                ExpiresAtUtc = expiry
            };

            await _userRepository.AddRefreshTokenAsync(refreshTokenEntity, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);

            var (session, _) = await _sessionService.CreateSessionAsync(
                user.Id,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                TimeSpan.FromDays(7),
                cancellationToken);

            return Ok(new
            {
                isSuccess = true,
                message = "2FA verification successful.",
                userId = user.Id,
                accessToken,
                refreshToken = rawToken,
                refreshTokenExpiresAtUtc = expiry,
                sessionId = session.Id,
                tokenType = "Bearer"
            });
        }

        [Authorize]
        [HttpPost("2fa/setup")]
        public async Task<IActionResult> SetupTwoFactor(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var (secret, qrCodeUri, recoveryCodes) = await _twoFactorService.InitiateTwoFactorSetupAsync(userId.Value, cancellationToken);
            return Ok(new
            {
                isSuccess = true,
                secret,
                qrCodeUri,
                recoveryCodes,
                message = "Scan the QR code with your authenticator app (Google Authenticator, Microsoft Authenticator) and call /api/v1/auth/2fa/enable with the 6-digit code to activate 2FA."
            });
        }

        [Authorize]
        [HttpPost("2fa/enable")]
        public async Task<IActionResult> EnableTwoFactor(
            [FromBody] TwoFactorVerifyRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var enabled = await _twoFactorService.EnableTwoFactorAsync(userId.Value, request.Code, cancellationToken);
            if (!enabled)
            {
                return BadRequest(new { isSuccess = false, message = "Invalid two-factor authentication code." });
            }

            return Ok(new { isSuccess = true, message = "Two-factor authentication successfully enabled." });
        }

        [Authorize]
        [HttpPost("2fa/disable")]
        public async Task<IActionResult> DisableTwoFactor(
            [FromBody] TwoFactorVerifyRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var disabled = await _twoFactorService.DisableTwoFactorAsync(userId.Value, request.Code, cancellationToken);
            if (!disabled)
            {
                return BadRequest(new { isSuccess = false, message = "Invalid verification code or recovery code." });
            }

            return Ok(new { isSuccess = true, message = "Two-factor authentication successfully disabled." });
        }

        [HttpPost("forget-password")]
        public async Task<IActionResult> ForgetPassword(
            [FromBody] ForgotPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var isSuccess = await _authService.ForgotPasswordAsync(
                request.Email,
                cancellationToken);

            if (!isSuccess)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    isSuccess = false,
                    message = "Unable to send password reset instructions. Please try again later."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "If the account exists, password reset instructions will be sent to the registered email."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            [FromBody] ResetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var isReset = await _authService.ResetPasswordAsync(
                request.Email,
                request.Token,
                request.NewPassword,
                cancellationToken);

            if (!isReset)
            {
                return BadRequest(new
                {
                    isSuccess = false,
                    message = "The password reset request is invalid or has expired."
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "Password has been reset successfully."
            });
        }

        // ==========================================
        // Session Management & Token Renewal (Team 3)
        // ==========================================

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authService.RefreshAccessTokenAsync(request.RefreshToken, cancellationToken);
            if (!result.IsSuccess)
            {
                return Unauthorized(new
                {
                    isSuccess = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                isSuccess = true,
                message = result.Message,
                userId = result.UserId,
                accessToken = result.AccessToken,
                refreshToken = result.RefreshToken,
                refreshTokenExpiresAtUtc = result.RefreshTokenExpiresAtUtc,
                tokenType = "Bearer"
            });
        }

        [Authorize]
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var sessions = await _sessionService.GetActiveSessionsAsync(userId.Value, cancellationToken);
            return Ok(new
            {
                isSuccess = true,
                sessions = sessions.Select(s => new
                {
                    id = s.Id,
                    deviceName = s.DeviceName,
                    ipAddress = s.IpAddress,
                    userAgent = s.UserAgent,
                    createdAtUtc = s.CreatedAtUtc,
                    lastActiveAtUtc = s.LastActiveAtUtc,
                    expiresAtUtc = s.ExpiresAtUtc
                })
            });
        }

        [Authorize]
        [HttpDelete("sessions/{sessionId:guid}")]
        public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var revoked = await _sessionService.RevokeSessionAsync(userId.Value, sessionId, cancellationToken);
            if (!revoked)
            {
                return NotFound(new { isSuccess = false, message = "Session not found or already ended." });
            }

            return Ok(new { isSuccess = true, message = "Session ended successfully." });
        }

        [Authorize]
        [HttpPost("sessions/revoke-all")]
        public async Task<IActionResult> RevokeAllOtherSessions([FromQuery] Guid currentSessionId, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var count = await _sessionService.RevokeAllOtherSessionsAsync(userId.Value, currentSessionId, cancellationToken);
            return Ok(new { isSuccess = true, revokedCount = count, message = $"{count} other session(s) ended successfully." });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                await _authService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
            }

            return Ok(new
            {
                isSuccess = true,
                message = "Logged out successfully. Session ended."
            });
        }

        // ==========================================
        // Google OAuth 2.0 Integration
        // ==========================================

        [HttpGet("google/url")]
        public IActionResult GetGoogleAuthorizationUrl([FromQuery] string? redirectUri, [FromQuery] string? state)
        {
            var targetState = state ?? Guid.NewGuid().ToString("N");
            var url = _googleOAuthService.GetAuthorizationUrl(targetState, redirectUri);
            return Ok(new
            {
                isSuccess = true,
                authorizationUrl = url,
                state = targetState
            });
        }

        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallbackGet(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            [FromQuery] string? redirectUri,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(error))
            {
                return BadRequest(new { isSuccess = false, message = $"Google OAuth error: {error}" });
            }

            if (string.IsNullOrEmpty(code))
            {
                return BadRequest(new { isSuccess = false, message = "Missing authorization code from Google." });
            }

            var result = await _googleOAuthService.HandleCallbackAsync(code, redirectUri, cancellationToken);
            if (!result.IsSuccess)
            {
                return BadRequest(new { isSuccess = false, message = result.Message });
            }

            return Ok(new
            {
                isSuccess = true,
                userId = result.UserId,
                accessToken = result.AccessToken,
                tokenType = "Bearer",
                message = result.Message,
                state
            });
        }

        [HttpPost("google/callback")]
        public async Task<IActionResult> GoogleCallbackPost(
            [FromBody] OAuthCallbackRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _googleOAuthService.HandleCallbackAsync(request.Code, request.RedirectUri, cancellationToken);
            if (!result.IsSuccess)
            {
                return BadRequest(new { isSuccess = false, message = result.Message });
            }

            return Ok(new
            {
                isSuccess = true,
                userId = result.UserId,
                accessToken = result.AccessToken,
                tokenType = "Bearer",
                message = result.Message
            });
        }

        [HttpPost("google/signin")]
        public async Task<IActionResult> GoogleSignInWithIdToken(
            [FromBody] GoogleSignInRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _googleOAuthService.ValidateIdTokenAsync(request.IdToken, cancellationToken);
            if (!result.IsSuccess)
            {
                return BadRequest(new { isSuccess = false, message = result.Message });
            }

            return Ok(new
            {
                isSuccess = true,
                userId = result.UserId,
                accessToken = result.AccessToken,
                tokenType = "Bearer",
                message = result.Message
            });
        }

        [Authorize]
        [HttpPost("google/link")]
        public async Task<IActionResult> LinkGoogleAccount(
            [FromBody] OAuthCallbackRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var linked = await _googleOAuthService.LinkAccountAsync(userId.Value, request.Code, request.RedirectUri, cancellationToken);
            if (!linked)
            {
                return BadRequest(new { isSuccess = false, message = "Failed to link Google account or already linked to another account." });
            }

            return Ok(new { isSuccess = true, message = "Google account successfully linked." });
        }

        private Guid? GetCurrentUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
            if (Guid.TryParse(sub, out var guid)) return guid;
            return null;
        }
    }
}
