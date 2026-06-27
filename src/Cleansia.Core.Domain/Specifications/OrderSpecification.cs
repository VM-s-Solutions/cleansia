using System.Linq;
using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class OrderSpecification : BaseSpecification<string?>, ISpecification<Order>
{
    public string? UserId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DisplayOrderNumber { get; set; }
    public string? EmployeeId { get; set; }
    public DateTime? CleaningDateFrom { get; set; }
    public DateTime? CleaningDateTo { get; set; }
    public IEnumerable<PaymentStatus>? PaymentStatuses { get; set; }
    public IEnumerable<PaymentType>? PaymentTypes { get; set; }
    public decimal? MinTotalPrice { get; set; }
    public decimal? MaxTotalPrice { get; set; }
    public IEnumerable<OrderStatus>? OrderStatuses { get; set; }
    public bool? HasAvailableSpots { get; set; }
    public bool? IsUnassigned { get; set; }
    public string? ExcludeEmployeeId { get; set; }

    // Server-pinned scope for a non-admin caller: results are restricted to
    // rows the caller is assigned to OR rows that still have an open spot the
    // caller could take. A non-admin can never read another employee's
    // exclusive (assigned, no-spot) rows, regardless of the client-supplied
    // EmployeeId filter. Null = no restriction (admin / unscoped).
    public string? RestrictToEmployeeId { get; set; }

    public Expression<Func<Order, bool>> SatisfiedBy()
    {
        Specification<Order> specification = new TrueSpecification<Order>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<Order>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<Order>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrEmpty(UserId))
        {
            specification &= new DirectSpecification<Order>(x => x.UserId == UserId);
        }

        if (!string.IsNullOrEmpty(CustomerName))
        {
            specification &= new DirectSpecification<Order>(x => x.CustomerName.Contains(CustomerName));
        }

        if (!string.IsNullOrEmpty(CustomerEmail))
        {
            specification &= new DirectSpecification<Order>(x => x.CustomerEmail.Contains(CustomerEmail));
        }

        if (!string.IsNullOrEmpty(CustomerPhone))
        {
            specification &= new DirectSpecification<Order>(x => x.CustomerPhone.Contains(CustomerPhone));
        }

        if (!string.IsNullOrEmpty(DisplayOrderNumber))
        {
            specification &= new DirectSpecification<Order>(x => x.DisplayOrderNumber.Contains(DisplayOrderNumber));
        }

        if (!string.IsNullOrEmpty(EmployeeId))
        {
            specification &= new DirectSpecification<Order>(x => x.AssignedEmployees.Select(x => x.EmployeeId).Contains(EmployeeId));
        }

        if (CleaningDateFrom.HasValue)
        {
            specification &= new DirectSpecification<Order>(x => x.CleaningDateTime >= CleaningDateFrom.Value);
        }

        if (CleaningDateTo.HasValue)
        {
            specification &= new DirectSpecification<Order>(x => x.CleaningDateTime <= CleaningDateTo.Value);
        }

        if (PaymentStatuses is not null && PaymentStatuses.Any())
        {
            specification &= new DirectSpecification<Order>(x => PaymentStatuses.Contains(x.PaymentStatus));
        }

        if (PaymentTypes is not null && PaymentTypes.Any())
        {
            specification &= new DirectSpecification<Order>(x => PaymentTypes.Contains(x.PaymentType));
        }

        if (MinTotalPrice.HasValue)
        {
            specification &= new DirectSpecification<Order>(x => x.TotalPrice >= MinTotalPrice.Value);
        }

        if (MaxTotalPrice.HasValue)
        {
            specification &= new DirectSpecification<Order>(x => x.TotalPrice <= MaxTotalPrice.Value);
        }

        if (OrderStatuses is not null && OrderStatuses.Any())
        {
            specification &= new DirectSpecification<Order>(x => OrderStatuses.Contains(x.OrderStatusHistory.OrderByDescending(s => s.CreatedOn).First().Status));
        }

        if (IsUnassigned.HasValue && IsUnassigned.Value)
        {
            specification &= new DirectSpecification<Order>(x => x.AssignedEmployees.Count == 0);
        }

        if (HasAvailableSpots.HasValue && HasAvailableSpots.Value)
        {
            specification &= new DirectSpecification<Order>(x => x.AssignedEmployees.Count < x.MaxEmployees);
        }

        if (!string.IsNullOrEmpty(ExcludeEmployeeId))
        {
            specification &= new DirectSpecification<Order>(x => x.AssignedEmployees.All(ae => ae.EmployeeId != ExcludeEmployeeId));
        }

        if (!string.IsNullOrEmpty(RestrictToEmployeeId))
        {
            specification &= new DirectSpecification<Order>(x =>
                x.AssignedEmployees.Any(ae => ae.EmployeeId == RestrictToEmployeeId)
                || x.AssignedEmployees.Count < x.MaxEmployees);
        }

        return specification.SatisfiedBy();
    }

    public static OrderSpecification Create(string? id = null, bool? isActive = null, string? customerName = null,
        string? customerEmail = null, string? customerPhone = null, string? displayOrderNumber = null,
        string? employeeId = null, DateTime? cleaningDateFrom = null,
        DateTime? cleaningDateTo = null, IEnumerable<PaymentStatus>? paymentStatuses = null, IEnumerable<PaymentType>? paymentTypes = null,
        decimal? minTotalPrice = null, decimal? maxTotalPrice = null, IEnumerable<OrderStatus>? orderStatuses = null,
        bool? hasAvailableSpots = null, bool? isUnassigned = null, string? excludeEmployeeId = null,
        string? userId = null, string? restrictToEmployeeId = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            UserId = userId,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            DisplayOrderNumber = displayOrderNumber,
            EmployeeId = employeeId,
            CleaningDateFrom = cleaningDateFrom,
            CleaningDateTo = cleaningDateTo,
            PaymentStatuses = paymentStatuses,
            PaymentTypes = paymentTypes,
            MinTotalPrice = minTotalPrice,
            MaxTotalPrice = maxTotalPrice,
            OrderStatuses = orderStatuses,
            HasAvailableSpots = hasAvailableSpots,
            IsUnassigned = isUnassigned,
            ExcludeEmployeeId = excludeEmployeeId,
            RestrictToEmployeeId = restrictToEmployeeId
        };
}