namespace Cleansia.Core.AppServices.Authentication;

public class Policy
{
    public const string CanViewOrderCustomer = nameof(CanViewOrderCustomer);
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
    public const string CanAdminCancelOrder = nameof(CanAdminCancelOrder); // SupportOrAbove (any order)
    public const string CanOverrideOrderStatus = nameof(CanOverrideOrderStatus); // SupportOrAbove (any order)
    public const string CanReassignOrder = nameof(CanReassignOrder); // SupportOrAbove (any order)
    public const string CanRefundOrder = nameof(CanRefundOrder); // SupportOrAbove (any order)
    // Admin-host reads split from the shared partner-host reads above (ADR-0066 D3): the partner hosts keep
    // their meaning byte-for-byte while the administrator's role gates the customer's PII here.
    public const string CanViewPagedOrderAdmin = nameof(CanViewPagedOrderAdmin); // SupportOrAbove (every order of the company)
    public const string CanViewOrderDetailAdmin = nameof(CanViewOrderDetailAdmin); // SupportOrAbove (the unredacted detail)
    public const string CanViewOrderPhotosAdmin = nameof(CanViewOrderPhotosAdmin); // SupportOrAbove (the customer's home)

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
    public const string CanApproveEmployee = nameof(CanApproveEmployee); // SupportOrAbove
    public const string CanRejectEmployee = nameof(CanRejectEmployee); // SupportOrAbove
    public const string CanAdminUpdateEmployee = nameof(CanAdminUpdateEmployee); // SupportOrAbove
    public const string CanViewEmployeePayoutDetails = nameof(CanViewEmployeePayoutDetails); // Admin (masked) + Employee (own)
    public const string CanViewEmployeePayoutDetailsAdmin = nameof(CanViewEmployeePayoutDetailsAdmin); // AccountantOrAbove (masked)
    public const string CanRevealEmployeePayoutDetails = nameof(CanRevealEmployeePayoutDetails); // ManagerOrAbove
    public const string CanRevealOrderAccessInstructions = nameof(CanRevealOrderAccessInstructions); // SupportOrAbove

    // Employee Documents
    public const string CanViewEmployeeDocuments = nameof(CanViewEmployeeDocuments); // Admin + Employee (own documents)
    public const string CanUploadEmployeeDocument = nameof(CanUploadEmployeeDocument); // Admin + Employee (own documents)
    public const string CanDownloadEmployeeDocument = nameof(CanDownloadEmployeeDocument); // Admin + Employee (own documents)
    public const string CanApproveEmployeeDocument = nameof(CanApproveEmployeeDocument); // SupportOrAbove
    public const string CanRejectEmployeeDocument = nameof(CanRejectEmployeeDocument); // SupportOrAbove
    public const string CanDeleteEmployeeDocument = nameof(CanDeleteEmployeeDocument); // Admin + Employee (own documents)
    public const string CanViewEmployeeDocumentsAdmin = nameof(CanViewEmployeeDocumentsAdmin); // SupportOrAbove (any cleaner's identity documents)

    // Employee Payroll
    public const string CanViewPagedInvoices = nameof(CanViewPagedInvoices); // Admin
    public const string CanViewPagedInvoicesAdmin = nameof(CanViewPagedInvoicesAdmin); // AccountantOrAbove (every cleaner's payout invoices)
    public const string CanViewPeriodPays = nameof(CanViewPeriodPays); // Admin + Employee (own data)
    public const string CanCalculateOrderPay = nameof(CanCalculateOrderPay); // Admin
    public const string CanGenerateInvoice = nameof(CanGenerateInvoice); // AccountantOrAbove
    public const string CanApproveInvoice = nameof(CanApproveInvoice); // AccountantOrAbove
    public const string CanMarkInvoicePaid = nameof(CanMarkInvoicePaid); // AccountantOrAbove
    public const string CanCancelInvoice = nameof(CanCancelInvoice); // AccountantOrAbove
    public const string CanClosePayPeriod = nameof(CanClosePayPeriod); // AccountantOrAbove
    public const string CanUpdateInvoiceAmounts = nameof(CanUpdateInvoiceAmounts); // AccountantOrAbove
    public const string CanDisputeInvoice = nameof(CanDisputeInvoice); // AccountantOrAbove
    public const string CanRejectInvoice = nameof(CanRejectInvoice); // AccountantOrAbove

