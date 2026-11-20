using FluentValidation;
using Fundoo.User.Api.Contracts;

namespace Fundoo.User.Api.Validation;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .Length(PasswordRules.MinLength, PasswordRules.MaxLength)
            .WithMessage(PasswordRules.LengthMessage)
            .Matches(PasswordRules.ComplexityPattern)
            .WithMessage(PasswordRules.ComplexityMessage);
    }
}
