using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Bookings;
using FluentValidation.TestHelper;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// The unit cancels the order its caller names, past the tenant filter, so the cutoff it is handed is bounded by
/// the sweep's own: no more than a day ahead. A cutoff further out would retract occurrences the customer still has
/// time to confirm.
/// </summary>
public class AutoCancelRecurringOccurrenceValidatorTests
{
    private const string OrderId = "ord-recurring-validated";

    [Fact]
    public void A_Cutoff_More_Than_A_Day_Ahead_Is_Refused()
    {
        var result = new AutoCancelRecurringOccurrence.Validator()
            .TestValidate(new AutoCancelRecurringOccurrence.Command(OrderId, DateTime.UtcNow.AddHours(25)));

        result.ShouldHaveValidationErrorFor(x => x.CutoffUtc);
    }

    [Fact]
    public void A_Cutoff_Within_The_Day_Passes()
    {
        var result = new AutoCancelRecurringOccurrence.Validator()
            .TestValidate(new AutoCancelRecurringOccurrence.Command(OrderId, DateTime.UtcNow.AddHours(23)));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void An_Occurrence_Without_An_Id_Is_Refused()
    {
        var result = new AutoCancelRecurringOccurrence.Validator()
            .TestValidate(new AutoCancelRecurringOccurrence.Command("", DateTime.UtcNow.AddHours(1)));

        result.ShouldHaveValidationErrorFor(x => x.OrderId).WithErrorMessage(BusinessErrorMessage.Required);
    }
}