    // Pay Period
    public const string CanViewPayPeriods = nameof(CanViewPayPeriods); // Admin + Employee
    public const string CanViewPayPeriod = nameof(CanViewPayPeriod); // Admin + Employee
    public const string CanViewPayPeriodsAdmin = nameof(CanViewPayPeriodsAdmin); // AccountantOrAbove
    public const string CanViewPayPeriodAdmin = nameof(CanViewPayPeriodAdmin); // AccountantOrAbove
    public const string CanCreatePayPeriod = nameof(CanCreatePayPeriod); // AccountantOrAbove
    public const string CanUpdatePayPeriod = nameof(CanUpdatePayPeriod); // AccountantOrAbove
    public const string CanOpenPayPeriod = nameof(CanOpenPayPeriod); // AccountantOrAbove
    public const string CanDeletePayPeriod = nameof(CanDeletePayPeriod); // AccountantOrAbove
    public const string CanMarkPayPeriodPaid = nameof(CanMarkPayPeriodPaid); // AccountantOrAbove
    public const string CanReopenPayPeriod = nameof(CanReopenPayPeriod); // AccountantOrAbove

    // Pay Config
    public const string CanViewPayConfigs = nameof(CanViewPayConfigs); // AccountantOrAbove
    public const string CanViewPayConfig = nameof(CanViewPayConfig); // AccountantOrAbove
    public const string CanCreatePayConfig = nameof(CanCreatePayConfig); // ManagerOrAbove
    public const string CanUpdatePayConfig = nameof(CanUpdatePayConfig); // ManagerOrAbove
    public const string CanDeletePayConfig = nameof(CanDeletePayConfig); // ManagerOrAbove

    // Dispute
    public const string CanCreateDispute = nameof(CanCreateDispute); // Authenticated (Customers can create disputes)
    public const string CanViewDispute = nameof(CanViewDispute); // Authenticated (Users can view their own disputes)
    public const string CanViewDisputeList = nameof(CanViewDisputeList); // Authenticated (Users can view their dispute list)
    // Customer self-reply on their own dispute (IsStaffMessage=false). Split from CanRespondToDispute
    // per ADR-0001 D2 Note C — the customer path must stay CustomerOnly [OWN-DATA].
    public const string CanAddDisputeMessage = nameof(CanAddDisputeMessage); // Customer (own dispute)
    public const string CanRespondToDispute = nameof(CanRespondToDispute); // SupportOrAbove (staff reply only — IsStaffMessage=true)
    public const string CanResolveDispute = nameof(CanResolveDispute); // SupportOrAbove (Only admins can resolve disputes)
    public const string CanUpdateDisputeStatus = nameof(CanUpdateDisputeStatus); // SupportOrAbove (Only admins can update status)
    public const string CanUploadDisputeEvidence = nameof(CanUploadDisputeEvidence); // Customer (Customers can upload evidence to their own disputes)
    // Admin-host dispute reads. Distinct from the CustomerOnly own-data CanViewDispute/CanViewDisputeList:
    // the admin reads every dispute, so the admin host needs its own AdminOnly view gates.
    public const string CanViewDisputeAdmin = nameof(CanViewDisputeAdmin); // SupportOrAbove (any dispute)
    public const string CanViewDisputeListAdmin = nameof(CanViewDisputeListAdmin); // SupportOrAbove (all disputes)

    // Reports
    public const string CanViewRevenueReport = nameof(CanViewRevenueReport); // AccountantOrAbove
    public const string CanViewPayrollReport = nameof(CanViewPayrollReport); // AccountantOrAbove

    // Fiscal
    public const string CanManageFiscalFailures = nameof(CanManageFiscalFailures); // AccountantOrAbove

    // Services
    public const string CanViewServices = nameof(CanViewServices); // Admin
    public const string CanCreateService = nameof(CanCreateService); // ManagerOrAbove
    public const string CanUpdateService = nameof(CanUpdateService); // ManagerOrAbove
    public const string CanDeleteService = nameof(CanDeleteService); // ManagerOrAbove

    // Packages
    public const string CanViewPackages = nameof(CanViewPackages); // Admin
    public const string CanCreatePackage = nameof(CanCreatePackage); // ManagerOrAbove
    public const string CanUpdatePackage = nameof(CanUpdatePackage); // ManagerOrAbove
    public const string CanDeletePackage = nameof(CanDeletePackage); // ManagerOrAbove

