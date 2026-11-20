using FluentValidation;
using Fundoo.Notes.Api.Contracts.Requests;

namespace Fundoo.Notes.Api.Validation;

public sealed class UpdateLabelRequestValidator : AbstractValidator<UpdateLabelRequest>
{
    public UpdateLabelRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithMessage("Label name is required.")
            .MaximumLength(100).WithMessage("Label name cannot exceed 100 characters.");
    }
}
