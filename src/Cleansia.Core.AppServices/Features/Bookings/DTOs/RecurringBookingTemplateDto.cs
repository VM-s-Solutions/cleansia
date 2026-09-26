using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Bookings.DTOs;

/// <summary>
/// Read DTO for a user's recurring booking template — drives the Plus
/// "My recurring cleanings" list. Field shape mirrors what the customer UI
/// needs to render a row (when, what, where, paused/active state).
///
/// Service / package ids are projected as plain string lists; the UI joins
/// them against the catalog cache it already has.
/// </summary>
public record RecurringBookingTemplateDto(
    string Id,
    int Frequency,
    int DayOfWeek,
    string TimeOfDay,
    int Rooms,
    int Bathrooms,
    string SavedAddressId,
    string? AddressLine,
    IReadOnlyList<string> SelectedServiceIds,
    IReadOnlyList<string> SelectedPackageIds,
    int PaymentType,
    DateTime StartsOn,
    DateTime? EndsOn,
    DateTime? LastMaterializedFor,
    bool IsActive,
    string? PreferredEmployeeId = null,
    /// <summary>
    /// A cash template whose selection now needs more than one cleaner. It creates no occurrences until
    /// the customer switches it to card or to a one-cleaner selection; nothing switches it for them.
    /// Read from the live catalogue, so an edit to a service's duration can set or clear it.
    /// </summary>
    bool RequiresPaymentMethodChange = false);
