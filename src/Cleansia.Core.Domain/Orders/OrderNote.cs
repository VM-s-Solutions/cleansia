using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Orders;

public class OrderNote : Auditable, ITenantEntity
{
    [Required]
    [MaxLength(26)]
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    [Required]
    [MaxLength(26)]
    public string EmployeeId { get; private set; }

    [Required]
    [MaxLength(2000)]
    public string Content { get; private set; }

    public static OrderNote Create(string orderId, string employeeId, string content) => new()
    {
        OrderId = orderId,
        EmployeeId = employeeId,
        Content = content
    };

    public OrderNote UpdateContent(string content)
    {
        Content = content;
        return this;
    }

    public OrderNote Anonymize()
    {
        Content = AnonymizationMarker.Value;
        return this;
    }
}
