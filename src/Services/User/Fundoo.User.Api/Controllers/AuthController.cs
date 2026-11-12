using Asp.Versioning;
using Fundoo.User.Api.Contracts;
using Fundoo.User.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.User.Api.Controllers
{
    [ApiController]
    [Microsoft.AspNetCore.Mvc.ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request,
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
        public async Task<IActionResult> Login([FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
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

            return Ok(new
            {
                isSuccess = true,
                message = result.Message,
                userId = result.UserId,
                accessToken = result.AccessToken,
                tokenType = "Bearer",
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
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request,
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
    }
}