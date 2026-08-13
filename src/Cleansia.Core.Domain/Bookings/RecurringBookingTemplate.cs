using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Bookings;

/// <summary>
/// A user's "every other Tuesday at 10am" booking blueprint. The materializer
/// background job reads active templates and creates concrete <see cref="Orders.Order"/>
/// rows N days ahead. Cancellation of one occurrence does not affect future
/// ones — that's a property of the spawned orders being independent rows.
///
/// No UI exists today to create these; the entity is the foundation that
/// Cleansia Plus's "recurring bookings" perk will surface when Plus launches.
/// </summary>
public class RecurringBookingTemplate : Auditable, ITenantEntity
{
    [Required]
    public string UserId { get; private set; } = default!;
    public User User { get; private set; } = default!;

    [Required]
    public RecurrenceFrequency Frequency { get; private set; }

    /// <summary>Day of the week the cleaning happens.</summary>
    public System.DayOfWeek DayOfWeek { get; private set; }

    /// <summary>Time of day the cleaning starts (in the user's local time, UTC stored).</summary>
    public TimeOnly TimeOfDay { get; private set; }

    public int Rooms { get; private set; }
    public int Bathrooms { get; private set; }

    /// <summary>FK to the user's saved address used for spawned orders.</summary>
    [Required]
    public string SavedAddressId { get; private set; } = default!;

    private List<string> _selectedServiceIds = [];
    public IReadOnlyCollection<string> SelectedServiceIds => _selectedServiceIds.AsReadOnly();

    private List<string> _selectedPackageIds = [];
    public IReadOnlyCollection<string> SelectedPackageIds => _selectedPackageIds.AsReadOnly();

    public PaymentType PaymentType { get; private set; }

    /// <summary>
    /// ADR-0036 D8 — the customer's preferred cleaner for every occurrence this template spawns. A
    /// recurring customer is precisely the customer who wants the same cleaner, and until this existed
    /// the materializer had no field to pass. Plain id, no FK, mirroring <c>Order.PreferredEmployeeId</c>.
    /// <para>The column does not guard itself: "this customer has been served by this cleaner" is checked
    /// once, by <c>CreateRecurringBooking</c>'s validator, because it is the one gate that needs the
    /// caller's identity. The materializer re-resolves everything that can LAPSE per occurrence and
    /// DEGRADES on failure — it spawns the order with no hold rather than dropping a customer's cleaning.
    /// Reject where someone can react; degrade where nobody can.</para>
    /// </summary>
    [MaxLength(26)]
    public string? PreferredEmployeeId { get; private set; }

    /// <summary>First date the template starts spawning orders (UTC).</summary>
    public DateTime StartsOn { get; private set; }

    /// <summary>Optional end date (UTC). Null = indefinite recurrence.</summary>
    public DateTime? EndsOn { get; private set; }

    /// <summary>
    /// Soft-delete flag. Set to false by user "pause" or admin action; the
    /// materializer skips inactive templates without removing the row, so the
    /// user can resume later with the same configuration.
    /// </summary>
    [Required]
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// The cleaning date of the last occurrence the materializer evaluated — a
    /// resume pointer, not a duplicate guard (see <see cref="UpdateSchedule"/>,
    /// which clears it). Null on a brand-new template and after every edit;
    /// the materializer then derives from StartsOn.
    /// </summary>
    public DateTime? LastMaterializedFor { get; private set; }

    private RecurringBookingTemplate() { }

    public static RecurringBookingTemplate Create(
        string userId,
        RecurrenceFrequency frequency,
        System.DayOfWeek dayOfWeek,
        TimeOnly timeOfDay,
        int rooms,
        int bathrooms,
        string savedAddressId,
        IEnumerable<string> selectedServiceIds,
        IEnumerable<string> selectedPackageIds,
        PaymentType paymentType,
        DateTime startsOn,
        DateTime? endsOn = null,
        string? preferredEmployeeId = null)
        => new()
        {
            UserId = userId,
            Frequency = frequency,
            DayOfWeek = dayOfWeek,
            TimeOfDay = timeOfDay,
            Rooms = rooms,
            Bathrooms = bathrooms,
            SavedAddressId = savedAddressId,
            _selectedServiceIds = selectedServiceIds.ToList(),
            _selectedPackageIds = selectedPackageIds.ToList(),
            PaymentType = paymentType,
            StartsOn = startsOn,
            EndsOn = endsOn,
            PreferredEmployeeId = string.IsNullOrEmpty(preferredEmployeeId) ? null : preferredEmployeeId,
        };

    /// <summary>
    /// In-place schedule update. Preserves the id so a client caching by id survives the edit.
    ///
    /// <para><b>The materialisation watermark is cleared deliberately</b> — a new schedule may put the
    /// next occurrence earlier than the last materialised one. <b>Do not stop clearing it to prevent
    /// duplicates</b>: the marker is a resume pointer, and the duplicate guard is the materialiser's
    /// check for an order already spawned at that instant.
    /// → /flows/booking-and-pricing#recurring-bookings</para>
    /// </summary>
    public RecurringBookingTemplate UpdateSchedule(
        RecurrenceFrequency frequency,
        System.DayOfWeek dayOfWeek,
        TimeOnly timeOfDay,
        int rooms,
        int bathrooms,
        string savedAddressId,
        IEnumerable<string> selectedServiceIds,
        IEnumerable<string> selectedPackageIds,
        PaymentType paymentType,
        DateTime startsOn,
        DateTime? endsOn)
    {
        Frequency = frequency;
        DayOfWeek = dayOfWeek;
        TimeOfDay = timeOfDay;
        Rooms = rooms;
        Bathrooms = bathrooms;
        SavedAddressId = savedAddressId;
        _selectedServiceIds = selectedServiceIds.ToList();
        _selectedPackageIds = selectedPackageIds.ToList();
        PaymentType = paymentType;
        StartsOn = startsOn;
        EndsOn = endsOn;
        LastMaterializedFor = null;
        return this;
    }

    public RecurringBookingTemplate Pause()
    {
        IsActive = false;
        return this;
    }

    public RecurringBookingTemplate Resume()
    {
        IsActive = true;
        return this;
    }

    public RecurringBookingTemplate MarkMaterializedFor(DateTime occurrenceUtc)
    {
        LastMaterializedFor = occurrenceUtc;
        return this;
    }
}
