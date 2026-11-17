using FluentValidation;
using TaskFlow.Application.Queries.Notifications;

namespace TaskFlow.Application.Validators;

public class GetMyNotificationsQueryValidator : AbstractValidator<GetMyNotificationsQuery>
{
    public GetMyNotificationsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetMyNotificationsQuery.MaxPageSize);
    }
}
