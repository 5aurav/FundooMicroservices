using FluentValidation;
using Fundoo.Notes.Api.Contracts.Requests;

namespace Fundoo.Notes.Api.Validation;

public sealed class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Color)
            .MaximumLength(50).WithMessage("Color cannot exceed 50 characters.");
    }
}
