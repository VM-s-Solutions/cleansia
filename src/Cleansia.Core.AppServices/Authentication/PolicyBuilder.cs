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
        [Policy.CanReportOrderIssue] = PhysicalPolicy.Authenticated,
        [Policy.CanSubmitOrderReview] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewOrderReview] = PhysicalPolicy.Authenticated,
        [Policy.CanCancelOrder] = PhysicalPolicy.CustomerOnly,

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

        // Dispute
        [Policy.CanCreateDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDispute] = PhysicalPolicy.CustomerOnly,
        [Policy.CanViewDisputeList] = PhysicalPolicy.CustomerOnly,
        [Policy.CanRespondToDispute] = PhysicalPolicy.Authenticated,
        [Policy.CanResolveDispute] = PhysicalPolicy.AdminOnly,
        [Policy.CanUpdateDisputeStatus] = PhysicalPolicy.AdminOnly,
        [Policy.CanUploadDisputeEvidence] = PhysicalPolicy.CustomerOnly,

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

        // Admin Referrals
        [Policy.CanViewReferrals] = PhysicalPolicy.AdminOnly,
    };

    public static string ToPhysicalPolicy(this string permission) =>
        Map.GetValueOrDefault(permission, PhysicalPolicy.Authenticated);
}