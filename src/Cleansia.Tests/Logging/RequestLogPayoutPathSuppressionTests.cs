using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Tests.Logging;

/// <summary>
/// ADR-0034 AC8 / S6 — the payout value is never logged, CHECKED rather than assumed.
///
/// <para>The hosts slice request and response bodies into Information-level logs and redact by field
/// NAME. <c>accountNumber</c>, <c>iban</c>, <c>swift</c> and <c>holderName</c> are in none of those token
/// lists, and the guard that catches unmasking
/// (<see cref="RedactionUnmaskedFreeTextGuardTests"/>) says in its own doc-comment that a secret whose
/// field name is simply absent from the token list is caught by <b>nothing</b>. So the payout routes are
/// suppressed wholesale, on every host, and that is pinned here.</para>
/// </summary>
public class RequestLogPayoutPathSuppressionTests
{
    [Theory]
    [InlineData("/api/Employee/UpdateBankDetails")]
    [InlineData("/api/Employee/GetMyPayoutDetails")]
    [InlineData("/api/AdminEmployee/emp-1/payout-details")]
    [InlineData("/api/AdminEmployee/emp-1/payout-details/reveal")]
    [InlineData("/api/Gdpr/export")]
    public void Payout_Carrying_Routes_Are_Suppressed_On_Every_Host(string route)
    {
        foreach (var middleware in RequestLoggingHarness.AllHostMiddleware)
        {
            var suppressed = (bool)middleware
                .GetMethod("IsSensitivePath", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [new PathString(route)])!;

            Assert.True(suppressed, $"{route} must be in IsSensitivePath on {middleware.FullName}");
        }
    }
}