    // Extras
    public const string CanViewExtras = nameof(CanViewExtras); // Admin
    public const string CanCreateExtra = nameof(CanCreateExtra); // ManagerOrAbove
    public const string CanUpdateExtra = nameof(CanUpdateExtra); // ManagerOrAbove
    public const string CanDeleteExtra = nameof(CanDeleteExtra); // ManagerOrAbove

    // Languages
    public const string CanViewLanguages = nameof(CanViewLanguages); // Admin
    public const string CanCreateLanguage = nameof(CanCreateLanguage); // ManagerOrAbove
    public const string CanUpdateLanguage = nameof(CanUpdateLanguage); // ManagerOrAbove
    public const string CanDeleteLanguage = nameof(CanDeleteLanguage); // ManagerOrAbove

    // Countries
    public const string CanViewCountries = nameof(CanViewCountries); // Admin
    public const string CanCreateCountry = nameof(CanCreateCountry); // ManagerOrAbove
    public const string CanUpdateCountry = nameof(CanUpdateCountry); // ManagerOrAbove
    public const string CanDeleteCountry = nameof(CanDeleteCountry); // ManagerOrAbove

    // Service areas (cities)
    public const string CanViewServiceCities = nameof(CanViewServiceCities); // Admin
    public const string CanManageServiceCities = nameof(CanManageServiceCities); // ManagerOrAbove

    // Currencies
    public const string CanViewCurrencies = nameof(CanViewCurrencies); // Admin
    public const string CanCreateCurrency = nameof(CanCreateCurrency); // ManagerOrAbove
    public const string CanUpdateCurrency = nameof(CanUpdateCurrency); // ManagerOrAbove
    public const string CanDeleteCurrency = nameof(CanDeleteCurrency); // ManagerOrAbove

    // Admin Users
    public const string CanViewAdminUsers = nameof(CanViewAdminUsers); // ManagerOrAbove
    public const string CanCreateAdminUser = nameof(CanCreateAdminUser); // AdministratorOnly
    public const string CanUpdateAdminUser = nameof(CanUpdateAdminUser); // AdministratorOnly
    public const string CanDeactivateAdminUser = nameof(CanDeactivateAdminUser); // AdministratorOnly
    public const string CanActivateAdminUser = nameof(CanActivateAdminUser); // AdministratorOnly
    public const string CanSetAdminRole = nameof(CanSetAdminRole); // AdministratorOnly (role assignment)

    // Company Info
    public const string CanViewCompanyInfo = nameof(CanViewCompanyInfo); // Admin
    public const string CanCreateCompanyInfo = nameof(CanCreateCompanyInfo); // ManagerOrAbove
    public const string CanUpdateCompanyInfo = nameof(CanUpdateCompanyInfo); // ManagerOrAbove
    public const string CanDeleteCompanyInfo = nameof(CanDeleteCompanyInfo); // ManagerOrAbove

    // Email Templates
    public const string CanViewEmailTemplates = nameof(CanViewEmailTemplates); // Admin
    public const string CanUpdateEmailTemplate = nameof(CanUpdateEmailTemplate); // ManagerOrAbove

    // Country Configuration
    public const string CanViewCountryConfigurations = nameof(CanViewCountryConfigurations); // Admin
    public const string CanCreateCountryConfiguration = nameof(CanCreateCountryConfiguration); // ManagerOrAbove
    public const string CanUpdateCountryConfiguration = nameof(CanUpdateCountryConfiguration); // ManagerOrAbove
    public const string CanDeleteCountryConfiguration = nameof(CanDeleteCountryConfiguration); // ManagerOrAbove

    // Legal documents (read-only; the texts in force per market)
    public const string CanViewLegalDocuments = nameof(CanViewLegalDocuments); // AdministratorOnly

    // Tenant Configuration
    public const string CanViewTenantConfigurations = nameof(CanViewTenantConfigurations); // AdministratorOnly
    public const string CanCreateTenantConfiguration = nameof(CanCreateTenantConfiguration); // AdministratorOnly
    public const string CanUpdateTenantConfiguration = nameof(CanUpdateTenantConfiguration); // AdministratorOnly
    public const string CanDeleteTenantConfiguration = nameof(CanDeleteTenantConfiguration); // AdministratorOnly

    // Company lifecycle (the admin's own operating company)
    public const string CanViewCompanyLifecycle = nameof(CanViewCompanyLifecycle); // AdministratorOnly
    public const string CanDeactivateCompany = nameof(CanDeactivateCompany); // AdministratorOnly
    public const string CanReactivateCompany = nameof(CanReactivateCompany); // AdministratorOnly
    public const string CanWindDownCompany = nameof(CanWindDownCompany); // AdministratorOnly
    public const string CanArchiveCompany = nameof(CanArchiveCompany); // AdministratorOnly

