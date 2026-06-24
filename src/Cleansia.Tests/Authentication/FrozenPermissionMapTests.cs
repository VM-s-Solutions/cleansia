using System.Reflection;
using Cleansia.Core.AppServices.Authentication;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// Verification #4 (ADR-0001 §D2) — the frozen permission-map snapshot.
///
/// Asserts the live <c>PolicyBuilder.Map</c> (<c>Policy.* → PhysicalPolicy.*</c>) equals the
/// D2 table exactly. A purely *additive* row updates the expected snapshot below in the same PR;
/// a *semantic* change to an existing row requires a superseding ADR before this test is touched.
/// </summary>
public class FrozenPermissionMapTests
{
    /// <summary>
    /// The D2 table, transcribed verbatim. Rows that are on the AllowAnonymous allow-list are
    /// intentionally NOT here — they are not in <c>Map</c>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> ExpectedD2Map = new Dictionary<string, string>
    {
        // Order
        [Policy.CanViewPagedOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewPagedUserOrder] = PhysicalPolicy.Authenticated,
        [Policy.CanViewOrderDetail] = PhysicalPolicy.Authenticated,
        [Policy.CanUpdateOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanTakeOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanStartOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanCompleteOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanUploadOrderPhoto] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewOrderPhotos] = PhysicalPolicy.Authenticated,
        [Policy.CanDeleteOrderPhoto] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanAddOrderNote] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanUpdateOrderNote] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanDeleteOrderNote] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanReportOrderIssue] = PhysicalPolicy.Authenticated,
        [Policy.CanUpdateOrderIssue] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanDeleteOrderIssue] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanSubmitOrderReview] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewOrderReview] = PhysicalPolicy.Authenticated,
        [Policy.CanCancelOrder] = PhysicalPolicy.CustomerOnly,
        [Policy.CanAdminCancelOrder] = PhysicalPolicy.AdminOnly,   // AUD-01 admin order ops (additive)
        [Policy.CanOverrideOrderStatus] = PhysicalPolicy.AdminOnly, // AUD-01 admin order ops (additive)
        [Policy.CanReassignOrder] = PhysicalPolicy.AdminOnly,      // AUD-01 admin order ops (additive)
        [Policy.CanRefundOrder] = PhysicalPolicy.AdminOnly,        // AUD-01 admin order ops (additive)

        // Customer self-service
        [Policy.CanManageSavedAddresses] = PhysicalPolicy.CustomerOnly,
        [Policy.CanManageMembership] = PhysicalPolicy.CustomerOnly,
        [Policy.CanManageRecurringBookings] = PhysicalPolicy.CustomerOnly,

        // User
        [Policy.CanViewPagedUser] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewUserDetail] = PhysicalPolicy.OwnerOrElevated,
        [Policy.CanGetCurrentUser] = PhysicalPolicy.Authenticated,
        [Policy.CanChangeOwnPassword] = PhysicalPolicy.Authenticated, // additive — [OWN-DATA] authenticated change-own-password
        [Policy.CanUpdateCurrentUser] = PhysicalPolicy.Authenticated,
        [Policy.CanAddPhoneNumber] = PhysicalPolicy.Authenticated,

        // Employee
        [Policy.CanGetCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanCheckCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanUpdateCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanViewPagedEmployee] = PhysicalPolicy.AdminOnly,
        [Policy.CanApproveEmployee] = PhysicalPolicy.AdminOnly,
        [Policy.CanRejectEmployee] = PhysicalPolicy.AdminOnly,
        [Policy.CanAdminUpdateEmployee] = PhysicalPolicy.AdminOnly,

        // Employee Documents
        [Policy.CanViewEmployeeDocuments] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanUploadEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanDownloadEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanApproveEmployeeDocument] = PhysicalPolicy.AdminOnly,
        [Policy.CanRejectEmployeeDocument] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,

        // Payroll — Invoices (added, fail-closed)
        [Policy.CanViewPagedInvoices] = PhysicalPolicy.EmployeeOrAdmin,  // [OWN-DATA] (Note A)
        [Policy.CanViewPeriodPays] = PhysicalPolicy.EmployeeOrAdmin,     // [OWN-DATA]
        [Policy.CanCalculateOrderPay] = PhysicalPolicy.AdminOnly,
        [Policy.CanGenerateInvoice] = PhysicalPolicy.AdminOnly,
        [Policy.CanApproveInvoice] = PhysicalPolicy.AdminOnly,
        [Policy.CanMarkInvoicePaid] = PhysicalPolicy.AdminOnly,
        [Policy.CanCancelInvoice] = PhysicalPolicy.AdminOnly,
        [Policy.CanClosePayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateInvoiceAmounts] = PhysicalPolicy.AdminOnly,    // AUD-02 settlement (additive)
        [Policy.CanDisputeInvoice] = PhysicalPolicy.AdminOnly,          // AUD-02 settlement (additive)
        [Policy.CanRejectInvoice] = PhysicalPolicy.AdminOnly,           // AUD-02 settlement (additive)

        // Payroll — Pay Periods
        [Policy.CanViewPayPeriods] = PhysicalPolicy.EmployeeOrAdmin,     // global cycles (Note B)
        [Policy.CanViewPayPeriod] = PhysicalPolicy.EmployeeOrAdmin,      // global cycles (Note B)
        [Policy.CanCreatePayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdatePayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanOpenPayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeletePayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanMarkPayPeriodPaid] = PhysicalPolicy.AdminOnly,       // AUD-02 settlement (additive)
        [Policy.CanReopenPayPeriod] = PhysicalPolicy.AdminOnly,         // AUD-02 settlement (additive)

        // Payroll — Pay Config
        [Policy.CanViewPayConfigs] = PhysicalPolicy.AdminOnly,
        [Policy.CanViewPayConfig] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreatePayConfig] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdatePayConfig] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeletePayConfig] = PhysicalPolicy.AdminOnly,

        // Dispute (split)
        [Policy.CanCreateDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDisputeList] = PhysicalPolicy.CustomerOnly,
        [Policy.CanAddDisputeMessage] = PhysicalPolicy.CustomerOnly,     // [OWN-DATA] (new, Note C)
        [Policy.CanRespondToDispute] = PhysicalPolicy.AdminOnly,         // staff path (was Authenticated)
        [Policy.CanResolveDispute] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateDisputeStatus] = PhysicalPolicy.AdminOnly,
        [Policy.CanUploadDisputeEvidence] = PhysicalPolicy.CustomerOnly,
        // Admin-host dispute reads (D-01 admin dispute management, additive). Distinct from the
        // CustomerOnly CanViewDispute/CanViewDisputeList own-data reads — admin sees all disputes.
        [Policy.CanViewDisputeAdmin] = PhysicalPolicy.AdminOnly,
        [Policy.CanViewDisputeListAdmin] = PhysicalPolicy.AdminOnly,

        // Reports
        [Policy.CanViewRevenueReport] = PhysicalPolicy.AdminOnly,
        [Policy.CanViewPayrollReport] = PhysicalPolicy.AdminOnly,

        // Fiscal
        [Policy.CanManageFiscalFailures] = PhysicalPolicy.AdminOnly,

        // Services
        [Policy.CanViewServices] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateService] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateService] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteService] = PhysicalPolicy.AdminOnly,

        // Packages
        [Policy.CanViewPackages] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreatePackage] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdatePackage] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeletePackage] = PhysicalPolicy.AdminOnly,

        // Languages
        [Policy.CanViewLanguages] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateLanguage] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateLanguage] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteLanguage] = PhysicalPolicy.AdminOnly,

        // Countries
        [Policy.CanViewCountries] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCountry] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateCountry] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteCountry] = PhysicalPolicy.AdminOnly,

        // Service areas
        [Policy.CanViewServiceCities] = PhysicalPolicy.AdminOnly,
        [Policy.CanManageServiceCities] = PhysicalPolicy.AdminOnly,

        // Currencies
        [Policy.CanViewCurrencies] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCurrency] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateCurrency] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteCurrency] = PhysicalPolicy.AdminOnly,

        // Admin Users
        [Policy.CanViewAdminUsers] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateAdminUser] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateAdminUser] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeactivateAdminUser] = PhysicalPolicy.AdminOnly,
        [Policy.CanActivateAdminUser] = PhysicalPolicy.AdminOnly,

        // Company Info
        [Policy.CanViewCompanyInfo] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCompanyInfo] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateCompanyInfo] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteCompanyInfo] = PhysicalPolicy.AdminOnly,

        // Email Templates
        [Policy.CanViewEmailTemplates] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateEmailTemplate] = PhysicalPolicy.AdminOnly,

        // Feature Flags
        [Policy.CanViewFeatureFlags] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateFeatureFlag] = PhysicalPolicy.AdminOnly,
        [Policy.CanToggleFeatureFlag] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteFeatureFlag] = PhysicalPolicy.AdminOnly,
        [Policy.CanCheckFeatureFlag] = PhysicalPolicy.Authenticated,

        // Country Configuration
        [Policy.CanViewCountryConfigurations] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCountryConfiguration] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateCountryConfiguration] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteCountryConfiguration] = PhysicalPolicy.AdminOnly,

        // Tenant Configuration
        [Policy.CanViewTenantConfigurations] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateTenantConfiguration] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateTenantConfiguration] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeleteTenantConfiguration] = PhysicalPolicy.AdminOnly,

        // Device
        [Policy.Authenticated] = PhysicalPolicy.Authenticated,

        // GDPR (self)
        [Policy.CanExportOwnData] = PhysicalPolicy.Authenticated,
        [Policy.CanDeleteOwnAccount] = PhysicalPolicy.Authenticated,
        [Policy.CanGrantConsent] = PhysicalPolicy.Authenticated,
        [Policy.CanWithdrawConsent] = PhysicalPolicy.Authenticated,
        [Policy.CanViewOwnConsents] = PhysicalPolicy.Authenticated,

        // GDPR (admin)
        [Policy.CanAdminExportUserData] = PhysicalPolicy.AdminOnly,
        [Policy.CanAdminDeleteUserAccount] = PhysicalPolicy.AdminOnly,
        [Policy.CanAdminViewUserConsents] = PhysicalPolicy.AdminOnly,
        [Policy.CanViewGdprRequests] = PhysicalPolicy.AdminOnly,

        // Loyalty / Promo / Referral (customer)
        [Policy.CanViewMyLoyalty] = PhysicalPolicy.CustomerOnly,
        [Policy.CanRedeemPromoCode] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewMyReferral] = PhysicalPolicy.CustomerOnly,

        // Admin Promo Codes
        [Policy.CanViewPromoCodes] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreatePromoCode] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdatePromoCode] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeactivatePromoCode] = PhysicalPolicy.AdminOnly,

        // Admin Loyalty Tier Configs
        [Policy.CanViewLoyaltyTierConfigs] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateLoyaltyTierConfig] = PhysicalPolicy.AdminOnly,

        // Admin Loyalty (manual grants + user inspection)
        [Policy.CanGrantLoyaltyPoints] = PhysicalPolicy.AdminOnly,
        [Policy.CanViewUserLoyalty] = PhysicalPolicy.AdminOnly,

        // Admin Membership Plans (additive — T-0175a / LG-04)
        [Policy.CanViewMembershipPlans] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateMembershipPlan] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateMembershipPlan] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeactivateMembershipPlan] = PhysicalPolicy.AdminOnly,

        // Admin Referrals
        [Policy.CanViewReferrals] = PhysicalPolicy.AdminOnly,
        [Policy.CanInterveneReferral] = PhysicalPolicy.AdminOnly, // additive — referral intervention (LG-06)

        // Marketing
        [Policy.CanSendSitewidePromo] = PhysicalPolicy.AdminOnly,

        // Refunds (admin-issued partial refund)
        [Policy.CanIssueRefund] = PhysicalPolicy.AdminOnly,

        // Admin Action Audit Log read surface (additive — ADR-0012 D7 / T-0285)
        [Policy.CanViewAuditLog] = PhysicalPolicy.AdminOnly,
    };

    private static IReadOnlyDictionary<string, string> ActualMap()
    {
        var field = typeof(PolicyBuilder).GetField("Map",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (IReadOnlyDictionary<string, string>)field.GetValue(null)!;
    }

    [Fact]
    public void Map_Matches_The_Frozen_D2_Table_Exactly()
    {
        var actual = ActualMap();

        var onlyInActual = actual.Keys.Except(ExpectedD2Map.Keys).OrderBy(k => k).ToList();
        var onlyInExpected = ExpectedD2Map.Keys.Except(actual.Keys).OrderBy(k => k).ToList();
        var mismatched = ExpectedD2Map.Keys.Intersect(actual.Keys)
            .Where(k => actual[k] != ExpectedD2Map[k])
            .Select(k => $"{k}: expected {ExpectedD2Map[k]} but was {actual[k]}")
            .OrderBy(s => s)
            .ToList();

        Assert.True(
            onlyInActual.Count == 0 && onlyInExpected.Count == 0 && mismatched.Count == 0,
            "PolicyBuilder.Map drifted from the ADR-0001 D2 frozen table. " +
            "An ADDITIVE row updates this snapshot in-PR; a SEMANTIC change needs a superseding ADR.\n" +
            $"In Map but not expected: {string.Join(", ", onlyInActual)}\n" +
            $"Expected but not in Map: {string.Join(", ", onlyInExpected)}\n" +
            $"Wrong physical policy: {string.Join("; ", mismatched)}");
    }

    [Fact]
    public void Dispute_Split_Is_Mapped_Per_D2()
    {
        // the overloaded CanRespondToDispute=Authenticated is gone.
        Assert.Equal(PhysicalPolicy.AdminOnly, Policy.CanRespondToDispute.ToPhysicalPolicy());
        Assert.Equal(PhysicalPolicy.CustomerOnly, Policy.CanAddDisputeMessage.ToPhysicalPolicy());
    }

    [Fact]
    public void Entire_Payroll_Family_Is_Mapped_Closed()
    {
        // none of these may resolve to Authenticated anymore.
        string[] adminOnly =
        {
            Policy.CanCalculateOrderPay, Policy.CanGenerateInvoice, Policy.CanApproveInvoice,
            Policy.CanMarkInvoicePaid, Policy.CanCancelInvoice, Policy.CanClosePayPeriod,
            Policy.CanCreatePayPeriod, Policy.CanUpdatePayPeriod, Policy.CanOpenPayPeriod,
            Policy.CanDeletePayPeriod, Policy.CanViewPayConfigs, Policy.CanViewPayConfig,
            Policy.CanCreatePayConfig, Policy.CanUpdatePayConfig, Policy.CanDeletePayConfig,
            Policy.CanUpdateInvoiceAmounts, Policy.CanDisputeInvoice, Policy.CanRejectInvoice,
            Policy.CanMarkPayPeriodPaid, Policy.CanReopenPayPeriod,
        };
        foreach (var p in adminOnly)
            Assert.Equal(PhysicalPolicy.AdminOnly, p.ToPhysicalPolicy());

        string[] employeeOrAdmin =
        {
            Policy.CanViewPagedInvoices, Policy.CanViewPeriodPays,
            Policy.CanViewPayPeriods, Policy.CanViewPayPeriod,
        };
        foreach (var p in employeeOrAdmin)
            Assert.Equal(PhysicalPolicy.EmployeeOrAdmin, p.ToPhysicalPolicy());
    }
}
