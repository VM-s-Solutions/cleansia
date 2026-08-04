using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Domain.Payouts;

/// <summary>
/// ADR-0034 D7 / F2 — the profile gate decides whether a cleaner may take orders at all
/// (<c>[RequireCompleteProfile]</c> is class-level on the partner host's Order, Payroll, Dashboard and
/// Dispute controllers), so the payout term must mean PRESENCE and must be readable from the row.
///
/// <para>The first test is the deploy-day one: there is no backfill, so an existing cleaner's
/// <c>HasPayoutDetails</c> is <c>false</c> on the morning of the release. If the gate read only that
/// flag, every one of them would 403 off the whole partner surface.</para>
/// </summary>
public class EmployeePayoutProfileGateTests
{
    private static Employee CompleteExceptPayout()
    {
        var user = User.CreateWithPassword("cleaner@cleansia.cz", "Password1", "First", "Last");
        user.Update("First", "Last", "+420111222333", new DateOnly(1990, 1, 1));

        var employee = Employee.CreateWithUser(user);
        employee.UpdateEmployeeDetails(
            entityType: EmployeeEntityType.NaturalPerson,
            registrationNumber: "12345678",
            vatNumber: null,
            legalEntityName: null,
            nationalityId: "country-cz",
            passportId: "P1234567",
            address: Address.Create("Wenceslas Square 1", "Prague", "11000", "country-cz"),
            availability: new Dictionary<string, List<TimeRange>>(),
            emergencyContactName: null,
            emergencyContactPhone: null);

        return employee;
    }

    [Fact]
    public void An_Existing_Cleaner_Whose_Account_Predates_The_Payout_Record_Is_Not_Locked_Out()
    {
        var employee = CompleteExceptPayout();
        employee.UpdateBankDetails("CZ6508000000192000145399");

        Assert.False(employee.HasPayoutDetails);
        Assert.True(employee.IsProfileComplete());
        Assert.DoesNotContain("profile.fields.iban", employee.GetMissingProfileFields());
    }

    [Fact]
    public void A_Cleaner_With_A_Payout_Record_Is_Complete_Without_Reading_The_Navigation()
    {
        var employee = CompleteExceptPayout();
        employee.MarkPayoutDetailsProvided();

        Assert.Null(employee.PayoutDetails);
        Assert.True(employee.IsProfileComplete());
        Assert.DoesNotContain("profile.fields.iban", employee.GetMissingProfileFields());
    }

    [Fact]
    public void A_Cleaner_With_No_Payout_Destination_At_All_Is_Incomplete()
    {
        var employee = CompleteExceptPayout();

        Assert.False(employee.IsProfileComplete());
        Assert.Contains("profile.fields.iban", employee.GetMissingProfileFields());
    }

    [Fact]
    public void Clearing_The_Flag_Drops_The_Gate_Only_When_No_Legacy_Value_Remains()
    {
        var employee = CompleteExceptPayout();
        employee.MarkPayoutDetailsProvided();
        employee.ClearPayoutDetails();

        Assert.False(employee.IsProfileComplete());
    }

    /// <summary>
    /// The gate carries presence, never validity (D7): a stored value the new validator would reject must
    /// not retroactively take a working cleaner off the job board.
    /// </summary>
    [Fact]
    public void An_Unvalidatable_Legacy_Value_Still_Counts_As_Presence()
    {
        var employee = CompleteExceptPayout();
        employee.UpdateBankDetails("1920001453990800");

        Assert.True(employee.IsProfileComplete());
    }
}
