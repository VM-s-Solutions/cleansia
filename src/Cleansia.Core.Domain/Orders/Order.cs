using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    // Cash-collection audit: a card order flips to Paid via the Stripe webhook, but an order the cleaner
    // settled on site stays Pending until they physically collect the money and mark it
    // (MarkCashCollected). These record WHO collected and WHEN, so completion of an unpaid order can be
    // blocked with a trail — and they are the source of the derived tender below.
    public DateTime? CashCollectedAt { get; private set; }

    public string? CollectedByEmployeeId { get; private set; }

    [NotMapped]
    public bool SettledInCash => CashCollectedAt is not null;

    /// <summary>
    /// The tender the customer ACTUALLY paid with, as opposed to <see cref="PaymentType"/>, which stays
    /// the booking contract (a card booking whose Stripe webhook never arrived keeps
    /// <see cref="PaymentType.Card"/> so the refund path still finds its charge surface). A card booking
    /// the cleaner collected in cash is legally a cash sale, so the fiscal registration, the receipt's
    /// payment label and revenue-by-tender reporting must read this — not the booked type.
    /// </summary>
    [NotMapped]
    public PaymentType ActualPaymentType => SettledInCash ? PaymentType.Cash : PaymentType;

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

    /// <summary>
    /// The longest span a single booking is ASSUMED to occupy. It exists only as a query floor: the
    /// overlap scan starts at <c>windowStart - MaxOrderSpanHours</c> instead of at the beginning of
    /// time, because the predicate's lower side is a per-row interval computation and only the upper
    /// bound is sargable. It may only ever be too generous — too generous costs a wider range scan of a
    /// near-empty band, too tight makes an overlapping order invisible ON THE BOOKING WRITE GATE, which
    /// is a double booking.
    ///
    /// <para><b>Assumed, not enforced.</b> <see cref="EstimatedTime"/> is an unbounded sum over the
    /// selected services and packages: nothing caps a service's estimate (the catalog validators only
    /// require it be non-negative) and nothing caps how many items one order may select. The shipped
    /// catalog's maximum producible span is 3495 min (58.25 h) — every service plus every package on
    /// one order — so 7 days holds it with ~3x headroom, but the bound is a policy number, not an
    /// invariant. Falsify it in one line: <c>SELECT MAX("EstimatedTime") FROM "Orders"</c> must stay
    /// well under <c>MaxOrderSpanHours * 60</c>. When it stops holding, the durable fix is a validated
    /// span cap on the order write path or a persisted appointment-end column (ADR-0039 A15) — NOT a
    /// bigger number here.</para>
    /// </summary>
    public const int MaxOrderSpanHours = 168;

    public int AvailableSpots => MaxEmployees - _assignedEmployees.Count;
    public bool HasAvailableSpots => AvailableSpots > 0;

    [MaxLength(50)]
    public string ConfirmationCode { get; private set; } = OrderExtensions.GenerateConfirmationCode();

    public string StripeSessionId { get; private set; } = string.Empty;

    public string? StripePaymentIntentId { get; private set; }

    // A card order is charged on exactly one Stripe surface: the web flow on a Checkout Session, the
    // mobile (PaymentSheet) flow on a PaymentIntent (T-0347 suppresses the Session for mobile, so its
    // StripeSessionId is empty). A refund is possible when either surface is present.
    [NotMapped]
    public bool HasRefundableChargeSurface =>
        !string.IsNullOrEmpty(StripeSessionId) || !string.IsNullOrEmpty(StripePaymentIntentId);

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
    /// Who initiated the cancellation. Persisted as the legacy lowercase string
    /// ("customer"/"cleaner"/"admin"/"system") via a value converter so already-cancelled
    /// rows remain readable. Null while active.
    /// </summary>
    public CancelledBy? CancelledBy { get; private set; }

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
    /// Customer-requested cleaner — what the customer ASKED for. Whether the platform could act on it
    /// is <see cref="PreferredHoldUntilUtc"/>, a separate column with a separate lifetime, so that
    /// "we stored your preference and could not act on it" stays expressible. There is no matching
    /// algorithm and no score: dispatch is first-come-first-served off a pull board, and the only thing
    /// this field buys is a bounded head start on the order's first seat (ADR-0036).
    /// Nulled by <see cref="AnonymizeCustomerData"/>. Never exposed on a partner-facing DTO — the
    /// chosen cleaner is told they were chosen, and nobody is ever told they were passed over.
    /// </summary>
    [MaxLength(26)]
    public string? PreferredEmployeeId { get; private set; }

    /// <summary>
    /// ADR-0036 — until this instant the order is offered to <see cref="PreferredEmployeeId"/> alone.
    /// An absolute deadline, never a duration: set once at creation through
    /// <see cref="GrantPreferredHold"/>, never recomputed, so tuning the policy cannot re-time orders
    /// that already exist, and expiry is <c>now &gt;= deadline</c> in a WHERE clause with no sweep, no
    /// timer and no status transition. The failure mode of a job-driven expiry is <i>an order stuck
    /// held</i>; the failure mode of a clock comparison is that the clock is wrong.
    ///
    /// <para><c>null</c> = no hold, ever — which is what makes every row without one unaffected by
    /// construction, with no backfill.</para>
    ///
    /// <para>Predicates key on the DEADLINE, never on <see cref="PreferredEmployeeId"/>: keying on the
    /// beneficiary would switch behaviour on retroactively for every order that ever carried a
    /// preference. And the reverse pair — a deadline with nobody able to act on it — is closed at both
    /// ends: unwritable here, and treated as no hold by the visibility rule's null-beneficiary disjunct.</para>
    /// </summary>
    public DateTime? PreferredHoldUntilUtc { get; private set; }

    /// <summary>
    /// ADR-0045 D5.3 — how many preferred-cleaner reservations this order has ever carried. The
    /// booking's own choice is round 1 and the customer's single re-offer is round 2;
    /// <see cref="GrantPreferredHold"/> is the sole writer and increments once per grant, which is what
    /// makes <c>Round &lt; max</c> admit exactly two.
    ///
    /// <para>A COUNT is required because the window formula does not terminate: it recomputes off the
    /// current lead time, so each round is ~90% of the previous one and reaching the eight-hour floor
    /// from a seven-day booking takes about thirty rounds. A lead-time floor is not a loop bound.</para>
    ///
    /// <para>It counts ROUNDS, not declines, and it is per-order — it can never answer a question about
    /// a cleaner (ADR-0045 D13).</para>
    /// </summary>
    public int PreferredOfferRound { get; private set; }

    /// <summary>
    /// ADR-0045 D6 — when the customer was told this reservation ended without a confirmation. The
    /// receipt exists so the 5-minute lapse sweep does not prompt twice, and it is a separate column
    /// rather than a cleared hold pair because <c>NewJobsDigestService.ApplyFreshness</c> reads that
    /// pair to decide a lapsed order is NEW AGAIN to every other cleaner — nulling it would drop the
    /// order out of the notification channel permanently. Precedent:
    /// <see cref="RecurringReminderSentAt"/>.
    ///
    /// <para>Per RESERVATION, not per order: <see cref="GrantPreferredHold"/> clears it, so a second
    /// round's lapse is announced too.</para>
    /// </summary>
    public DateTime? PreferredOfferLapseNotifiedAt { get; private set; }

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

    /// <summary>
    /// Timestamp when the "your cleaning starts in about an hour" push was dispatched for this one-off
    /// order. Null until the pre-cleaning sweep fires; never cleared. Disjoint from
    /// <see cref="RecurringReminderSentAt"/> in both population and meaning — that one is the 24h-ahead
    /// CONFIRM prompt on a recurring occurrence, this one is the T-1h notice the booking-confirmation
    /// screen promises.
    /// </summary>
    public DateTime? PreCleaningReminderSentAt { get; private set; }

    public IDictionary<string, bool> _extras = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, bool> Extras => _extras.AsReadOnly();

    private ICollection<OrderService> _selectedServices = [];
    public IReadOnlyCollection<OrderService> SelectedServices => _selectedServices.ToList().AsReadOnly();

    private ICollection<OrderPackage> _selectedPackages = [];
    public IReadOnlyCollection<OrderPackage> SelectedPackages => _selectedPackages.ToList().AsReadOnly();

    private ICollection<OrderStatusTrack> _orderStatusHistory = [];
    public IReadOnlyCollection<OrderStatusTrack> OrderStatusHistory => _orderStatusHistory.ToList().AsReadOnly();

    /// <summary>
    /// Persisted denormalization of the latest <see cref="OrderStatusHistory"/> row, written ONLY by
    /// <see cref="AddOrderStatus"/> (the single append seam); the history stays the authoritative audit
    /// trail. CreatedOn is the primary (human-meaningful) sort and Sequence the deterministic tiebreaker
    /// for same-tick transitions the ULID id cannot provide.
    ///
    /// <para><b>Non-nullable, and there is no history fallback.</b> A brand-new aggregate is
    /// <see cref="OrderStatus.New"/> — which is what it is — and the single production creation path
    /// appends the <c>New</c> track before the row is staged, so no persisted order can lack a status.
    /// Making the column NOT NULL is what lets every filter drop the <c>!= null</c> conjunct that was
    /// pushing the status term inside an OR and stopping PostgreSQL from using the leading column of
    /// IX_Orders_CurrentStatus_CleaningDateTime.</para>
    /// </summary>
    public OrderStatus CurrentStatus { get; private set; }

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
        string? recurringTemplateId = null,
        // Free-text note the customer typed at booking time ("gate code 1234",
        // "cat is friendly"). Read-only afterwards — the partner apps render it
        // on the job card. Whitespace-only collapses to null so the partner UI
        // doesn't render an empty "notes from the customer" section.
        string? specialInstructions = null,
        // How to get in ("key under the mat", "gate code 4455"). Separate from
        // specialInstructions because the two answer different questions, and
        // because keeping them apart leaves room to release them on different
        // terms later. That room is now used: OrderPiiRedaction blanks this —
        // with the address, the phone and the confirmation code — for a cleaner
        // the order does not belong to, so a browsing cleaner reads the job's
        // scope and not the customer's door code. An ENTITLED reader (the
        // customer, an assigned cleaner, an admin) still gets it at any status.
        string? accessInstructions = null) => new()
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
            SpecialInstructions = string.IsNullOrWhiteSpace(specialInstructions) ? null : specialInstructions.Trim(),
            AccessInstructions = string.IsNullOrWhiteSpace(accessInstructions) ? null : accessInstructions.Trim(),
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

    /// <summary>
    /// Stamp the instant the pre-cleaning reminder was dispatched. First stamp wins, so a re-entrant
    /// sweep cannot move it forward and re-open the order to a second reminder.
    /// </summary>
    public Order MarkPreCleaningReminderSent(DateTime sentAtUtc)
    {
        PreCleaningReminderSentAt ??= sentAtUtc;
        return this;
    }

    public Order AddSelectedPackages(IEnumerable<OrderPackage> selectedPackages)
    {
        _selectedPackages = selectedPackages.ToList();
        return this;
    }

    /// <summary>
    /// ADR-0036 — the ONLY writer of the (<see cref="PreferredEmployeeId"/>,
    /// <see cref="PreferredHoldUntilUtc"/>) pair, and ADR-0045 D5.3's only writer of
    /// <see cref="PreferredOfferRound"/>. Writing both halves of the pair together is what makes a
    /// deadline with no beneficiary — an order nobody may take and no actor may release — unreachable:
    /// a safety property defended by a reviewer remembering to null the companion field is not a
    /// safety property.
    ///
    /// <para>ADR-0045 D5.1 widened this from "set once, at creation" to re-callable, and the structural
    /// invariants it keeps are below. The one that can actually fail is <b>no live reservation for
    /// someone else</b>, and it is phrased on the HOLD rather than on the preference column:
    /// <see cref="Create"/> writes <see cref="PreferredEmployeeId"/> independently of any hold, so an
    /// invariant on the preference would refuse re-offers that never held anything and permit ones that
    /// do.</para>
    ///
    /// <para><paramref name="maxRounds"/> is a platform policy number the application layer owns
    /// (<c>BookingPolicy.MaxPreferredOfferRounds</c>) — this entity stays policy-ignorant, the same way
    /// <see cref="CalculateRequiredEmployees"/> takes the spare-seat count.</para>
    /// </summary>
    public Order GrantPreferredHold(
        string preferredEmployeeId, DateTime untilUtc, DateTime nowUtc, int maxRounds)
    {
        if (string.IsNullOrWhiteSpace(preferredEmployeeId))
        {
            throw new ArgumentException(
                "A preferred hold requires a beneficiary who can act on it.", nameof(preferredEmployeeId));
        }

        if (untilUtc <= nowUtc)
        {
            throw new ArgumentException(
                "A preferred hold must end in the future; a zero-length reservation burns a round and "
                + "withholds nothing.",
                nameof(untilUtc));
        }

        if (_assignedEmployees.Count > 0)
        {
            throw new InvalidOperationException(
                "An order that already has a cleaner has no reservation to grant.");
        }

        if (PreferredHoldUntilUtc > nowUtc && PreferredEmployeeId != preferredEmployeeId)
        {
            throw new InvalidOperationException(
                "A live reservation belongs to another cleaner who was told the job was theirs.");
        }

        if (PreferredOfferRound >= maxRounds)
        {
            throw new InvalidOperationException(
                $"This order has already carried {PreferredOfferRound} preferred-cleaner reservations.");
        }

        PreferredEmployeeId = preferredEmployeeId;
        PreferredHoldUntilUtc = untilUtc;
        PreferredOfferRound++;
        PreferredOfferLapseNotifiedAt = null;
        return this;
    }

    /// <summary>
    /// ADR-0045 D6.4 / D1.1 — the cleaner passes: the reservation ends now. One write, and the
    /// beneficiary stays on the row so the customer's re-offer can refuse the same person without
    /// anybody being told who it was. Never moves the deadline forward, so a second decline racing the
    /// lapse sweep cannot re-open a reservation the clock already closed.
    /// </summary>
    public Order EndPreferredHold(DateTime endedAtUtc)
    {
        if (PreferredHoldUntilUtc > endedAtUtc)
        {
            PreferredHoldUntilUtc = endedAtUtc;
        }

        return this;
    }

    /// <summary>
    /// Stamp the instant the customer was told this reservation closed. First stamp wins, so a
    /// re-entrant sweep cannot prompt twice; <see cref="GrantPreferredHold"/> clears it because the
    /// receipt belongs to the reservation, not to the order.
    /// </summary>
    public Order MarkPreferredOfferLapseNotified(DateTime notifiedAtUtc)
    {
        PreferredOfferLapseNotifiedAt ??= notifiedAtUtc;
        return this;
    }

    /// <summary>Drops both halves together — anonymization, and any future path that returns the order to the board.</summary>
    public Order ClearPreferredHold()
    {
        PreferredEmployeeId = null;
        PreferredHoldUntilUtc = null;
        return this;
    }

    public Order AddOrderStatus(OrderStatusTrack orderStatusTrack)
    {
        orderStatusTrack.AssignSequence(
            _orderStatusHistory.Count == 0 ? 0 : _orderStatusHistory.Max(s => s.Sequence) + 1);
        _orderStatusHistory.Add(orderStatusTrack);
        // Recompute (rather than blindly take the appended status) so the persisted value is by
        // construction the same rule as the audit trail — a track appended with a backdated
        // CreatedOn (seeds, tests) correctly does NOT become current.
        CurrentStatus = _orderStatusHistory
            .OrderByDescending(s => s.CreatedOn)
            .ThenByDescending(s => s.Sequence)
            .First().Status;

        return this;
    }

    public Order UpdatePaymentStatus(PaymentStatus paymentStatus)
    {
        PaymentStatus = paymentStatus;

        return this;
    }

    // The cleaner collected the cash owed for this order. Flips it to Paid (the same terminal state a
    // Stripe-charged card order reaches) and stamps the audit trail. Idempotency and the InProgress gate
    // are enforced in MarkCashCollected.Validator, and the Stripe reconciliation that keeps a card order
    // from being charged twice is in its handler, so this stays a pure happy-path mutator.
    public Order MarkCashCollected(string employeeId)
    {
        PaymentStatus = PaymentStatus.Paid;
        CashCollectedAt = DateTime.UtcNow;
        CollectedByEmployeeId = employeeId;

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

    /// <summary>
    /// Removes the given employee's assignment, freeing a spot. No-op if the employee is not
    /// currently assigned. Used by the admin reassign flow to replace a cleaner; the spot-availability
    /// guard for the replacement add stays in the application layer so it surfaces as a business error.
    /// </summary>
    public Order UnassignEmployee(string employeeId)
    {
        var assignment = _assignedEmployees.FirstOrDefault(oe => oe.EmployeeId == employeeId);
        if (assignment is not null)
        {
            _assignedEmployees.Remove(assignment);
        }

        return this;
    }

    /// <summary>
    /// Derives the crew the booked work needs and the seat cap that follows from it.
    /// <paramref name="spareSeats"/> is a platform policy number the application layer owns
    /// (<c>BookingPolicy.SpareSeatsPerOrder</c>) — this entity stays policy-ignorant, the same way
    /// <see cref="Cancel"/> takes an already-computed fee rate.
    /// </summary>
    public Order CalculateRequiredEmployees(int spareSeats)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spareSeats);

        RequiredEmployees = EstimatedTime <= 0
            ? 1
            : (int)Math.Ceiling((double)EstimatedTime / StandardWorkUnitMinutes);
        MaxEmployees = RequiredEmployees + spareSeats;

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
        CancelledBy cancelledBy,
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
        ClearPreferredHold();
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
