using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Orders;

public class Order : Auditable
{
    [MaxLength(100)]
    public string CustomerName { get; private set; }

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string CustomerEmail { get; private set; }

    [MaxLength(50)]
    public string CustomerPhone { get; private set; }

    [MaxLength(200)]
    public string CustomerAddress { get; private set; }

    [Required]
    [MaxLength(50)]
    public string DisplayOrderNumber { get; private set; }

    public int Rooms { get; private set; }

    public int Bathrooms { get; private set; }

    public Dictionary<string, bool> Extras { get; private set; } = new();

    [Required]
    public DateTime CleaningDate { get; private set; }

    public PaymentType PaymentType { get; private set; }

    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;

    [Required]
    public decimal TotalPrice { get; private set; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    [MaxLength(50)]
    public string ConfirmationCode { get; private set; }

    public string StripeSessionId { get; private set; }

    public string? SelectedPackageId { get; private set; }
    public virtual Package SelectedPackage { get; private set; }

    public string CurrencyId { get; private set; }
    public Currency Currency { get; private set; }

    private ICollection<Service> _selectedServices = [];
    public IReadOnlyCollection<Service> SelectedServices => _selectedServices.ToList().AsReadOnly();
}