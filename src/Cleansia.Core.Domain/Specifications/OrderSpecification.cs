using System.Linq.Expressions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class OrderSpecification : BaseSpecification<string?>, ISpecification<Order>
{
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DisplayOrderNumber { get; set; }
    public string? EmployeeId { get; set; }
    public string? PackageId { get; set; }
    public DateTime? CleaningDateFrom { get; set; }
    public DateTime? CleaningDateTo { get; set; }
    public int[]? PaymentStatuses { get; set; }
    public int[]? PaymentTypes { get; set; }
    public decimal? MinTotalPrice { get; set; }
    public decimal? MaxTotalPrice { get; set; }

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
            specification &= new DirectSpecification<Order>(x => x.EmployeeId == EmployeeId);
        }

        if (!string.IsNullOrEmpty(PackageId))
        {
            specification &= new DirectSpecification<Order>(x => x.SelectedPackageId == PackageId);
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
            specification &= new DirectSpecification<Order>(x => PaymentStatuses.Contains((int)x.PaymentStatus));
        }

        if (PaymentTypes is not null && PaymentTypes.Any())
        {
            specification &= new DirectSpecification<Order>(x => PaymentTypes.Contains((int)x.PaymentType));
        }

        if (MinTotalPrice.HasValue)
        {
            specification &= new DirectSpecification<Order>(x => x.TotalPrice >= MinTotalPrice.Value);
        }

        if (MaxTotalPrice.HasValue)
        {
            specification &= new DirectSpecification<Order>(x => x.TotalPrice <= MaxTotalPrice.Value);
        }

        return specification.SatisfiedBy();
    }

    public static OrderSpecification Create(string? id = null, bool? isActive = null, string? customerName = null,
        string? customerEmail = null, string? customerPhone = null, string? displayOrderNumber = null,
        string? employeeId = null, string? packageId = null, DateTime? cleaningDateFrom = null,
        DateTime? cleaningDateTo = null, int[]? paymentStatuses = null, int[]? paymentTypes = null,
        decimal? minTotalPrice = null, decimal? maxTotalPrice = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            DisplayOrderNumber = displayOrderNumber,
            EmployeeId = employeeId,
            PackageId = packageId,
            CleaningDateFrom = cleaningDateFrom,
            CleaningDateTo = cleaningDateTo,
            PaymentStatuses = paymentStatuses,
            PaymentTypes = paymentTypes,
            MinTotalPrice = minTotalPrice,
            MaxTotalPrice = maxTotalPrice
        };
}