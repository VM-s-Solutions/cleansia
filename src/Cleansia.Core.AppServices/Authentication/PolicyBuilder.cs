using System.Reflection;

namespace Cleansia.Core.AppServices.Authentication;

public static class PolicyBuilder
{

    private static readonly Dictionary<string, string> Map = new()
    {
        // Code
        //[Policy.CanViewCodeOverview] = PhysicalPolicy.Anonymous,

        // Global Search
        //[Policy.CanPerformGlobalSearch] = PhysicalPolicy.Anonymous,

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
        [Policy.CanAdminCancelOrder] = PhysicalPolicy.AdminOnly,
        [Policy.CanOverrideOrderStatus] = PhysicalPolicy.AdminOnly,
        [Policy.CanReassignOrder] = PhysicalPolicy.AdminOnly,
        [Policy.CanRefundOrder] = PhysicalPolicy.AdminOnly,

        // Saved addresses
        [Policy.CanManageSavedAddresses] = PhysicalPolicy.CustomerOnly,

        // Cleansia Plus membership — subscribe / cancel / read own status
        [Policy.CanManageMembership] = PhysicalPolicy.CustomerOnly,

        // Recurring booking templates — Plus perk, customer-only
        [Policy.CanManageRecurringBookings] = PhysicalPolicy.CustomerOnly,
        //[Policy.CanCreateOrder] = PhysicalPolicy.Anonymous,
        //[Policy.CanGetOrderStatus] = PhysicalPolicy.Anonymous,

        // User
        [Policy.CanViewPagedUser] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewUserDetail] = PhysicalPolicy.OwnerOrElevated,
        [Policy.CanGetCurrentUser] = PhysicalPolicy.Authenticated,
        //[Policy.CanRequestPasswordChange] = PhysicalPolicy.Anonymous,
        //[Policy.CanChangePassword] = PhysicalPolicy.Anonymous,
        // Authenticated change-own-password (distinct from the anonymous email-link reset above).
        // [OWN-DATA]: the handler takes the subject id from the session/JWT only.
        [Policy.CanChangeOwnPassword] = PhysicalPolicy.Authenticated,
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

