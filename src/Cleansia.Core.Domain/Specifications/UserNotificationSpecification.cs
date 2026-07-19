using System.Linq.Expressions;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class UserNotificationSpecification : BaseSpecification<string?>, ISpecification<UserNotification>
{
    public string? UserId { get; set; }

    public IReadOnlyList<string>? EventKeys { get; set; }

    public Expression<Func<UserNotification, bool>> SatisfiedBy()
    {
        Specification<UserNotification> specification = new TrueSpecification<UserNotification>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<UserNotification>(x => x.Id == Id);
        }

        if (!string.IsNullOrWhiteSpace(UserId))
        {
            specification &= new DirectSpecification<UserNotification>(x => x.UserId == UserId);
        }

        if (EventKeys is { Count: > 0 })
        {
            var eventKeys = EventKeys;
            specification &= new DirectSpecification<UserNotification>(x => eventKeys.Contains(x.EventKey));
        }

        return specification.SatisfiedBy();
    }

    public static UserNotificationSpecification Create(string userId, IReadOnlyList<string> eventKeys) =>
        new()
        {
            UserId = userId,
            EventKeys = eventKeys,
        };
}
