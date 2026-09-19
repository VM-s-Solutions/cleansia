using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// What a <c>customer.recurring.*</c> row records: the schedule before and after the act, as facts only
/// (ADR-0062 D3). Top-level rather than nested because four commands emit it. A create has no
/// <see cref="Before"/>; a delete has no <see cref="After"/> — the template row is hard-deleted, so the
/// row is the last place its final schedule survives. No address text and no preferred cleaner.
/// </summary>
public record RecurringTemplateEvidence(
    RecurringTemplateFacts? Before,
    RecurringTemplateFacts? After) : ICustomerAuditPayload;

public record RecurringTemplateFacts(
    RecurrenceFrequency Frequency,
    System.DayOfWeek Weekday,
    TimeOnly TimeOfDay,
    IReadOnlyList<string> PackageIds,
    IReadOnlyList<string> ServiceIds,
    string SavedAddressId,
    bool IsActive)
{
    public static RecurringTemplateFacts Of(RecurringBookingTemplate template) => new(
        Frequency: template.Frequency,
        Weekday: template.DayOfWeek,
        TimeOfDay: template.TimeOfDay,
        PackageIds: template.SelectedPackageIds.ToList(),
        ServiceIds: template.SelectedServiceIds.ToList(),
        SavedAddressId: template.SavedAddressId,
        IsActive: template.IsActive);
}