        // Employee Payroll — Invoices (was unmapped → fail-open; now closed)
        // CanViewPagedInvoices/CanViewPeriodPays gate an employee's OWN pay on the Partner host
        // (Note A): EmployeeOrAdmin + handler ownership scoping ([OWN-DATA]). AdminOnly would 403
        // every cleaner from their own invoices.
        [Policy.CanViewPagedInvoices] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewPeriodPays] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanCalculateOrderPay] = PhysicalPolicy.AdminOnly,
        [Policy.CanGenerateInvoice] = PhysicalPolicy.AdminOnly,
        [Policy.CanApproveInvoice] = PhysicalPolicy.AdminOnly,
        [Policy.CanMarkInvoicePaid] = PhysicalPolicy.AdminOnly,
        [Policy.CanCancelInvoice] = PhysicalPolicy.AdminOnly,
        [Policy.CanClosePayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateInvoiceAmounts] = PhysicalPolicy.AdminOnly,
        [Policy.CanDisputeInvoice] = PhysicalPolicy.AdminOnly,
        [Policy.CanRejectInvoice] = PhysicalPolicy.AdminOnly,

        // Employee Payroll — Pay Periods. Pay periods are global cycles (Note B), so an
        // employee may list them to locate their own pay; the per-row data is fetched via the
        // [OWN-DATA] CanViewPeriodPays path. Mutations are admin-only.
        [Policy.CanViewPayPeriods] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewPayPeriod] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanCreatePayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdatePayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanOpenPayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeletePayPeriod] = PhysicalPolicy.AdminOnly,
        [Policy.CanMarkPayPeriodPaid] = PhysicalPolicy.AdminOnly,
        [Policy.CanReopenPayPeriod] = PhysicalPolicy.AdminOnly,

        // Employee Payroll — Pay Config. Admin-only configuration surface.
        [Policy.CanViewPayConfigs] = PhysicalPolicy.AdminOnly,
        [Policy.CanViewPayConfig] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreatePayConfig] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdatePayConfig] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeletePayConfig] = PhysicalPolicy.AdminOnly,

        // Dispute
        [Policy.CanCreateDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDisputeList] = PhysicalPolicy.CustomerOnly,
        // Dispute reply split (Note C): the customer self-reply path is CustomerOnly [OWN-DATA]; the staff
        // reply path (CanRespondToDispute) is AdminOnly (was the fail-open Authenticated).
        [Policy.CanAddDisputeMessage] = PhysicalPolicy.CustomerOnly,
        [Policy.CanRespondToDispute] = PhysicalPolicy.AdminOnly,
        [Policy.CanResolveDispute] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateDisputeStatus] = PhysicalPolicy.AdminOnly,
        [Policy.CanUploadDisputeEvidence] = PhysicalPolicy.CustomerOnly,
        // Admin-host dispute reads (D-01 admin dispute management). The own-data view gates above stay
        // CustomerOnly; these are the AdminOnly all-disputes reads for the Admin host.
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

        // GDPR
        [Policy.CanExportOwnData] = PhysicalPolicy.Authenticated,
        [Policy.CanDeleteOwnAccount] = PhysicalPolicy.Authenticated,
        [Policy.CanGrantConsent] = PhysicalPolicy.Authenticated,
        [Policy.CanWithdrawConsent] = PhysicalPolicy.Authenticated,
        [Policy.CanViewOwnConsents] = PhysicalPolicy.Authenticated,

        // Admin GDPR
        [Policy.CanAdminExportUserData] = PhysicalPolicy.AdminOnly,
        [Policy.CanAdminDeleteUserAccount] = PhysicalPolicy.AdminOnly,
        [Policy.CanAdminViewUserConsents] = PhysicalPolicy.AdminOnly,
        [Policy.CanViewGdprRequests] = PhysicalPolicy.AdminOnly,

        // Loyalty
        [Policy.CanViewMyLoyalty] = PhysicalPolicy.CustomerOnly,

        // Promo codes
        [Policy.CanRedeemPromoCode] = PhysicalPolicy.CustomerOnly,

        // Referrals
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

        // Admin Membership Plans
        [Policy.CanViewMembershipPlans] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateMembershipPlan] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateMembershipPlan] = PhysicalPolicy.AdminOnly,
        [Policy.CanDeactivateMembershipPlan] = PhysicalPolicy.AdminOnly,

        // Admin Referrals
        [Policy.CanViewReferrals] = PhysicalPolicy.AdminOnly,
        [Policy.CanInterveneReferral] = PhysicalPolicy.AdminOnly,

        // Marketing (sitewide push)
        [Policy.CanSendSitewidePromo] = PhysicalPolicy.AdminOnly,

        // Refunds (admin-issued partial refund) — ADR-0001 D2
        [Policy.CanIssueRefund] = PhysicalPolicy.AdminOnly,
    };

    /// <summary>
    /// The single sanctioned place a <see cref="Policy"/> constant may have no <see cref="Map"/>
    /// entry: it gates ONLY <c>[AllowAnonymous]</c> routes. This set is frozen by ADR-0001 §D1.2 and
    /// proven exhaustive against the host controllers by verification #1b. Adding a new genuinely
    /// anonymous permission means adding it here (a reviewable [AllowAnonymous] decision); any other
    /// new permission must go into <see cref="Map"/> or boot fails via <see cref="AssertComplete"/>.
    /// </summary>
    public static readonly IReadOnlySet<string> AnonymousAllowList = new HashSet<string>
    {
        Policy.CanViewCodeOverview,
        Policy.CanPerformGlobalSearch,
        Policy.CanViewOrderDetailWithOrderNumberAndEmail,
        Policy.CanCreateOrder,
        Policy.CanGetOrderStatus,
        Policy.CanRequestPasswordChange,
        Policy.CanChangePassword,
    };

    /// <summary>
    /// Fail-closed translation: an unmapped permission resolves to the always-deny sentinel, never
    /// to "any authenticated user". This is the runtime backstop in case <see cref="AssertComplete"/>
    /// is ever bypassed (ADR-0001 §D1.1).
    /// </summary>
    public static string ToPhysicalPolicy(this string permission) =>
        Map.GetValueOrDefault(permission, PhysicalPolicy.Deny);

    /// <summary>
    /// Pure static reflection check (no DI) asserting the permission seam is complete and consistent.
    /// Run at host startup (and in CI/unit tests) so an unmapped permission can never reach prod —
    /// a developer who adds a <see cref="Policy"/> constant and forgets to map it gets a boot failure
    /// listing every gap. Implements ADR-0001 §D1.2 exactly.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a declared permission is neither mapped nor allow-listed, when the map references
    /// an unknown permission, or when a permission is both allow-listed and mapped.
    /// </exception>
    public static void AssertComplete()
    {
        var declared = typeof(Policy)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        var anonymous = AnonymousAllowList;

        var missing = declared.Except(Map.Keys).Except(anonymous).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                "Authorization map incomplete. Every Policy.* constant must be mapped " +
                "(or listed in AnonymousAllowList). Missing: " + string.Join(", ", missing));

        var orphans = Map.Keys.Except(declared).ToList();
        if (orphans.Count > 0)
            throw new InvalidOperationException(
                "Authorization map references unknown permissions: " + string.Join(", ", orphans));

        var contradictions = anonymous.Intersect(Map.Keys).ToList();
        if (contradictions.Count > 0)
            throw new InvalidOperationException(
                "Permissions are both in the AnonymousAllowList and the Map: " +
                string.Join(", ", contradictions));
    }
}