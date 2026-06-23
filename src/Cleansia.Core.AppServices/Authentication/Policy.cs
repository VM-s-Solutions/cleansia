namespace Cleansia.Core.AppServices.Authentication;

public class Policy
{
    // Code
    public const string CanViewCodeOverview = nameof(CanViewCodeOverview); // Anonymous

    // Global Search
    public const string CanPerformGlobalSearch = nameof(CanPerformGlobalSearch); // Anonymous

    // Order
    public const string CanViewPagedOrder = nameof(CanViewPagedOrder); // Admin + Employee
    public const string CanViewPagedUserOrder = nameof(CanViewPagedUserOrder); // Authenticated (All roles)
    public const string CanViewOrderDetail = nameof(CanViewOrderDetail); // Authenticated (All roles) + Admin + Employee
    public const string CanViewOrderDetailWithOrderNumberAndEmail = nameof(CanViewOrderDetailWithOrderNumberAndEmail); // Anonymous
    public const string CanUpdateOrder = nameof(CanUpdateOrder); // Admin + Employee
    public const string CanCreateOrder = nameof(CanCreateOrder); // Anonymous
    public const string CanGetOrderStatus = nameof(CanGetOrderStatus); // Anonymous
    public const string CanTakeOrder = nameof(CanTakeOrder); // Employee
    public const string CanStartOrder = nameof(CanStartOrder); // Employee
    public const string CanCompleteOrder = nameof(CanCompleteOrder); // Employee
    public const string CanUploadOrderPhoto = nameof(CanUploadOrderPhoto); // Employee
    public const string CanViewOrderPhotos = nameof(CanViewOrderPhotos); // Authenticated (All roles)
    public const string CanDeleteOrderPhoto = nameof(CanDeleteOrderPhoto); // Employee
    public const string CanAddOrderNote = nameof(CanAddOrderNote); // Employee
    public const string CanUpdateOrderNote = nameof(CanUpdateOrderNote); // Employee (own notes)
    public const string CanDeleteOrderNote = nameof(CanDeleteOrderNote); // Employee (own notes)
    public const string CanReportOrderIssue = nameof(CanReportOrderIssue); // Employee
    public const string CanUpdateOrderIssue = nameof(CanUpdateOrderIssue); // Employee (own issues)
    public const string CanDeleteOrderIssue = nameof(CanDeleteOrderIssue); // Employee (own issues)
    public const string CanSubmitOrderReview = nameof(CanSubmitOrderReview); // Customer
    public const string CanViewOrderReview = nameof(CanViewOrderReview); // Authenticated (All roles)
    public const string CanCancelOrder = nameof(CanCancelOrder); // Customer (own orders)
    public const string CanAdminCancelOrder = nameof(CanAdminCancelOrder); // Admin (any order)
    public const string CanOverrideOrderStatus = nameof(CanOverrideOrderStatus); // Admin (any order)
    public const string CanReassignOrder = nameof(CanReassignOrder); // Admin (any order)
    public const string CanRefundOrder = nameof(CanRefundOrder); // Admin (any order)

    // Saved addresses (Customer)
    public const string CanManageSavedAddresses = nameof(CanManageSavedAddresses); // Customer

    // Memberships / Cleansia Plus (Customer) — covers subscribe, cancel,
    // and "what's my Plus status?" reads. Same role gate as saved addresses.
    public const string CanManageMembership = nameof(CanManageMembership); // Customer

    // Recurring booking templates (Customer) — Plus perk; same role gate.
    // Backend doesn't enforce Plus here, just the customer role: the picker
    // is hidden in the UI for non-Plus, but the endpoint accepts any customer
    // so admin support tools can manage any user's templates if needed.
    public const string CanManageRecurringBookings = nameof(CanManageRecurringBookings); // Customer

    // User
    public const string CanViewPagedUser = nameof(CanViewPagedUser); // Admin + Employee
    public const string CanViewUserDetail = nameof(CanViewUserDetail); // Authenticated (All roles) + Admin + Employee
    public const string CanGetCurrentUser = nameof(CanGetCurrentUser); // Authenticated (All roles)
    public const string CanRequestPasswordChange = nameof(CanRequestPasswordChange); // Authenticated (All roles)
    public const string CanChangePassword = nameof(CanChangePassword);
    public const string CanChangeOwnPassword = nameof(CanChangeOwnPassword); // Authenticated [OWN-DATA] — subject id from the JWT only
    public const string CanUpdateCurrentUser = nameof(CanUpdateCurrentUser); // Authenticated (All roles)
    public const string CanAddPhoneNumber = nameof(CanAddPhoneNumber); // Authenticated (All roles)

