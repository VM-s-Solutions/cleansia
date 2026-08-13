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
