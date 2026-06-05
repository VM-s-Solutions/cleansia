using System.Net;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The host harness itself. Proves each of the four API hosts boots through the FULL
/// authentication + authorization pipeline (the host's own AddJwt bearer validation + the shared
/// AddCleansiaAuthorization policies + the real [Permission] gate), that a per-audience minted JWT
/// authenticates, that a wrong-audience token is rejected, and that the [Permission] role gate
/// genuinely enforces. This is the new infra ADR-0001 §D6 / verification #5 requires; it is green
/// standalone (no fix ticket dependency beyond AddCleansiaAuthorization, already landed).
/// </summary>
public sealed class Ac1HostHarnessSmokeTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    [Fact]
    public async Task Partner_host_authenticates_a_partner_audience_jwt_and_reaches_the_permission_gate()
    {
        var token = TestJwtFactory.Mint(PartnerAudience, "u-partner", "partner@hosttests.local", UserProfile.Employee);
        var resp = await PartnerClient(token).GetAsync("/api/User/GetCurrent");

        // Authentication succeeded (NOT 401) and the [Permission(CanGetCurrentUser)=Authenticated]
        // gate let the authenticated employee through to the handler — the end-to-end pipeline ran.
        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Customer_host_authenticates_a_customer_audience_jwt_and_reaches_the_permission_gate()
    {
        var token = TestJwtFactory.Mint(CustomerAudience, "u-customer", "customer@hosttests.local", UserProfile.Customer);
        var resp = await CustomerClient(token).GetAsync("/api/User/GetCurrent");

        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Admin_host_authenticates_an_admin_audience_jwt_and_passes_the_admin_permission_gate()
    {
        var token = TestJwtFactory.Mint(AdminAudience, "u-admin", "admin@hosttests.local", UserProfile.Administrator);
        var resp = await AdminClient(token).GetAsync("/api/AdminCurrency/get-overview");

        // Admin token clears the [Permission(CanViewCurrencies)=AdminOnly] gate end-to-end: the request
        // is NOT denied at the auth/authz layer (not 401/403) — it reaches the handler. (The dedicated
        // deny test below proves the same gate 403s a wrong role, so the gate is genuinely enforced.)
        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Mobile_host_authenticates_a_mobile_audience_jwt_and_reaches_the_permission_gate()
    {
        var token = TestJwtFactory.Mint(MobileAudience, "u-mobile", "mobile@hosttests.local", UserProfile.Employee);
        var resp = await MobileClient(token).GetAsync("/api/Employee/CheckCurrentEmployee");

        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.NotEqual(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Wrong_audience_token_is_rejected_with_401()
    {
        // A token minted for the Customer audience must NOT authenticate on the Admin host
        // (JWT audience validation — ADR-0001 D5 §2 host binding).
        var customerToken = TestJwtFactory.Mint(CustomerAudience, "u-x", "x@hosttests.local", UserProfile.Administrator);
        var resp = await AdminClient(customerToken).GetAsync("/api/AdminCurrency/get-overview");

        HttpAssert.IsUnauthorized(resp);
    }

    [Fact]
    public async Task No_token_is_rejected_with_401_on_a_gated_endpoint()
    {
        var resp = await PartnerClientAnonymous().GetAsync("/api/User/GetCurrent");
        HttpAssert.IsUnauthorized(resp);
    }

    [Fact]
    public async Task Permission_role_gate_denies_a_customer_role_on_an_admin_only_endpoint()
    {
        // Right audience, wrong role: a Customer-role token on the Admin host's AdminOnly endpoint
        // must 403 — proving the real [Permission] physical policy runs (not a fail-open).
        var token = TestJwtFactory.Mint(AdminAudience, "u-cust", "cust@hosttests.local", UserProfile.Customer);
        var resp = await AdminClient(token).GetAsync("/api/AdminCurrency/get-overview");

        HttpAssert.IsForbidden(resp);
    }
}
