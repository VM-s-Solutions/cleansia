namespace Cleansia.Core.Domain.Outbox;

public enum OutboxMessageStatus
{
    Pending = 0,
    Dispatched = 1,
    Failed = 2,
}
