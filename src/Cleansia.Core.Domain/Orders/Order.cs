using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
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

    /// <summary>
    /// When the order was actually marked Completed (UTC). Null while
    /// the order is still open. This is the authoritative completion
    /// timestamp for dashboards / reports / analytics — previously the
    /// system inferred it from `OrderStatusHistory` rows or from
    /// `OrderEmployeePay.CreatedOn`, both of which produced wrong-day
    /// boundaries and disagreed with each other. Mirrors the existing
    /// `CancelledAt` pattern.
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

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

    public string? StripePaymentIntentId { get; private set; }

    public string? Notes { get; private set; }

    public string? SpecialInstructions { get; private set; }

    public string? AccessInstructions { get; private set; }

    public string CurrencyId { get; private set; }
    public Currency Currency { get; private set; }

    public string? UserId { get; private set; }
    public User? User { get; private set; }

    public string? ReceiptId { get; private set; }
    public OrderReceipt? Receipt { get; private set; }

    /// <summary>
    /// When the customer cancelled this order. Null while active.
    /// </summary>
    public DateTime? CancelledAt { get; private set; }

    /// <summary>
    /// Amount actually refunded to the customer on cancellation.
    /// Zero if the full fee applied (100% no-refund charge).
    /// </summary>
    public decimal? CancellationRefundAmount { get; private set; }

    /// <summary>
    /// Fee rate applied at cancellation (0.0 = free, 0.5 = half, 1.0 = full charge).
    /// </summary>
    public decimal? CancellationFeeRate { get; private set; }

    /// <summary>
    /// Who initiated the cancellation — "customer", "cleaner", or "system".
    /// </summary>
    [MaxLength(20)]
    public string? CancelledBy { get; private set; }

    [MaxLength(500)]
    public string? CancellationReason { get; private set; }

    /// <summary>
    /// Loyalty tier discount applied at create-time (CZK amount, not %).
    /// Null when no loyalty discount applied (legacy/anon orders, Bronze tier, or no qualifying account).
    /// </summary>
    public decimal? TierDiscountAmount { get; private set; }

    /// <summary>
    /// Snapshot of the customer's loyalty tier at the moment the booking was placed.
    /// Null for orders booked without an authenticated user, or before loyalty foundation rolled out.
    /// </summary>
    public LoyaltyTier? TierAtPurchase { get; private set; }

    /// <summary>
    /// Promo-code discount applied at create-time (CZK amount, not %).
    /// Null when no promo was applied (no code entered, code invalid, or
    /// tier discount won the best-wins comparison).
    /// </summary>
    public decimal? PromoDiscountAmount { get; private set; }

    /// <summary>
    /// FK to the <see cref="Cleansia.Core.Domain.Loyalty.PromoCode"/> that was
    /// redeemed for this order. Null when no promo applied. Restricted on
    /// delete so we don't lose the audit linkage if the code gets removed.
    /// </summary>
    public string? PromoCodeId { get; private set; }

    /// <summary>
    /// Membership discount applied at create-time (CZK amount, not %). Null
    /// when no membership discount applied (no active membership, or tier/promo
    /// won the best-wins comparison). Mutually exclusive with TierDiscountAmount
    /// and PromoDiscountAmount — only one of the three can be non-null.
    /// </summary>
    public decimal? MembershipDiscountAmount { get; private set; }

    /// <summary>
    /// Snapshot of the <see cref="Cleansia.Core.Domain.Memberships.MembershipPlan"/> id
    /// that produced the discount. Stored even when discount is zero so receipts
    /// can render "Cleansia Plus member" for transparency.
    /// </summary>
    [MaxLength(26)]
    public string? MembershipPlanIdAtPurchase { get; private set; }

    /// <summary>
    /// Customer-requested cleaner. The matching algorithm boosts this employee's
    /// score so they're more likely to be offered the order, but it's not a
    /// guarantee — if they decline or are busy, the order falls back to normal
    /// matching. Not exposed to the cleaner side (avoids "they didn't pick me"
    /// awkwardness). Future Cleansia Plus perk; today the field exists but no
    /// UI sets it.
    /// </summary>
    [MaxLength(26)]
    public string? PreferredEmployeeId { get; private set; }

    /// <summary>
    /// FK back to the <see cref="Bookings.RecurringBookingTemplate"/> that spawned
    /// this order. Null for one-off orders. Set by the materializer; lets the
    /// confirm-recurring flow find the originating template for things like
    /// cancellation cascade decisions and analytics.
    /// </summary>
    [MaxLength(26)]
    public string? RecurringTemplateId { get; private set; }

    /// <summary>
    /// Timestamp when the 24h-ahead "confirm your recurring booking" push was
    /// dispatched for this order. Used by the reminder sweep to avoid sending
    /// the same push twice if the sweep runs multiple times within the 24h
    /// window. Null until the sweep fires; never cleared after that.
    /// </summary>
    public DateTime? RecurringReminderSentAt { get; private set; }

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
        decimal totalPrice, string currencyId, PaymentStatus paymentStatus,
        // Optional: when present, links the order to the booking user so
        // CancelOrder / SubmitReview / ReportIssue can enforce ownership.
        // Empty/null is allowed for the (legacy) anonymous guest checkout
        // path on web — those orders just can't be cancelled by the user.
        string? userId = null,
        // Loyalty: optional snapshot of the tier discount applied at booking
        // time so receipts/order details can render the breakdown later.
        // Null for anon/legacy orders or non-discount tiers.
        decimal? tierDiscountAmount = null,
        LoyaltyTier? tierAtPurchase = null,
        // Promo: optional snapshot of the promo discount applied at booking
        // time. Mutually exclusive with tierDiscountAmount in practice — the
        // CreateOrder handler picks best-wins between tier and promo.
        decimal? promoDiscountAmount = null,
        string? promoCodeId = null,
        // Membership: optional snapshot of the Cleansia Plus discount applied
        // at booking time. Mutually exclusive with tier/promo via best-wins.
        decimal? membershipDiscountAmount = null,
        string? membershipPlanIdAtPurchase = null,
        // Optional customer-requested cleaner. Used as a matching hint;
        // silent fallback to normal matching if unavailable.
        string? preferredEmployeeId = null,
        // Set by the recurring-bookings materializer to link the spawned
        // Pending order back to its template. Null for one-off orders.
        string? recurringTemplateId = null) => new()
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
            PaymentStatus = paymentStatus,
            UserId = string.IsNullOrEmpty(userId) ? null : userId,
            TierDiscountAmount = tierDiscountAmount is > 0 ? tierDiscountAmount : null,
            TierAtPurchase = tierAtPurchase,
            PromoDiscountAmount = promoDiscountAmount is > 0 ? promoDiscountAmount : null,
            PromoCodeId = string.IsNullOrEmpty(promoCodeId) ? null : promoCodeId,
            MembershipDiscountAmount = membershipDiscountAmount is > 0 ? membershipDiscountAmount : null,
            MembershipPlanIdAtPurchase = string.IsNullOrEmpty(membershipPlanIdAtPurchase) ? null : membershipPlanIdAtPurchase,
            PreferredEmployeeId = string.IsNullOrEmpty(preferredEmployeeId) ? null : preferredEmployeeId,
            RecurringTemplateId = string.IsNullOrEmpty(recurringTemplateId) ? null : recurringTemplateId,
        };

    public Order AddSelectedServices(IEnumerable<OrderService> selectedServices)
    {
        _selectedServices = selectedServices.ToList();

        return this;
    }

    /// <summary>
    /// Stamp the timestamp when the 24h-ahead recurring-booking reminder push
    /// was dispatched. Idempotent — calling twice keeps the first stamp;
    /// the sweep filters by <c>RecurringReminderSentAt == null</c> so it
    /// shouldn't reach this method twice anyway.
    /// </summary>
    public Order MarkRecurringReminderSent(DateTime sentAtUtc)
    {
        RecurringReminderSentAt ??= sentAtUtc;
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

    public Order AssignStripePaymentIntentId(string paymentIntentId)
    {
        StripePaymentIntentId = paymentIntentId;
        return this;
    }

    public Order AssignStripeSessionId(string stripeSessionId)
    {
        StripeSessionId = stripeSessionId;
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

    /// <summary>
    /// Mark this order as cancelled and record the refund breakdown.
    /// Fee-rate / refund-amount should be computed by <see cref="Cleansia.Core.AppServices.Features.Orders.BookingPolicy"/>
    /// at the application layer so this entity stays persistence-ignorant.
    /// </summary>
    public Order Cancel(
        DateTime cancelledAtUtc,
        string cancelledBy,
        decimal feeRate,
        decimal refundAmount,
        string? reason)
    {
        CancelledAt = cancelledAtUtc;
        CancelledBy = cancelledBy;
        CancellationFeeRate = feeRate;
        CancellationRefundAmount = refundAmount;
        CancellationReason = reason;
        return this;
    }

    public Order AddNote(OrderNote note)
    {
        _notes.Add(note);
        return this;
    }

    public Order RemoveNote(OrderNote note)
    {
        _notes.Remove(note);
        return this;
    }

    public Order AddIssue(OrderIssue issue)
    {
        _issues.Add(issue);
        return this;
    }

    public Order RemoveIssue(OrderIssue issue)
    {
        _issues.Remove(issue);
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
        // Authoritative completion timestamp. Set inside the domain
        // so it can't drift away from the status mutation that
        // actually marks the order Completed. Dashboards / reports /
        // analytics all read this column directly instead of trying
        // to derive it from OrderStatusHistory / OrderEmployeePay.
        CompletedAt = DateTime.UtcNow;
        return this;
    }

    public Order AnonymizeCustomerData()
    {
        CustomerName = AnonymizationMarker.Value;
        CustomerEmail = AnonymizationMarker.Value;
        CustomerPhone = AnonymizationMarker.Value;
        UserId = null;
        PromoCodeId = null;
        MembershipPlanIdAtPurchase = null;
        PreferredEmployeeId = null;
        RecurringTemplateId = null;
        Notes = null;
        SpecialInstructions = null;
        AccessInstructions = null;
        CompletionNotes = null;
        foreach (var review in Reviews)
        {
            review.Anonymize();
        }
        foreach (var note in _notes)
        {
            note.Anonymize();
        }
        foreach (var issue in _issues)
        {
            issue.Anonymize();
        }
        return this;
    }
}
