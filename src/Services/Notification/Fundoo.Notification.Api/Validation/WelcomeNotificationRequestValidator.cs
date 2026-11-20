using FluentValidation;
using Fundoo.Notification.Application.DTO;

namespace Fundoo.Notification.Api.Validation;

public sealed class WelcomeNotificationRequestValidator
    : AbstractValidator<WelcomeNotificationRequest>
{
    public WelcomeNotificationRequestValidator()
    {
        RuleFor(x => x.RecipientEmail)
            .NotEmpty().WithMessage("Recipient email is required.")
            .EmailAddress().WithMessage("Enter a valid recipient email address.");

        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("Recipient name is required.");
    }
}
