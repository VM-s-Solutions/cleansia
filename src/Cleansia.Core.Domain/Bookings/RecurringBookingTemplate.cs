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
    /// The cleaning date of the most recently materialized occurrence. The
    /// materializer reads this to know what to skip and where to resume.
    /// Null on a brand-new template — first run materializes from StartsOn.
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
        DateTime? endsOn = null)
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
        };

    /// <summary>
    /// Apply an in-place update to the template's schedule + targets. Preserves
    /// the entity's <see cref="BaseEntity.Id"/> so any client holding a reference
    /// (mobile/web caching by id) survives the edit.
    ///
    /// <see cref="LastMaterializedFor"/> is intentionally cleared because the
    /// new schedule may put the next occurrence earlier than the previously
    /// materialized one — the materializer must re-evaluate from scratch.
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
