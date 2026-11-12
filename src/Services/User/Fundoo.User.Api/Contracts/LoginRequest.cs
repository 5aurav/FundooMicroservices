using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Api.Contracts
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(PasswordRules.MaxLength, ErrorMessage = PasswordRules.LengthMessage)]
        public string Password { get; set; } = string.Empty;
    }
}
