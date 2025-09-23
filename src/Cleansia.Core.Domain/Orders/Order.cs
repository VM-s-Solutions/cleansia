using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

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

    public string CustomerAddressId { get; private set; }
    public Address? CustomerAddress { get; private set; }

    [Required]
    [MaxLength(50)]
    public string DisplayOrderNumber { get; private set; }

    public int Rooms { get; private set; }

    public int Bathrooms { get; private set; }

    [Required]
    public DateTime CleaningDateTime { get; private set; }

    public PaymentType PaymentType { get; private set; }

    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;

    [Required]
    public decimal TotalPrice { get; private set; }

    [Required]
    public int EstimatedTime { get; private set; }

    [MaxLength(50)]
    public string ConfirmationCode { get; private set; } = OrderExtensions.GenerateConfirmationCode();

    public string StripeSessionId { get; private set; }

    public string? Notes { get; private set; }

    public string? SpecialInstructions { get; private set; }

    public string? AccessInstructions { get; private set; }

    public string? SelectedPackageId { get; private set; }
    public Package? SelectedPackage { get; private set; }

    public string CurrencyId { get; private set; }
    public Currency Currency { get; private set; }

    public string? UserId { get; private set; }
    public User? User { get; private set; }

    public string? EmployeeId { get; private set; }
    public Employee? Employee { get; private set; }


    public IDictionary<string, bool> _extras = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, bool> Extras => _extras.AsReadOnly();

    private ICollection<OrderService> _selectedServices = [];
    public IReadOnlyCollection<OrderService> SelectedServices => _selectedServices.ToList().AsReadOnly();

    private ICollection<OrderStatusTrack> _orderStatusHistory = [];
    public IReadOnlyCollection<OrderStatusTrack> OrderStatusHistory => _orderStatusHistory.ToList().AsReadOnly();

    private ICollection<OrderEmployee> _assignedEmployees = [];
    public IReadOnlyCollection<OrderEmployee> AssignedEmployees => _assignedEmployees.ToList().AsReadOnly();

    public static Order Create(string customerName, string customerEmail, string customerPhone,
        Address customerAddress, string? selectedPackageId, int rooms, int bathrooms,
        Dictionary<string, bool> extras, DateTime cleaningDateTime, PaymentType paymentType,
        decimal totalPrice, string currencyId, PaymentStatus paymentStatus) => new()
        {
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            CustomerAddress = customerAddress,
            SelectedPackageId = selectedPackageId,
            Rooms = rooms,
            Bathrooms = bathrooms,
            _extras = extras,
            CleaningDateTime = cleaningDateTime,
            PaymentType = paymentType,
            TotalPrice = totalPrice,
            CurrencyId = currencyId,
            PaymentStatus = paymentStatus
        };

    public Order AddSelectedServices(IEnumerable<OrderService> selectedServices)
    {
        _selectedServices = selectedServices.ToList();

        return this;
    }

    public Order AddOrderStatus(OrderStatusTrack orderStatusTrack)
    {
        _orderStatusHistory.Add(orderStatusTrack);

        return this;
    }

    public Order UpdatePaymentStatus(PaymentStatus paymentStatus)
    {
        PaymentStatus = paymentStatus;

        return this;
    }

    public Order UpdatePhone(string phone)
    {
        CustomerPhone = phone;

        return this;
    }

    public Order UpdateEstimatedTime(int estimatedTime)
    {
        EstimatedTime = estimatedTime;

        return this;
    }
}