    // Employee
    public const string CanGetCurrentEmployee = nameof(CanGetCurrentEmployee); // Authenticated (All roles)
    public const string CanCheckCurrentEmployee = nameof(CanCheckCurrentEmployee); // Authenticated (All roles)
    public const string CanUpdateCurrentEmployee = nameof(CanUpdateCurrentEmployee); // Authenticated (All roles)
    public const string CanViewPagedEmployee = nameof(CanViewPagedEmployee); // Admin
    public const string CanApproveEmployee = nameof(CanApproveEmployee); // Admin
    public const string CanRejectEmployee = nameof(CanRejectEmployee); // Admin
    public const string CanAdminUpdateEmployee = nameof(CanAdminUpdateEmployee); // Admin

    // Employee Documents
    public const string CanViewEmployeeDocuments = nameof(CanViewEmployeeDocuments); // Admin + Employee (own documents)
    public const string CanUploadEmployeeDocument = nameof(CanUploadEmployeeDocument); // Admin + Employee (own documents)
    public const string CanDownloadEmployeeDocument = nameof(CanDownloadEmployeeDocument); // Admin + Employee (own documents)
    public const string CanApproveEmployeeDocument = nameof(CanApproveEmployeeDocument); // Admin
    public const string CanRejectEmployeeDocument = nameof(CanRejectEmployeeDocument); // Admin
    public const string CanDeleteEmployeeDocument = nameof(CanDeleteEmployeeDocument); // Admin + Employee (own documents)

    // Employee Payroll
    public const string CanViewPagedInvoices = nameof(CanViewPagedInvoices); // Admin
    public const string CanViewPeriodPays = nameof(CanViewPeriodPays); // Admin + Employee (own data)
    public const string CanCalculateOrderPay = nameof(CanCalculateOrderPay); // Admin
    public const string CanGenerateInvoice = nameof(CanGenerateInvoice); // Admin
    public const string CanApproveInvoice = nameof(CanApproveInvoice); // Admin
    public const string CanMarkInvoicePaid = nameof(CanMarkInvoicePaid); // Admin
    public const string CanCancelInvoice = nameof(CanCancelInvoice); // Admin
    public const string CanClosePayPeriod = nameof(CanClosePayPeriod); // Admin
    public const string CanUpdateInvoiceAmounts = nameof(CanUpdateInvoiceAmounts); // Admin
    public const string CanDisputeInvoice = nameof(CanDisputeInvoice); // Admin
    public const string CanRejectInvoice = nameof(CanRejectInvoice); // Admin

    // Pay Period
    public const string CanViewPayPeriods = nameof(CanViewPayPeriods); // Admin + Employee
    public const string CanViewPayPeriod = nameof(CanViewPayPeriod); // Admin + Employee
    public const string CanCreatePayPeriod = nameof(CanCreatePayPeriod); // Admin
    public const string CanUpdatePayPeriod = nameof(CanUpdatePayPeriod); // Admin
    public const string CanOpenPayPeriod = nameof(CanOpenPayPeriod); // Admin
    public const string CanDeletePayPeriod = nameof(CanDeletePayPeriod); // Admin
    public const string CanMarkPayPeriodPaid = nameof(CanMarkPayPeriodPaid); // Admin
    public const string CanReopenPayPeriod = nameof(CanReopenPayPeriod); // Admin

    // Pay Config
    public const string CanViewPayConfigs = nameof(CanViewPayConfigs); // Admin
    public const string CanViewPayConfig = nameof(CanViewPayConfig); // Admin
    public const string CanCreatePayConfig = nameof(CanCreatePayConfig); // Admin
    public const string CanUpdatePayConfig = nameof(CanUpdatePayConfig); // Admin
    public const string CanDeletePayConfig = nameof(CanDeletePayConfig); // Admin

