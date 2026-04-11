using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Receipts;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Cleansia.Core.Domain.Orders;

public class Order : Auditable, ITenantEntity
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
    public string DisplayOrderNumber { get; private set; } = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

    public int Rooms { get; private set; }

    public int Bathrooms { get; private set; }

    [Required]
    public DateTime CleaningDateTime { get; private set; }

    public PaymentType PaymentType { get; private set; }

    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.Pending;

    [Required]
    public decimal TotalPrice { get; private set; }

    /// <summary>
    /// Net amount (price excluding VAT). Equal to <see cref="TotalPrice"/> when the company is not a VAT payer.
    /// </summary>
    public decimal NetAmount { get; private set; }

    /// <summary>
    /// VAT portion of <see cref="TotalPrice"/>. Zero when the company is not a VAT payer.
    /// </summary>
    public decimal VatAmount { get; private set; }

    /// <summary>
    /// VAT rate applied at order creation time (e.g., 21.00m for 21%).
    /// Null when no VAT was calculated (company is not a VAT payer).
    /// Stored so historical orders retain their original rate when the country rate changes.
    /// </summary>
    public decimal? AppliedVatRate { get; private set; }

    [Required]
    public int EstimatedTime { get; private set; }

    public int? ActualCompletionTime { get; private set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; private set; }

    public bool EmployeePayCalculated { get; private set; } = false;

    public decimal? TravelDistance { get; private set; }

    public int RequiredEmployees { get; private set; } = 1;

    public int MaxEmployees { get; private set; } = 1;

    private const int StandardWorkUnitMinutes = 120;

    public int AvailableSpots => MaxEmployees - _assignedEmployees.Count;
    public bool HasAvailableSpots => AvailableSpots > 0;
    public bool IsFullyAssigned => _assignedEmployees.Count >= RequiredEmployees;

    [MaxLength(50)]
    public string ConfirmationCode { get; private set; } = OrderExtensions.GenerateConfirmationCode();

    public string StripeSessionId { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public string? SpecialInstructions { get; private set; }

    public string? AccessInstructions { get; private set; }

    public string CurrencyId { get; private set; }
    public Currency Currency { get; private set; }

    public string? UserId { get; private set; }
    public User? User { get; private set; }

    public string? ReceiptId { get; private set; }
    public OrderReceipt? Receipt { get; private set; }

    public IDictionary<string, bool> _extras = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, bool> Extras => _extras.AsReadOnly();

    private ICollection<OrderService> _selectedServices = [];
    public IReadOnlyCollection<OrderService> SelectedServices => _selectedServices.ToList().AsReadOnly();

    private ICollection<OrderPackage> _selectedPackages = [];
    public IReadOnlyCollection<OrderPackage> SelectedPackages => _selectedPackages.ToList().AsReadOnly();

    private ICollection<OrderStatusTrack> _orderStatusHistory = [];
    public IReadOnlyCollection<OrderStatusTrack> OrderStatusHistory => _orderStatusHistory.ToList().AsReadOnly();

    private ICollection<OrderEmployee> _assignedEmployees = [];
    public IReadOnlyCollection<OrderEmployee> AssignedEmployees => _assignedEmployees.ToList().AsReadOnly();

    private ICollection<OrderPhoto> _photos = [];
    public IReadOnlyCollection<OrderPhoto> Photos => _photos.ToList().AsReadOnly();

    private ICollection<OrderNote> _notes = [];
    public IReadOnlyCollection<OrderNote> OrderNotes => _notes.ToList().AsReadOnly();

    private ICollection<OrderIssue> _issues = [];
    public IReadOnlyCollection<OrderIssue> OrderIssues => _issues.ToList().AsReadOnly();

    private ICollection<OrderReview> _reviews = [];
    public IReadOnlyCollection<OrderReview> Reviews => _reviews.ToList().AsReadOnly();

    public static Order Create(string customerName, string customerEmail, string customerPhone,
        Address customerAddress, int rooms, int bathrooms,
        Dictionary<string, bool> extras, DateTime cleaningDateTime, PaymentType paymentType,
        decimal totalPrice, string currencyId, PaymentStatus paymentStatus) => new()
        {
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            CustomerAddress = customerAddress,
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

    public Order AddSelectedPackages(IEnumerable<OrderPackage> selectedPackages)
    {
        _selectedPackages = selectedPackages.ToList();
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

    public Order SetCurrency(Currency currency)
    {
        Currency = currency;
        CurrencyId = currency.Id;
        return this;
    }

    public Order MarkEmployeePayCalculated()
    {
        EmployeePayCalculated = true;
        return this;
    }

    /// <summary>
    /// Persists the VAT breakdown computed at order creation time.
    /// When the company is not a VAT payer, pass net=TotalPrice, vat=0, rate=null.
    /// </summary>
    public Order SetVatBreakdown(decimal netAmount, decimal vatAmount, decimal? appliedRate)
    {
        if (netAmount < 0) throw new ArgumentException("Net amount cannot be negative", nameof(netAmount));
        if (vatAmount < 0) throw new ArgumentException("VAT amount cannot be negative", nameof(vatAmount));

        NetAmount = netAmount;
        VatAmount = vatAmount;
        AppliedVatRate = appliedRate;
        return this;
    }

    public Order SetTravelDistance(decimal distance)
    {
        if (distance < 0)
        {
            throw new ArgumentException("Travel distance cannot be negative", nameof(distance));
        }

        TravelDistance = distance;
        return this;
    }


    public Order AddAssignedEmployee(OrderEmployee orderEmployee)
    {
        if (!HasAvailableSpots)
        {
            throw new InvalidOperationException("No available spots for this order");
        }

        _assignedEmployees.Add(orderEmployee);
        return this;
    }

    public Order CalculateRequiredEmployees()
    {
        if (EstimatedTime <= 0)
        {
            RequiredEmployees = 1;
            MaxEmployees = 1;
            return this;
        }

        RequiredEmployees = (int)Math.Ceiling((double)EstimatedTime / StandardWorkUnitMinutes);
        MaxEmployees = RequiredEmployees + 1;

        return this;
    }

    public Order SetMaxEmployees(int maxEmployees)
    {
        if (maxEmployees < RequiredEmployees)
        {
            throw new ArgumentException("Max employees cannot be less than required employees", nameof(maxEmployees));
        }

        MaxEmployees = maxEmployees;
        return this;
    }

    public Order StartOrder()
    {
        return this;
    }

    public Order AddNote(OrderNote note)
    {
        _notes.Add(note);
        return this;
    }

    public Order AddIssue(OrderIssue issue)
    {
        _issues.Add(issue);
        return this;
    }

    public Order AddReview(OrderReview review)
    {
        _reviews.Add(review);
        return this;
    }

    public Order CompleteOrder(int actualCompletionTime, string? completionNotes = null)
    {
        if (actualCompletionTime <= 0)
        {
            throw new ArgumentException("Actual completion time must be greater than zero", nameof(actualCompletionTime));
        }

        if (completionNotes is { Length: > 1000 })
        {
            throw new ArgumentException("Completion notes must not exceed 1000 characters", nameof(completionNotes));
        }

        ActualCompletionTime = actualCompletionTime;
        CompletionNotes = completionNotes;
        return this;
    }

    public Order AnonymizeCustomerData()
    {
        CustomerName = "[DELETED]";
        CustomerEmail = "[DELETED]";
        CustomerPhone = "[DELETED]";
        return this;
    }
}
