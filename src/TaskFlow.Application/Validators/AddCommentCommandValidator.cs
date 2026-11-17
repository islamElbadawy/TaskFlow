using FluentValidation;
using TaskFlow.Application.Commands.Comments;

namespace TaskFlow.Application.Validators;

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        // TaskId comes from the route (set by the controller after model validation), so it is not validated here.
        RuleFor(x => x.Content).NotEmpty().WithMessage("Content is required")
            .MaximumLength(1000).WithMessage("Content must be at most 1000 characters");
    }
}
