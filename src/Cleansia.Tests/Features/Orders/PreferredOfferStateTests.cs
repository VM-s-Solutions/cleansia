using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0045 D7.1 — the customer-facing state is DERIVED from four columns already on the row, for the
/// same reason ADR-0036 stores a deadline rather than a flag: a derived state has no writer, cannot go
/// stale, needs no backfill and cannot be left inconsistent by a path nobody remembered.
///
/// <para><c>None</c> covers every case with NO reservation — no preference, a non-member, a declined
/// resolve outcome, and the entire 2-8 h notify-only band. Telling a customer in that band that someone
/// is "considering" their booking would be false: nothing is withheld from anyone.</para>
/// </summary>
public class PreferredOfferStateTests
{
    private static readonly DateTime Now = new(2026, 8, 9, 9, 0, 0, DateTimeKind.Utc);
    private const string Beneficiary = "employee-favourite";

    [Fact]
    public void No_Preference_Is_No_Offer()
    {
        Assert.Equal(
            PreferredOfferState.None,
            PreferredOffer.StateOf(null, null, beneficiaryIsAssigned: false, Now));
    }

    /// <summary>
    /// The notify-only band: <c>Order.Create</c> writes <c>PreferredEmployeeId</c> independently of any
    /// hold, so the stored pick is non-null while nothing was ever withheld. Reading the preference
    /// column as the reservation is the mistake this case exists to catch.
    /// </summary>
    [Fact]
    public void A_Stored_Preference_That_Never_Earned_A_Hold_Is_No_Offer()
    {
        Assert.Equal(
            PreferredOfferState.None,
            PreferredOffer.StateOf(Beneficiary, null, beneficiaryIsAssigned: false, Now));
    }

    /// <summary>
    /// The stranded pair — a deadline with nobody able to act on it. Unwritable through the aggregate,
    /// and read as no reservation here for the same reason <c>OrderVisibility</c> reads it as no hold:
    /// one end is not enough when no actor is permitted to clear it.
    /// </summary>
    [Fact]
    public void A_Deadline_With_No_Beneficiary_Is_No_Offer()
    {
        Assert.Equal(
            PreferredOfferState.None,
            PreferredOffer.StateOf(null, Now.AddHours(2), beneficiaryIsAssigned: false, Now));
    }

    [Fact]
    public void A_Live_Reservation_Is_Awaiting_Confirmation()
    {
        Assert.Equal(
            PreferredOfferState.AwaitingConfirmation,
            PreferredOffer.StateOf(Beneficiary, Now.AddHours(2), beneficiaryIsAssigned: false, Now));
    }

    [Fact]
    public void A_Reservation_That_Ran_Out_Is_Closed()
    {
        Assert.Equal(
            PreferredOfferState.Closed,
            PreferredOffer.StateOf(Beneficiary, Now.AddMinutes(-1), beneficiaryIsAssigned: false, Now));
    }

    /// <summary>The deadline is exclusive on the open side — <c>&lt;= now</c> is over, exactly as
    /// <c>OrderVisibility</c> term 3 releases the seats.</summary>
    [Fact]
    public void The_Deadline_Instant_Itself_Is_Already_Closed()
    {
        Assert.Equal(
            PreferredOfferState.Closed,
            PreferredOffer.StateOf(Beneficiary, Now, beneficiaryIsAssigned: false, Now));
    }

    [Fact]
    public void The_Beneficiary_Confirming_Is_Accepted()
    {
        Assert.Equal(
            PreferredOfferState.Accepted,
            PreferredOffer.StateOf(Beneficiary, Now.AddHours(2), beneficiaryIsAssigned: true, Now));
    }

    /// <summary>
    /// Acceptance outlives the deadline. Nothing clears the pair when the cleaner confirms, so a
    /// state derived from the clock alone would flip a confirmed booking back to "closed" the moment
    /// the window ran out.
    /// </summary>
    [Fact]
    public void Acceptance_Survives_The_Deadline_Passing()
    {
        Assert.Equal(
            PreferredOfferState.Accepted,
            PreferredOffer.StateOf(Beneficiary, Now.AddHours(-3), beneficiaryIsAssigned: true, Now));
    }

    /// <summary>
    /// Somebody else took it off the open board after the lapse. The reservation still ended without a
    /// confirmation — the customer hears about the assignment through <c>order.cleaner_assigned</c>,
    /// which is not perk-scoped and is a different sentence.
    /// </summary>
    [Fact]
    public void Another_Cleaner_Taking_The_Job_Leaves_The_Offer_Closed()
    {
        Assert.Equal(
            PreferredOfferState.Closed,
            PreferredOffer.StateOf(Beneficiary, Now.AddHours(-3), beneficiaryIsAssigned: false, Now));
    }
}
