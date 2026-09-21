using System.Reflection;
using Cleansia.Core.AppServices.Authentication;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// Verification #4 (ADR-0001 §D2, superseded for the admin-host rows by ADR-0066 §D3) — the frozen
/// permission-map snapshot.
///
/// Asserts the live <c>PolicyBuilder.Map</c> (<c>Policy.* → PhysicalPolicy.*</c>) equals the
/// table exactly. A purely *additive* row updates the expected snapshot below in the same PR;
/// a *semantic* change to an existing row requires a superseding ADR before this test is touched —
/// ADR-0066 is that record for every row that read <c>AdminOnly</c> and for the eight shared reads
/// the admin host re-routed onto its own constants.
/// </summary>
public class FrozenPermissionMapTests
{
    /// <summary>
    /// The ADR-0001 D2 table as ADR-0066 D3 re-mapped it, transcribed verbatim. Rows that are on the
    /// AllowAnonymous allow-list are intentionally NOT here — they are not in <c>Map</c>.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> ExpectedD3Map = new Dictionary<string, string>
    {
        // Order
        [Policy.CanViewPagedOrder] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewPagedUserOrder] = PhysicalPolicy.Authenticated,
        [Policy.CanViewOrderDetail] = PhysicalPolicy.Authenticated,
        [Policy.CanViewOrderCustomer] = PhysicalPolicy.SupportOrAbove,
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
        [Policy.CanCancelOrder] = PhysicalPolicy.CustomerOnly,
        [Policy.CanAdminCancelOrder] = PhysicalPolicy.SupportOrAbove,   // AUD-01 admin order ops (additive)
        [Policy.CanOverrideOrderStatus] = PhysicalPolicy.SupportOrAbove, // AUD-01 admin order ops (additive)
        [Policy.CanReassignOrder] = PhysicalPolicy.SupportOrAbove,      // AUD-01 admin order ops (additive)
        [Policy.CanRefundOrder] = PhysicalPolicy.SupportOrAbove,        // AUD-01 admin order ops (additive)
        // Admin-host reads split from the partner-host shared reads (ADR-0066 D3)
        [Policy.CanViewPagedOrderAdmin] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewOrderDetailAdmin] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewOrderPhotosAdmin] = PhysicalPolicy.SupportOrAbove,

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

        // Employee
        [Policy.CanGetCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanCheckCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanUpdateCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanViewPagedEmployee] = PhysicalPolicy.AdminOnly,
        [Policy.CanApproveEmployee] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanRejectEmployee] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanAdminUpdateEmployee] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewEmployeePayoutDetails] = PhysicalPolicy.EmployeeOrAdmin, // ADR-0034 D8 (additive)
        [Policy.CanViewEmployeePayoutDetailsAdmin] = PhysicalPolicy.AccountantOrAbove, // ADR-0066 D3 (admin-host read)
        [Policy.CanRevealEmployeePayoutDetails] = PhysicalPolicy.ManagerOrAbove,     // ADR-0034 D8 (additive)
        [Policy.CanRevealOrderAccessInstructions] = PhysicalPolicy.SupportOrAbove,  // G-11 (additive)

        // Employee Documents
        [Policy.CanViewEmployeeDocuments] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanUploadEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanDownloadEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanApproveEmployeeDocument] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanRejectEmployeeDocument] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanDeleteEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewEmployeeDocumentsAdmin] = PhysicalPolicy.SupportOrAbove, // ADR-0066 D3 (admin-host read)

        // Payroll — Invoices (added, fail-closed)
        [Policy.CanViewPagedInvoices] = PhysicalPolicy.EmployeeOrAdmin,  // [OWN-DATA] (Note A)
        [Policy.CanViewPeriodPays] = PhysicalPolicy.EmployeeOrAdmin,     // [OWN-DATA]
        [Policy.CanViewPagedInvoicesAdmin] = PhysicalPolicy.AccountantOrAbove, // ADR-0066 D3 (admin-host read)
        [Policy.CanGenerateInvoice] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanApproveInvoice] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanMarkInvoicePaid] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanCancelInvoice] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanClosePayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanUpdateInvoiceAmounts] = PhysicalPolicy.AccountantOrAbove,    // AUD-02 settlement (additive)
        [Policy.CanDisputeInvoice] = PhysicalPolicy.AccountantOrAbove,          // AUD-02 settlement (additive)
        [Policy.CanRejectInvoice] = PhysicalPolicy.AccountantOrAbove,           // AUD-02 settlement (additive)

        // Payroll — Pay Periods
        [Policy.CanViewPayPeriods] = PhysicalPolicy.EmployeeOrAdmin,     // global cycles (Note B)
        [Policy.CanViewPayPeriodsAdmin] = PhysicalPolicy.AccountantOrAbove, // ADR-0066 D3 (admin-host read)
        [Policy.CanViewPayPeriodAdmin] = PhysicalPolicy.AccountantOrAbove,  // ADR-0066 D3 (admin-host read)
        [Policy.CanCreatePayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanUpdatePayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanOpenPayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanDeletePayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanMarkPayPeriodPaid] = PhysicalPolicy.AccountantOrAbove,       // AUD-02 settlement (additive)
        [Policy.CanReopenPayPeriod] = PhysicalPolicy.AccountantOrAbove,         // AUD-02 settlement (additive)

        // Payroll — Pay Config
        [Policy.CanViewPayConfigs] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanViewPayConfig] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanCreatePayConfig] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdatePayConfig] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeletePayConfig] = PhysicalPolicy.ManagerOrAbove,

        // Dispute (split)
        [Policy.CanCreateDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDisputeList] = PhysicalPolicy.CustomerOnly,
        [Policy.CanAddDisputeMessage] = PhysicalPolicy.CustomerOnly,     // [OWN-DATA] (new, Note C)
        [Policy.CanRespondToDispute] = PhysicalPolicy.SupportOrAbove,         // staff path (was Authenticated)
        [Policy.CanResolveDispute] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanUpdateDisputeStatus] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanUploadDisputeEvidence] = PhysicalPolicy.CustomerOnly,
        // Admin-host dispute reads (D-01 admin dispute management, additive). Distinct from the
        // CustomerOnly CanViewDispute/CanViewDisputeList own-data reads — admin sees all disputes.
        [Policy.CanViewDisputeAdmin] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewDisputeListAdmin] = PhysicalPolicy.SupportOrAbove,

        // Reports
        [Policy.CanViewRevenueReport] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanViewPayrollReport] = PhysicalPolicy.AccountantOrAbove,

        // Fiscal
        [Policy.CanManageFiscalFailures] = PhysicalPolicy.AccountantOrAbove,

        // Services
        [Policy.CanViewServices] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateService] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdateService] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeleteService] = PhysicalPolicy.ManagerOrAbove,

        // Packages
        [Policy.CanViewPackages] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreatePackage] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdatePackage] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeletePackage] = PhysicalPolicy.ManagerOrAbove,

        // Extras (T-0698 admin extras CRUD, additive)
        [Policy.CanViewExtras] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateExtra] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdateExtra] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeleteExtra] = PhysicalPolicy.ManagerOrAbove,

        // Languages
        [Policy.CanViewLanguages] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateLanguage] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdateLanguage] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeleteLanguage] = PhysicalPolicy.ManagerOrAbove,

        // Countries
        [Policy.CanViewCountries] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCountry] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdateCountry] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeleteCountry] = PhysicalPolicy.ManagerOrAbove,

        // Service areas
        [Policy.CanViewServiceCities] = PhysicalPolicy.AdminOnly,
        [Policy.CanManageServiceCities] = PhysicalPolicy.ManagerOrAbove,

        // Currencies
        [Policy.CanViewCurrencies] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCurrency] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdateCurrency] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeleteCurrency] = PhysicalPolicy.ManagerOrAbove,

        // Admin Users
        [Policy.CanViewAdminUsers] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanCreateAdminUser] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanUpdateAdminUser] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanDeactivateAdminUser] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanActivateAdminUser] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanSetAdminRole] = PhysicalPolicy.AdministratorOnly, // ADR-0066 D4 (additive)

        // Company Info
        [Policy.CanViewCompanyInfo] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCompanyInfo] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdateCompanyInfo] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeleteCompanyInfo] = PhysicalPolicy.ManagerOrAbove,

        // Email Templates
        [Policy.CanViewEmailTemplates] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateEmailTemplate] = PhysicalPolicy.ManagerOrAbove,

        // Feature Flags — REMOVED (T-0689). The five Can*FeatureFlag policies guarded a CRUD surface
        // over a table that gated nothing; endpoints, policies and table were deleted together. This is a
        // removal, not the additive case or the semantic case the class doc names: no surviving route
        // changed its physical policy, and no permission was widened. -> /decisions/adr-0001

        // Legal documents (ADR-0066 D3 — moved off CanViewCountryConfigurations)
        [Policy.CanViewLegalDocuments] = PhysicalPolicy.AdministratorOnly,

        // Tenant Configuration
        [Policy.CanViewTenantConfigurations] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanUpdateTenantConfiguration] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanDeleteTenantConfiguration] = PhysicalPolicy.AdministratorOnly,

        // Company lifecycle (ADR-0064)
        [Policy.CanViewCompanyLifecycle] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanDeactivateCompany] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanReactivateCompany] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanWindDownCompany] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanArchiveCompany] = PhysicalPolicy.AdministratorOnly,

        // Device
        [Policy.Authenticated] = PhysicalPolicy.Authenticated,

        // GDPR (self)
        [Policy.CanExportOwnData] = PhysicalPolicy.Authenticated,
        [Policy.CanDeleteOwnAccount] = PhysicalPolicy.Authenticated,
        [Policy.CanGrantConsent] = PhysicalPolicy.Authenticated,
        [Policy.CanWithdrawConsent] = PhysicalPolicy.Authenticated,
        [Policy.CanViewOwnConsents] = PhysicalPolicy.Authenticated,

        // GDPR (admin)
        [Policy.CanAdminExportUserData] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanAdminDeleteUserAccount] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanAdminViewUserConsents] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewGdprRequests] = PhysicalPolicy.SupportOrAbove,

        // Loyalty / Promo / Referral (customer)
        [Policy.CanViewMyLoyalty] = PhysicalPolicy.CustomerOnly,

        // Credit (additive)
        [Policy.CanViewMyCredit] = PhysicalPolicy.CustomerOnly,
        [Policy.CanIssueCustomerCredit] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewUserCredit] = PhysicalPolicy.AdminOnly,
        [Policy.CanExpireCustomerCredit] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanRedeemPromoCode] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewMyReferral] = PhysicalPolicy.CustomerOnly,

        // Admin Promo Codes
        [Policy.CanViewPromoCodes] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreatePromoCode] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdatePromoCode] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeactivatePromoCode] = PhysicalPolicy.ManagerOrAbove,

        // Admin Loyalty Tier Configs
        [Policy.CanViewLoyaltyTierConfigs] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateLoyaltyTierConfig] = PhysicalPolicy.ManagerOrAbove,

        // Admin Loyalty (manual grants + user inspection)
        [Policy.CanGrantLoyaltyPoints] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewUserLoyalty] = PhysicalPolicy.SupportOrAbove,

        // Admin Membership Plans (additive — T-0175a / LG-04)
        [Policy.CanViewMembershipPlans] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateMembershipPlan] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdateMembershipPlan] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeactivateMembershipPlan] = PhysicalPolicy.ManagerOrAbove,

        // Admin Referrals
        [Policy.CanViewReferrals] = PhysicalPolicy.AdminOnly,
        [Policy.CanInterveneReferral] = PhysicalPolicy.SupportOrAbove, // additive — referral intervention (LG-06)

        // Marketing
        [Policy.CanSendSitewidePromo] = PhysicalPolicy.ManagerOrAbove,

        // Refunds (admin-issued partial refund)
        [Policy.CanIssueRefund] = PhysicalPolicy.SupportOrAbove,

        // Admin Action Audit Log read surface (additive — ADR-0012 D7 / T-0285)
        [Policy.CanViewAuditLog] = PhysicalPolicy.SupportOrAbove,

        // Admin notifications feed (additive — ADR-0065 D1)
        [Policy.CanViewAdminNotifications] = PhysicalPolicy.AdminOnly,
    };

    private static IReadOnlyDictionary<string, string> ActualMap()
    {
        var field = typeof(PolicyBuilder).GetField("Map",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (IReadOnlyDictionary<string, string>)field.GetValue(null)!;
    }

    [Fact]
    public void Map_Matches_The_Frozen_D3_Table_Exactly()
    {
        var actual = ActualMap();

        var onlyInActual = actual.Keys.Except(ExpectedD3Map.Keys).OrderBy(k => k).ToList();
        var onlyInExpected = ExpectedD3Map.Keys.Except(actual.Keys).OrderBy(k => k).ToList();
        var mismatched = ExpectedD3Map.Keys.Intersect(actual.Keys)
            .Where(k => actual[k] != ExpectedD3Map[k])
            .Select(k => $"{k}: expected {ExpectedD3Map[k]} but was {actual[k]}")
            .OrderBy(s => s)
            .ToList();

        Assert.True(
            onlyInActual.Count == 0 && onlyInExpected.Count == 0 && mismatched.Count == 0,
            "PolicyBuilder.Map drifted from the frozen table (ADR-0001 D2 as ADR-0066 D3 re-mapped it). " +
            "An ADDITIVE row updates this snapshot in-PR; a SEMANTIC change needs a superseding ADR.\n" +
            $"In Map but not expected: {string.Join(", ", onlyInActual)}\n" +
            $"Expected but not in Map: {string.Join(", ", onlyInExpected)}\n" +
            $"Wrong physical policy: {string.Join("; ", mismatched)}");
    }

    [Fact]
    public void Dispute_Split_Is_Mapped_Per_D2()
    {
        // the overloaded CanRespondToDispute=Authenticated is gone; the staff path is Support's.
        Assert.Equal(PhysicalPolicy.SupportOrAbove, Policy.CanRespondToDispute.ToPhysicalPolicy());
        Assert.Equal(PhysicalPolicy.CustomerOnly, Policy.CanAddDisputeMessage.ToPhysicalPolicy());
    }

    [Fact]
    public void Entire_Payroll_Family_Is_Mapped_Closed()
    {
        // none of these may resolve to Authenticated anymore: every row is an administrator set.
        string[] accountantOrAbove =
        {
            Policy.CanGenerateInvoice, Policy.CanApproveInvoice,
            Policy.CanMarkInvoicePaid, Policy.CanCancelInvoice, Policy.CanClosePayPeriod,
            Policy.CanCreatePayPeriod, Policy.CanUpdatePayPeriod, Policy.CanOpenPayPeriod,
            Policy.CanDeletePayPeriod, Policy.CanViewPayConfigs, Policy.CanViewPayConfig,
            Policy.CanUpdateInvoiceAmounts, Policy.CanDisputeInvoice, Policy.CanRejectInvoice,
            Policy.CanMarkPayPeriodPaid, Policy.CanReopenPayPeriod,
            Policy.CanViewPagedInvoicesAdmin, Policy.CanViewPayPeriodsAdmin, Policy.CanViewPayPeriodAdmin,
        };
        foreach (var p in accountantOrAbove)
            Assert.Equal(PhysicalPolicy.AccountantOrAbove, p.ToPhysicalPolicy());

        string[] managerOrAbove =
        {
            Policy.CanCreatePayConfig, Policy.CanUpdatePayConfig, Policy.CanDeletePayConfig,
        };
        foreach (var p in managerOrAbove)
            Assert.Equal(PhysicalPolicy.ManagerOrAbove, p.ToPhysicalPolicy());

        string[] employeeOrAdmin =
        {
            Policy.CanViewPagedInvoices, Policy.CanViewPeriodPays, Policy.CanViewPayPeriods,
        };
        foreach (var p in employeeOrAdmin)
            Assert.Equal(PhysicalPolicy.EmployeeOrAdmin, p.ToPhysicalPolicy());
    }
}