    // Device
    public const string Authenticated = nameof(Authenticated); // Authenticated (All roles)

    // GDPR
    public const string CanExportOwnData = nameof(CanExportOwnData); // Authenticated (All roles)
    public const string CanDeleteOwnAccount = nameof(CanDeleteOwnAccount); // Authenticated (All roles)
    public const string CanGrantConsent = nameof(CanGrantConsent); // Authenticated (All roles)
    public const string CanWithdrawConsent = nameof(CanWithdrawConsent); // Authenticated (All roles)
    public const string CanViewOwnConsents = nameof(CanViewOwnConsents); // Authenticated (All roles)

    // Admin GDPR
    public const string CanAdminExportUserData = nameof(CanAdminExportUserData); // SupportOrAbove
    public const string CanAdminDeleteUserAccount = nameof(CanAdminDeleteUserAccount); // ManagerOrAbove
    public const string CanAdminViewUserConsents = nameof(CanAdminViewUserConsents); // SupportOrAbove
    public const string CanViewGdprRequests = nameof(CanViewGdprRequests); // SupportOrAbove

    // Loyalty
    public const string CanViewMyLoyalty = nameof(CanViewMyLoyalty); // Customer (own loyalty account)

    // Promo codes
    public const string CanRedeemPromoCode = nameof(CanRedeemPromoCode); // Customer

    // Referrals
    public const string CanViewMyReferral = nameof(CanViewMyReferral); // Customer (own referral code + invitees)

    // Credit
    public const string CanViewMyCredit = nameof(CanViewMyCredit); // Customer (own credit balance)
    public const string CanIssueCustomerCredit = nameof(CanIssueCustomerCredit); // SupportOrAbove (money out)
    public const string CanViewUserCredit = nameof(CanViewUserCredit); // Admin (any customer balance + ledger)
    public const string CanExpireCustomerCredit = nameof(CanExpireCustomerCredit); // ManagerOrAbove (discharge a balance)

    // Admin Promo Codes
    public const string CanViewPromoCodes = nameof(CanViewPromoCodes); // Admin
    public const string CanCreatePromoCode = nameof(CanCreatePromoCode); // ManagerOrAbove
    public const string CanUpdatePromoCode = nameof(CanUpdatePromoCode); // ManagerOrAbove
    public const string CanDeactivatePromoCode = nameof(CanDeactivatePromoCode); // ManagerOrAbove

    // Admin Loyalty Tier Configs
    public const string CanViewLoyaltyTierConfigs = nameof(CanViewLoyaltyTierConfigs); // Admin
    public const string CanUpdateLoyaltyTierConfig = nameof(CanUpdateLoyaltyTierConfig); // ManagerOrAbove

    // Admin Loyalty (manual grants + user inspection)
    public const string CanGrantLoyaltyPoints = nameof(CanGrantLoyaltyPoints); // SupportOrAbove
    public const string CanViewUserLoyalty = nameof(CanViewUserLoyalty); // SupportOrAbove

    // Admin Membership Plans
    public const string CanViewMembershipPlans = nameof(CanViewMembershipPlans); // Admin
    public const string CanCreateMembershipPlan = nameof(CanCreateMembershipPlan); // ManagerOrAbove
    public const string CanUpdateMembershipPlan = nameof(CanUpdateMembershipPlan); // ManagerOrAbove
    public const string CanDeactivateMembershipPlan = nameof(CanDeactivateMembershipPlan); // ManagerOrAbove

    // Admin Referrals
    public const string CanViewReferrals = nameof(CanViewReferrals); // Admin
    public const string CanInterveneReferral = nameof(CanInterveneReferral); // SupportOrAbove (reverse / force-qualify)

    // Marketing (sitewide push)
    public const string CanSendSitewidePromo = nameof(CanSendSitewidePromo); // ManagerOrAbove

    // Refunds (admin-issued partial refund — money-out + privileged)
    public const string CanIssueRefund = nameof(CanIssueRefund); // SupportOrAbove

    // Admin Action Audit Log (read surface — ADR-0012 D7)
    public const string CanViewAuditLog = nameof(CanViewAuditLog); // SupportOrAbove

    // Admin notifications feed (the administrator's own rows)
    public const string CanViewAdminNotifications = nameof(CanViewAdminNotifications); // Admin
}
