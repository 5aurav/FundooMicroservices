using Asp.Versioning;
using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.User.Api.Controllers
{
    [ApiController]
    [Asp.Versioning.ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/auth")]
    public class AuthControllerV2 : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthControllerV2(IAuthService authService)
        {
            _authService = authService;
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
                apiVersion = "2.0"
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequestV2 request,
            CancellationToken cancellationToken)
        {
            var result = await _authService.LoginV2Async(
                request.Email,
                request.Password,
                request.RememberMe,
                cancellationToken);

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
                tokenType = "Bearer",
                apiVersion = "2.0"
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ForgotPasswordRequest request,
            CancellationToken cancellationToken)
        {
            var isSuccess = await _authService.ForgotPasswordAsync(
                request.Email,
                cancellationToken);

            if (!isSuccess)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new
                    {
                        isSuccess = false,
                        message = "Unable to send password reset instructions. Please try again later."
                    });
            }

            return Ok(new
            {
                isSuccess = true,
                message = "If the account exists, password reset instructions will be sent to the registered email.",
                apiVersion = "2.0"
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
                message = "Password has been reset successfully.",
                apiVersion = "2.0"
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authService.RefreshAccessTokenAsync(
                request.RefreshToken,
                cancellationToken);

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
                tokenType = "Bearer",
                apiVersion = "2.0"
            });
        }
    }
}