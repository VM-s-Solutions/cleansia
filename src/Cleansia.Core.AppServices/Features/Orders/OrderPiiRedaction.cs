#nullable enable
using Cleansia.Core.AppServices.Features.Orders.DTOs;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// What a cleaner who has NOT taken this job and is not its customer may read about it.
/// Listed guest bookings use that browsing shape with only what their e-mails carried put back.
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
            CustomerAddressLatitude = null,
            CustomerAddressLongitude = null,
            IsGuestBooking = null,
        };

    /// <summary>
    /// A booking made without an account, as a signed-in customer's My orders lists it under their e-mail
    /// (owner ruling 2026-10-26, "I select option B"): what its own e-mails carried, which is not the phone.
    /// </summary>
    public static OrderListItem RedactForListedGuestBooking(this OrderListItem item) =>
        item.RedactForBrowsingCleaner() with
        {
            CustomerName = item.CustomerName,
            CustomerEmail = item.CustomerEmail,
            CustomerAddress = item.CustomerAddress,
            CustomerAddressLatitude = item.CustomerAddressLatitude,
            CustomerAddressLongitude = item.CustomerAddressLongitude,
            IsGuestBooking = true,
        };

    /// <summary>
    /// The detail of the same listed booking: the browsing shape, with what the booking's e-mails carried put
    /// back — the name, the e-mail, the address and the cancellation window — and the crew as a customer sees
    /// it. Built from the browsing shape so a member added to the DTO later is withheld here until someone
    /// says otherwise; nothing the household or the crew wrote is shown, nor the phone.
    /// </summary>
    public static OrderItem RedactForListedGuestBooking(this OrderItem item) =>
        item.RedactForBrowsingCleaner() with
        {
            CustomerName = item.CustomerName,
            CustomerEmail = item.CustomerEmail,
            Address = item.Address,
            FreeCancellationHours = item.FreeCancellationHours,
            IsGuestBooking = true,
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
            Notes = null,
            SpecialInstructions = null,
            AccessInstructions = null,
            CustomerFloor = null,
            CustomerApartment = null,
            // Knowing there IS a door code is most of knowing the code is worth
            // asking for, so the shape of the answer is redacted with the answer.
            AccessMode = null,
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
            CustomerCompany = null,
            WorkContractAcceptances = [],
            CancellationFeeRate = null,
            CancellationFeeOwed = null,
            NeedsConfirmation = null,
            CashCollectedAt = null,
            CashCollectedByName = null,
            CashCollectedAmount = null,
            LockoutReportedAt = null,
            LockoutCallAttempts = null,
            FreeCancellationHours = null,
            IsGuestBooking = null,
        };

    /// <summary>
    /// A job the cleaner worked, once their access to the customer has ended (24 hours after completion,
    /// at once on cancellation — owner ruling 2026-09-28): the browsing shape, keeping what is the
    /// cleaner's own — the completion notes, their notes and their contract acceptance.
    /// </summary>
    public static OrderItem RedactForPastJob(this OrderItem item, string employeeId) =>
        item.RedactForBrowsingCleaner() with
        {
            CompletionNotes = item.CompletionNotes,
            OrderNotes = item.OrderNotes.Where(note => note.EmployeeId == employeeId).ToList(),
            WorkContractAcceptances = (item.WorkContractAcceptances ?? [])
                .Where(acceptance => acceptance.EmployeeId == employeeId)
                .ToList(),
        };

    /// <summary>
    /// The same disclosure the customer gets about the crew (<c>MapToAssignedEmployeeDto</c>): a given
    /// name, no phone number and no time a cleaner marked their part done. <c>EmployeeId</c> stays — the
    /// partner apps decide "am I on this job?" by looking for their own id in this list, so removing it
    /// would answer that question wrong.
    /// </summary>
    private static AssignedEmployeeDto WithoutPersonalContact(AssignedEmployeeDto employee) =>
        employee with
        {
            FullName = GivenNameOnly(employee.FullName),
            PhoneNumber = null,
            CompletionConfirmedAt = null,
            ProfilePhoto = null,
        };

    private static string GivenNameOnly(string fullName)
    {
        var firstSpace = fullName.IndexOf(' ');
        return firstSpace < 0 ? fullName : fullName[..firstSpace];
    }
}
