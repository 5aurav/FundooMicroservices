using FluentValidation;
using Fundoo.Notification.Application.DTO;

namespace Fundoo.Notification.Api.Validation;

public sealed class PasswordResetNotificationRequestValidator
    : AbstractValidator<PasswordResetNotificationRequest>
{
    public PasswordResetNotificationRequestValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("Idempotency key is required.");

        RuleFor(x => x.RecipientEmail)
            .NotEmpty().WithMessage("Recipient email is required.")
            .EmailAddress().WithMessage("Enter a valid recipient email address.");

        RuleFor(x => x.ResetToken)
            .NotEmpty().WithMessage("Reset token is required.");
    }
}
