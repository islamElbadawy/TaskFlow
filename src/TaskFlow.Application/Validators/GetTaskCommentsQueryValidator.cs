using FluentValidation;
using TaskFlow.Application.Queries.Comments;

namespace TaskFlow.Application.Validators;

public class GetTaskCommentsQueryValidator : AbstractValidator<GetTaskCommentsQuery>
{
    public GetTaskCommentsQueryValidator()
    {
        // TaskId comes from the route (set by the controller after model validation), so it is not validated here.
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetTaskCommentsQuery.MaxPageSize);
    }
}
