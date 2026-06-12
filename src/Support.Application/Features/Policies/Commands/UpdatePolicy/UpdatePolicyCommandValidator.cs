using FluentValidation;

namespace Support.Application.Features.Policies.Commands.UpdatePolicy;

public class UpdatePolicyCommandValidator : AbstractValidator<UpdatePolicyCommand>
{
    public UpdatePolicyCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(500).WithMessage("Title must be at most 500 characters");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required");
    }
}
