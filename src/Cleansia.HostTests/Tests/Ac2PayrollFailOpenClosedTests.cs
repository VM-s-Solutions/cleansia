using System.Net;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The payroll family is no longer fail-open. The 21 payroll
/// <c>Policy</c> rows that were unmapped (→ Deny / fail-open) now resolve to AdminOnly /
/// EmployeeOrAdmin, so a Customer can never reach a payroll endpoint. End-to-end proof through the real
/// pipeline: a Customer caller is denied on every payroll route — either at the JWT audience boundary
/// (a genuine cleansia.customer token does not authenticate on the Partner host) or at the [Permission]
/// policy (a Partner-audience token carrying the Customer role is 403'd). Never 200/the resource.
///
/// On current (fixed) code these are GREEN — they are the end-to-end regression proof the fix holds
/// through middleware/policy/JWT, the layer the in-process harness cannot reach (ADR-0001 §D6).
/// </summary>
public sealed class Ac2PayrollFailOpenClosedTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    // Read-only payroll endpoints (no request body) spanning the invoice / pay-period / pay-config
    // families — each maps to an AdminOnly or EmployeeOrAdmin physical policy.
    public static TheoryData<string> PayrollEndpoints =>
    [
        "/api/EmployeePayroll/GetPagedInvoices",   // CanViewPagedInvoices = EmployeeOrAdmin
        "/api/PayPeriod/GetPagedPayPeriods",        // CanViewPayPeriods    = EmployeeOrAdmin
        "/api/PayConfig/GetPagedPayConfigs",        // CanViewPayConfigs    = AdminOnly
    ];

    [Theory]
    [MemberData(nameof(PayrollEndpoints))]
    public async Task Customer_audience_jwt_cannot_reach_payroll_endpoint(string endpoint)
    {
        // A genuine Customer-audience token (cleansia.customer) on the Partner host where payroll lives.
        var token = TestJwtFactory.Mint(CustomerAudience, "u-cust", "cust@hosttests.local", UserProfile.Customer);
        var resp = await PartnerClient(token).GetAsync(endpoint);

        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(
            resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 on {endpoint} for a customer-audience token, got {(int)resp.StatusCode}.");
    }

    [Theory]
    [MemberData(nameof(PayrollEndpoints))]
    public async Task Customer_role_on_partner_host_is_403d_by_the_payroll_policy(string endpoint)
    {
        // Right host audience (cleansia.partner) but the Customer ROLE → authenticates, then the real
        // [Permission] payroll policy denies it. This is the precise "fail-open closed" assertion: the
        // gate RAN and said no, rather than letting an unmapped permission through.
        var token = TestJwtFactory.Mint(PartnerAudience, "u-cust2", "cust2@hosttests.local", UserProfile.Customer);
        var resp = await PartnerClient(token).GetAsync(endpoint);

        HttpAssert.IsForbidden(resp);
    }
}