    // Dispute
    public const string CanCreateDispute = nameof(CanCreateDispute); // Authenticated (Customers can create disputes)
    public const string CanViewDispute = nameof(CanViewDispute); // Authenticated (Users can view their own disputes)
    public const string CanViewDisputeList = nameof(CanViewDisputeList); // Authenticated (Users can view their dispute list)
    // Customer self-reply on their own dispute (IsStaffMessage=false). Split from CanRespondToDispute
    // per ADR-0001 D2 Note C — the customer path must stay CustomerOnly [OWN-DATA].
    public const string CanAddDisputeMessage = nameof(CanAddDisputeMessage); // Customer (own dispute)
    public const string CanRespondToDispute = nameof(CanRespondToDispute); // Admin (staff reply only — IsStaffMessage=true)
    public const string CanResolveDispute = nameof(CanResolveDispute); // Admin (Only admins can resolve disputes)
    public const string CanUpdateDisputeStatus = nameof(CanUpdateDisputeStatus); // Admin (Only admins can update status)
    public const string CanUploadDisputeEvidence = nameof(CanUploadDisputeEvidence); // Customer (Customers can upload evidence to their own disputes)
    // Admin-host dispute reads. Distinct from the CustomerOnly own-data CanViewDispute/CanViewDisputeList:
    // the admin reads every dispute, so the admin host needs its own AdminOnly view gates.
    public const string CanViewDisputeAdmin = nameof(CanViewDisputeAdmin); // Admin (any dispute)
    public const string CanViewDisputeListAdmin = nameof(CanViewDisputeListAdmin); // Admin (all disputes)

    // Reports
    public const string CanViewRevenueReport = nameof(CanViewRevenueReport); // Admin
    public const string CanViewPayrollReport = nameof(CanViewPayrollReport); // Admin

    // Fiscal
    public const string CanManageFiscalFailures = nameof(CanManageFiscalFailures); // Admin

    // Services
    public const string CanViewServices = nameof(CanViewServices); // Admin
    public const string CanCreateService = nameof(CanCreateService); // Admin
    public const string CanUpdateService = nameof(CanUpdateService); // Admin
    public const string CanDeleteService = nameof(CanDeleteService); // Admin

    // Packages
    public const string CanViewPackages = nameof(CanViewPackages); // Admin
    public const string CanCreatePackage = nameof(CanCreatePackage); // Admin
    public const string CanUpdatePackage = nameof(CanUpdatePackage); // Admin
    public const string CanDeletePackage = nameof(CanDeletePackage); // Admin

    // Languages
    public const string CanViewLanguages = nameof(CanViewLanguages); // Admin
    public const string CanCreateLanguage = nameof(CanCreateLanguage); // Admin
    public const string CanUpdateLanguage = nameof(CanUpdateLanguage); // Admin
    public const string CanDeleteLanguage = nameof(CanDeleteLanguage); // Admin

    // Countries
    public const string CanViewCountries = nameof(CanViewCountries); // Admin
    public const string CanCreateCountry = nameof(CanCreateCountry); // Admin
    public const string CanUpdateCountry = nameof(CanUpdateCountry); // Admin
    public const string CanDeleteCountry = nameof(CanDeleteCountry); // Admin

    // Service areas (cities)
    public const string CanViewServiceCities = nameof(CanViewServiceCities); // Admin
    public const string CanManageServiceCities = nameof(CanManageServiceCities); // Admin

    // Currencies
    public const string CanViewCurrencies = nameof(CanViewCurrencies); // Admin
    public const string CanCreateCurrency = nameof(CanCreateCurrency); // Admin
    public const string CanUpdateCurrency = nameof(CanUpdateCurrency); // Admin
    public const string CanDeleteCurrency = nameof(CanDeleteCurrency); // Admin

    // Admin Users
    public const string CanViewAdminUsers = nameof(CanViewAdminUsers); // SuperAdmin
    public const string CanCreateAdminUser = nameof(CanCreateAdminUser); // SuperAdmin
    public const string CanUpdateAdminUser = nameof(CanUpdateAdminUser); // SuperAdmin
    public const string CanDeactivateAdminUser = nameof(CanDeactivateAdminUser); // SuperAdmin
    public const string CanActivateAdminUser = nameof(CanActivateAdminUser); // SuperAdmin

    // Company Info
    public const string CanViewCompanyInfo = nameof(CanViewCompanyInfo); // Admin
    public const string CanCreateCompanyInfo = nameof(CanCreateCompanyInfo); // Admin
    public const string CanUpdateCompanyInfo = nameof(CanUpdateCompanyInfo); // Admin
    public const string CanDeleteCompanyInfo = nameof(CanDeleteCompanyInfo); // Admin

    // Email Templates
    public const string CanViewEmailTemplates = nameof(CanViewEmailTemplates); // Admin
    public const string CanUpdateEmailTemplate = nameof(CanUpdateEmailTemplate); // Admin

    // Feature Flags
    public const string CanViewFeatureFlags = nameof(CanViewFeatureFlags); // Admin
    public const string CanCreateFeatureFlag = nameof(CanCreateFeatureFlag); // Admin
    public const string CanToggleFeatureFlag = nameof(CanToggleFeatureFlag); // Admin
    public const string CanDeleteFeatureFlag = nameof(CanDeleteFeatureFlag); // Admin
    public const string CanCheckFeatureFlag = nameof(CanCheckFeatureFlag); // Authenticated (All roles)

