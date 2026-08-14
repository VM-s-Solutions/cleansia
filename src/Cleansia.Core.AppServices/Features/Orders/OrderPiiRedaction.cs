#nullable enable
using Cleansia.Core.AppServices.Features.Orders.DTOs;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// What a cleaner who has NOT taken this job and is not its customer may read about it.
///
/// <para><b>One rule, two shapes, in one file on purpose</b> — when the list and the detail lived apart,
/// the detail answered with everything the list had just withheld. A field added to either DTO is
/// covered here or it is not, and <c>OrderRedactionSurfaceTests</c> fails the build until somebody says
/// which. → /flows/execution-and-completion</para>
/// </summary>
public static class OrderPiiRedaction
{
    public static OrderListItem RedactForBrowsingCleaner(this OrderListItem item) =>
        item with
        {
            CustomerName = string.Empty,
            CustomerEmail = string.Empty,
            CustomerPhone = string.Empty,
            CustomerAddress = string.Empty,
            ConfirmationCode = string.Empty,
            CustomerAddressLatitude = null,
            CustomerAddressLongitude = null,
        };

    /// <summary>
    /// Withhold the entry instructions from an admin read. They are free text of the form "key under
    /// the mat" — the one field on this DTO that is a physical key to somebody's home — and until
    /// 2026-08-14 every admin who opened an order got them, with no reveal step and therefore no record
    /// of who looked. An admin who needs them asks for them: <c>RevealOrderAccessInstructions</c> is a
    /// Command precisely so the audit engine writes a row.
    ///
    /// <para><c>HasAccessInstructions</c> stays true so the admin UI can offer the reveal rather than
    /// showing an empty panel for an order that has none.</para>
    /// </summary>
    public static OrderItem WithholdAccessInstructions(this OrderItem item) =>
        item with { AccessInstructions = null };

    public static OrderItem RedactForBrowsingCleaner(this OrderItem item) =>
        item with
        {
            CustomerName = string.Empty,
            CustomerEmail = string.Empty,
            CustomerPhone = string.Empty,
            Address = null,
            ConfirmationCode = string.Empty,
            Notes = null,
            SpecialInstructions = null,
            AccessInstructions = null,
            HasAccessInstructions = null,
            CompletionNotes = null,
            RecurringTemplateId = null,
            ReceiptNumber = null,
            AssignedEmployees = item.AssignedEmployees.Select(WithoutPersonalContact).ToList(),
            OrderNotes = [],
            OrderIssues = [],
            Review = null,
            ExpressWaiverForfeitedOnCancel = null,
            PreferredOffer = null,
        };

    /// <summary>
    /// The same disclosure the customer gets about the crew (<c>MapToAssignedEmployeeDto</c>): a given
    /// name and no phone number. <c>EmployeeId</c> stays — the partner apps decide "am I on this job?"
    /// by looking for their own id in this list, so removing it would answer that question wrong.
    /// </summary>
    private static AssignedEmployeeDto WithoutPersonalContact(AssignedEmployeeDto employee) =>
        employee with
        {
            FullName = GivenNameOnly(employee.FullName),
            PhoneNumber = null,
        };

    private static string GivenNameOnly(string fullName)
    {
        var firstSpace = fullName.IndexOf(' ');
        return firstSpace < 0 ? fullName : fullName[..firstSpace];
    }
}
