using Cleansia.Core.AppServices.Features.Employees;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The partner onboarding commands used to accept an <c>Email</c>, validate it, and then drop it —
/// <c>User.Update</c> has no email parameter — so the partner got a success toast for a change that
/// never happened. Whether a partner may change their own email at all is still an open product
/// decision (the session resolves by the email claim, so a live change logs them out mid-chain), and
/// until it is answered the wire shape must not offer the field. Reflection, because the defect is the
/// existence of the member, not any behavior it has.
/// </summary>
public class EmailIsNotAnOnboardingInputTests
{
    [Theory]
    [InlineData(typeof(UpdateEmployee.Command))]
    [InlineData(typeof(UpdatePersonalInfo.Command))]
    public void The_Onboarding_Commands_Expose_No_Email_Member(Type commandType)
    {
        Assert.DoesNotContain(commandType.GetProperties(), p => p.Name == "Email");
    }
}
