using System.ComponentModel.DataAnnotations;

namespace Fundoo.User.Api.Contracts
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z]+(?:[ '-][a-zA-Z]+)*$",
            ErrorMessage = "First name contains invalid characters.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 50 characters.")]
        [RegularExpression(@"^[a-zA-Z]+(?:[ '-][a-zA-Z]+)*$",
            ErrorMessage = "Last name contains invalid characters.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(PasswordRules.MaxLength, MinimumLength = PasswordRules.MinLength,
            ErrorMessage = PasswordRules.LengthMessage)]
        [RegularExpression(PasswordRules.ComplexityPattern,
            ErrorMessage = PasswordRules.ComplexityMessage)]
        public string Password { get; set; } = string.Empty;
    }
}