    // Country Configuration
    public const string CanViewCountryConfigurations = nameof(CanViewCountryConfigurations); // Admin
    public const string CanCreateCountryConfiguration = nameof(CanCreateCountryConfiguration); // Admin
    public const string CanUpdateCountryConfiguration = nameof(CanUpdateCountryConfiguration); // Admin
    public const string CanDeleteCountryConfiguration = nameof(CanDeleteCountryConfiguration); // Admin

    // Tenant Configuration
    public const string CanViewTenantConfigurations = nameof(CanViewTenantConfigurations); // Admin
    public const string CanCreateTenantConfiguration = nameof(CanCreateTenantConfiguration); // Admin
    public const string CanUpdateTenantConfiguration = nameof(CanUpdateTenantConfiguration); // Admin
    public const string CanDeleteTenantConfiguration = nameof(CanDeleteTenantConfiguration); // Admin

    // Device
    public const string Authenticated = nameof(Authenticated); // Authenticated (All roles)

    // GDPR
    public const string CanExportOwnData = nameof(CanExportOwnData); // Authenticated (All roles)
    public const string CanDeleteOwnAccount = nameof(CanDeleteOwnAccount); // Authenticated (All roles)
    public const string CanGrantConsent = nameof(CanGrantConsent); // Authenticated (All roles)
    public const string CanWithdrawConsent = nameof(CanWithdrawConsent); // Authenticated (All roles)
    public const string CanViewOwnConsents = nameof(CanViewOwnConsents); // Authenticated (All roles)

    // Admin GDPR
    public const string CanAdminExportUserData = nameof(CanAdminExportUserData); // Admin
    public const string CanAdminDeleteUserAccount = nameof(CanAdminDeleteUserAccount); // Admin
    public const string CanAdminViewUserConsents = nameof(CanAdminViewUserConsents); // Admin
    public const string CanViewGdprRequests = nameof(CanViewGdprRequests); // Admin

    // Loyalty
    public const string CanViewMyLoyalty = nameof(CanViewMyLoyalty); // Customer (own loyalty account)

    // Promo codes
    public const string CanRedeemPromoCode = nameof(CanRedeemPromoCode); // Customer

    // Referrals
    public const string CanViewMyReferral = nameof(CanViewMyReferral); // Customer (own referral code + invitees)

    // Admin Promo Codes
    public const string CanViewPromoCodes = nameof(CanViewPromoCodes); // Admin
    public const string CanCreatePromoCode = nameof(CanCreatePromoCode); // Admin
    public const string CanUpdatePromoCode = nameof(CanUpdatePromoCode); // Admin
    public const string CanDeactivatePromoCode = nameof(CanDeactivatePromoCode); // Admin

    // Admin Loyalty Tier Configs
    public const string CanViewLoyaltyTierConfigs = nameof(CanViewLoyaltyTierConfigs); // Admin
    public const string CanUpdateLoyaltyTierConfig = nameof(CanUpdateLoyaltyTierConfig); // Admin

    // Admin Loyalty (manual grants + user inspection)
    public const string CanGrantLoyaltyPoints = nameof(CanGrantLoyaltyPoints); // Admin
    public const string CanViewUserLoyalty = nameof(CanViewUserLoyalty); // Admin

    // Admin Membership Plans
    public const string CanViewMembershipPlans = nameof(CanViewMembershipPlans); // Admin
    public const string CanCreateMembershipPlan = nameof(CanCreateMembershipPlan); // Admin
    public const string CanUpdateMembershipPlan = nameof(CanUpdateMembershipPlan); // Admin
    public const string CanDeactivateMembershipPlan = nameof(CanDeactivateMembershipPlan); // Admin

    // Admin Referrals
    public const string CanViewReferrals = nameof(CanViewReferrals); // Admin
    public const string CanInterveneReferral = nameof(CanInterveneReferral); // Admin (reverse / force-qualify)

    // Marketing (sitewide push)
    public const string CanSendSitewidePromo = nameof(CanSendSitewidePromo); // Admin

    // Refunds (admin-issued partial refund — money-out + privileged)
    public const string CanIssueRefund = nameof(CanIssueRefund); // Admin

    // Admin Action Audit Log (read surface — ADR-0012 D7)
    public const string CanViewAuditLog = nameof(CanViewAuditLog); // Admin
}
