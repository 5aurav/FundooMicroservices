using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fundoo.User.Application.Abstractions
{
    public interface IAuthService
    {
        Task<AuthResult> RegisterAsync(string firstName, string lastName, string email, string password,
        CancellationToken cancellationToken = default);

        Task<AuthResult> LoginAsync(string email, string password,
            CancellationToken cancellationToken = default);

        Task<bool> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);

        Task<bool> ResetPasswordAsync(string email, string token, string newPassword,
            CancellationToken cancellationToken = default);

        Task<AuthResult> LoginV2Async(
            string email,
            string password,
            bool rememberMe,
            CancellationToken cancellationToken = default);

        Task<AuthResult> RefreshAccessTokenAsync(
            string refreshToken,
            CancellationToken cancellationToken = default);
    }
    public class AuthResult
    {
        public bool IsSuccess { get; init; }

        public string Message { get; init; } = string.Empty;

        public Guid? UserId { get; init; }

        public string? AccessToken { get; init; }

        public string? RefreshToken { get; init; }

        public DateTime? RefreshTokenExpiresAtUtc { get; init; }
    }
}
