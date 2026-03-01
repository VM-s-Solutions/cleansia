using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Orders;

public class OrderIssue : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(26)]
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    [Required]
    [MaxLength(26)]
    public string ReportedByEmployeeId { get; private set; }

    [Required]
    [MaxLength(2000)]
    public string Description { get; private set; }

    public bool IsResolved { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public static OrderIssue Create(string orderId, string reportedByEmployeeId, string description) => new()
    {
        OrderId = orderId,
        ReportedByEmployeeId = reportedByEmployeeId,
        Description = description,
        IsResolved = false
    };

    public OrderIssue Resolve()
    {
        IsResolved = true;
        ResolvedAt = DateTimeOffset.UtcNow;
        return this;
    }
}
