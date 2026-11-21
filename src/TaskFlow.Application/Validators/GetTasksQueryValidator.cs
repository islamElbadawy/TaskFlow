using FluentValidation;
using TaskFlow.Application.Queries.Tasks;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Validators;

public class GetTasksQueryValidator : AbstractValidator<GetTasksQuery>
{
    public GetTasksQueryValidator()
    {
        // An unknown status or priority is an error, not "no filter".
        When(x => !string.IsNullOrEmpty(x.Status), () =>
            RuleFor(x => x.Status).IsEnumName(typeof(TaskItemStatus), caseSensitive: false)
                .WithMessage("Status must be one of: Todo, InProgress, Done"));

        When(x => !string.IsNullOrEmpty(x.Priority), () =>
            RuleFor(x => x.Priority).IsEnumName(typeof(TaskPriority), caseSensitive: false)
                .WithMessage("Priority must be one of: Low, Medium, High"));

        When(x => !string.IsNullOrEmpty(x.Sort), () =>
            RuleFor(x => x.Sort)
                .Must(sort => string.Equals(sort, GetTasksQuery.SortByCreatedAt, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(sort, GetTasksQuery.SortByPosition, StringComparison.OrdinalIgnoreCase))
                .WithMessage("Sort must be one of: createdAt, position"));

        RuleFor(x => x.Unassigned).Equal(false).When(x => x.AssignedToId.HasValue)
            .WithMessage("Use either assignedToId or unassigned, not both");

        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetTasksQuery.MaxPageSize);
    }
}
