using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Api.Contracts
{
    public sealed class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        [StringLength(PasswordRules.MaxLength, MinimumLength = PasswordRules.MinLength,
            ErrorMessage = PasswordRules.LengthMessage)]
        [RegularExpression(PasswordRules.ComplexityPattern,
            ErrorMessage = PasswordRules.ComplexityMessage)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
