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
        [Policy.CanAdminCancelOrder] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanOverrideOrderStatus] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanReassignOrder] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanRefundOrder] = PhysicalPolicy.SupportOrAbove,
        // Admin-host reads split from the shared partner-host reads (ADR-0066 D3): CanViewPagedOrder,
        // CanViewOrderDetail and CanViewOrderPhotos keep their partner-host meaning above; these three
        // are what the admin host routes, so the administrator's role gates the customer's PII.
        [Policy.CanViewPagedOrderAdmin] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewOrderDetailAdmin] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewOrderPhotosAdmin] = PhysicalPolicy.SupportOrAbove,

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

        // Employee
        [Policy.CanGetCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanCheckCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanUpdateCurrentEmployee] = PhysicalPolicy.Authenticated,
        [Policy.CanViewPagedEmployee] = PhysicalPolicy.AdminOnly,
        [Policy.CanApproveEmployee] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanRejectEmployee] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanAdminUpdateEmployee] = PhysicalPolicy.SupportOrAbove,
        // Masked by default for admins, own-record for a cleaner ([OWN-DATA], handler-scoped);
        // the unmasked value is a separate, audited, rate-limited admin command (ADR-0034 D8).
        [Policy.CanViewEmployeePayoutDetails] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewEmployeePayoutDetailsAdmin] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanRevealEmployeePayoutDetails] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanRevealOrderAccessInstructions] = PhysicalPolicy.SupportOrAbove,

        // Employee Documents
        [Policy.CanViewEmployeeDocuments] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanUploadEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanDownloadEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanApproveEmployeeDocument] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanRejectEmployeeDocument] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanDeleteEmployeeDocument] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewEmployeeDocumentsAdmin] = PhysicalPolicy.SupportOrAbove,

        // Employee Payroll — Invoices (was unmapped → fail-open; now closed)
        // CanViewPagedInvoices/CanViewPeriodPays gate an employee's OWN pay on the Partner host
        // (Note A): EmployeeOrAdmin + handler ownership scoping ([OWN-DATA]). AdminOnly would 403
        // every cleaner from their own invoices.
        [Policy.CanViewPagedInvoices] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewPeriodPays] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewPagedInvoicesAdmin] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanGenerateInvoice] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanApproveInvoice] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanMarkInvoicePaid] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanCancelInvoice] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanClosePayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanUpdateInvoiceAmounts] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanDisputeInvoice] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanRejectInvoice] = PhysicalPolicy.AccountantOrAbove,

        // Employee Payroll — Pay Periods. Pay periods are global cycles (Note B), so an
        // employee may list them to locate their own pay; the per-row data is fetched via the
        // [OWN-DATA] CanViewPeriodPays path. The admin host lists them on its own constants below;
        // mutations are the Accountant's.
        [Policy.CanViewPayPeriods] = PhysicalPolicy.EmployeeOrAdmin,
        [Policy.CanViewPayPeriodsAdmin] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanViewPayPeriodAdmin] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanCreatePayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanUpdatePayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanOpenPayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanDeletePayPeriod] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanMarkPayPeriodPaid] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanReopenPayPeriod] = PhysicalPolicy.AccountantOrAbove,

        // Employee Payroll — Pay Config. Readable by whoever books a pay row; a rate is company policy.
        [Policy.CanViewPayConfigs] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanViewPayConfig] = PhysicalPolicy.AccountantOrAbove,
        [Policy.CanCreatePayConfig] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdatePayConfig] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeletePayConfig] = PhysicalPolicy.ManagerOrAbove,

        // Dispute
        [Policy.CanCreateDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDisputeList] = PhysicalPolicy.CustomerOnly,
        // Dispute reply split (Note C): the customer self-reply path is CustomerOnly [OWN-DATA]; the staff
        // reply path (CanRespondToDispute) is Support's (was the fail-open Authenticated).
        [Policy.CanAddDisputeMessage] = PhysicalPolicy.CustomerOnly,
        [Policy.CanRespondToDispute] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanResolveDispute] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanUpdateDisputeStatus] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanUploadDisputeEvidence] = PhysicalPolicy.CustomerOnly,
        // Admin-host dispute reads (D-01 admin dispute management). The own-data view gates above stay
        // CustomerOnly; these are the all-disputes reads for the Admin host, Support's area.
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

        // Extras
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
        [Policy.CanSetAdminRole] = PhysicalPolicy.AdministratorOnly,

        // Company Info
        [Policy.CanViewCompanyInfo] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateCompanyInfo] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdateCompanyInfo] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeleteCompanyInfo] = PhysicalPolicy.ManagerOrAbove,

        // Email Templates
        [Policy.CanViewEmailTemplates] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateEmailTemplate] = PhysicalPolicy.ManagerOrAbove,

        // Legal documents
        [Policy.CanViewLegalDocuments] = PhysicalPolicy.AdministratorOnly,

        // Tenant Configuration
        [Policy.CanViewTenantConfigurations] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanUpdateTenantConfiguration] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanDeleteTenantConfiguration] = PhysicalPolicy.AdministratorOnly,

        // Company lifecycle
        [Policy.CanViewCompanyLifecycle] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanDeactivateCompany] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanReactivateCompany] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanWindDownCompany] = PhysicalPolicy.AdministratorOnly,
        [Policy.CanArchiveCompany] = PhysicalPolicy.AdministratorOnly,

        // Device
        [Policy.Authenticated] = PhysicalPolicy.Authenticated,

        // GDPR
        [Policy.CanExportOwnData] = PhysicalPolicy.Authenticated,
        [Policy.CanDeleteOwnAccount] = PhysicalPolicy.Authenticated,
        [Policy.CanGrantConsent] = PhysicalPolicy.Authenticated,
        [Policy.CanWithdrawConsent] = PhysicalPolicy.Authenticated,
        [Policy.CanViewOwnConsents] = PhysicalPolicy.Authenticated,

        // Admin GDPR
        [Policy.CanAdminExportUserData] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanAdminDeleteUserAccount] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanAdminViewUserConsents] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewGdprRequests] = PhysicalPolicy.SupportOrAbove,

        // Loyalty
        [Policy.CanViewMyLoyalty] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewMyCredit] = PhysicalPolicy.CustomerOnly,

        // Promo codes
        [Policy.CanRedeemPromoCode] = PhysicalPolicy.CustomerOnly,

        // Referrals
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

        // Admin Membership Plans
        [Policy.CanViewMembershipPlans] = PhysicalPolicy.AdminOnly,
        [Policy.CanCreateMembershipPlan] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanUpdateMembershipPlan] = PhysicalPolicy.ManagerOrAbove,
        [Policy.CanDeactivateMembershipPlan] = PhysicalPolicy.ManagerOrAbove,

        // Admin Referrals
        [Policy.CanViewReferrals] = PhysicalPolicy.AdminOnly,
        [Policy.CanInterveneReferral] = PhysicalPolicy.SupportOrAbove,

        // Marketing (sitewide push)
        [Policy.CanSendSitewidePromo] = PhysicalPolicy.ManagerOrAbove,

        // Refunds (admin-issued partial refund) — ADR-0001 D2
        [Policy.CanIssueRefund] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanIssueCustomerCredit] = PhysicalPolicy.SupportOrAbove,
        [Policy.CanViewUserCredit] = PhysicalPolicy.AdminOnly,
        [Policy.CanExpireCustomerCredit] = PhysicalPolicy.ManagerOrAbove,

        // Admin Action Audit Log (read surface — ADR-0012 D7)
        [Policy.CanViewAuditLog] = PhysicalPolicy.SupportOrAbove,

        // Admin notifications feed
        [Policy.CanViewAdminNotifications] = PhysicalPolicy.AdminOnly,
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
