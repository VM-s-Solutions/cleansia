import { PhysicalPolicy } from './physical-policy';

/**
 * Frontend mirror of `Cleansia.Core.AppServices.Authentication.Policy`.
 * Each constant names a logical permission; the directive resolves it to a
 * PhysicalPolicy via {@link POLICY_MAP} and checks the current user's role
 * against that gate.
 *
 * Every row of the backend `PolicyBuilder.Map` is here and nothing else is: the admin app's
 * `policy-map-mirror.spec.ts` diffs the two, so a backend row that moves or appears fails the
 * build until this file follows it. The seven anonymous permissions are not mapped on either side.
 */
export const Policy = {
  // Order
  CanViewPagedOrder: 'CanViewPagedOrder',
  CanViewPagedUserOrder: 'CanViewPagedUserOrder',
  CanViewOrderDetail: 'CanViewOrderDetail',
  CanViewOrderCustomer: 'CanViewOrderCustomer',
  CanUpdateOrder: 'CanUpdateOrder',
  CanTakeOrder: 'CanTakeOrder',
  CanStartOrder: 'CanStartOrder',
  CanCompleteOrder: 'CanCompleteOrder',
  CanUploadOrderPhoto: 'CanUploadOrderPhoto',
  CanViewOrderPhotos: 'CanViewOrderPhotos',
  CanDeleteOrderPhoto: 'CanDeleteOrderPhoto',
  CanAddOrderNote: 'CanAddOrderNote',
  CanUpdateOrderNote: 'CanUpdateOrderNote',
  CanDeleteOrderNote: 'CanDeleteOrderNote',
  CanReportOrderIssue: 'CanReportOrderIssue',
  CanUpdateOrderIssue: 'CanUpdateOrderIssue',
  CanDeleteOrderIssue: 'CanDeleteOrderIssue',
  CanSubmitOrderReview: 'CanSubmitOrderReview',
  CanViewOrderReview: 'CanViewOrderReview',
  CanCancelOrder: 'CanCancelOrder',
  CanAdminCancelOrder: 'CanAdminCancelOrder',
  CanOverrideOrderStatus: 'CanOverrideOrderStatus',
  CanReassignOrder: 'CanReassignOrder',
  CanRefundOrder: 'CanRefundOrder',
  CanViewPagedOrderAdmin: 'CanViewPagedOrderAdmin',
  CanViewOrderDetailAdmin: 'CanViewOrderDetailAdmin',
  CanViewOrderPhotosAdmin: 'CanViewOrderPhotosAdmin',

  // Saved addresses
  CanManageSavedAddresses: 'CanManageSavedAddresses',

  // Cleansia Plus membership (customer)
  CanManageMembership: 'CanManageMembership',

  // Recurring bookings (customer)
  CanManageRecurringBookings: 'CanManageRecurringBookings',

  // User
  CanViewPagedUser: 'CanViewPagedUser',
  CanViewUserDetail: 'CanViewUserDetail',
  CanGetCurrentUser: 'CanGetCurrentUser',
  CanChangeOwnPassword: 'CanChangeOwnPassword',
  CanUpdateCurrentUser: 'CanUpdateCurrentUser',
  CanAddPhoneNumber: 'CanAddPhoneNumber',

  // Employee
  CanGetCurrentEmployee: 'CanGetCurrentEmployee',
  CanCheckCurrentEmployee: 'CanCheckCurrentEmployee',
  CanUpdateCurrentEmployee: 'CanUpdateCurrentEmployee',
  CanViewPagedEmployee: 'CanViewPagedEmployee',
  CanApproveEmployee: 'CanApproveEmployee',
  CanRejectEmployee: 'CanRejectEmployee',
  CanAdminUpdateEmployee: 'CanAdminUpdateEmployee',
  CanViewEmployeePayoutDetails: 'CanViewEmployeePayoutDetails',
  CanViewEmployeePayoutDetailsAdmin: 'CanViewEmployeePayoutDetailsAdmin',
  CanRevealEmployeePayoutDetails: 'CanRevealEmployeePayoutDetails',
  CanRevealOrderAccessInstructions: 'CanRevealOrderAccessInstructions',

  // Employee Documents
  CanViewEmployeeDocuments: 'CanViewEmployeeDocuments',
  CanUploadEmployeeDocument: 'CanUploadEmployeeDocument',
  CanDownloadEmployeeDocument: 'CanDownloadEmployeeDocument',
  CanApproveEmployeeDocument: 'CanApproveEmployeeDocument',
  CanRejectEmployeeDocument: 'CanRejectEmployeeDocument',
  CanDeleteEmployeeDocument: 'CanDeleteEmployeeDocument',
  CanViewEmployeeDocumentsAdmin: 'CanViewEmployeeDocumentsAdmin',

  // Employee Payroll — Invoices
  CanViewPagedInvoices: 'CanViewPagedInvoices',
  CanViewPeriodPays: 'CanViewPeriodPays',
  CanViewPagedInvoicesAdmin: 'CanViewPagedInvoicesAdmin',
  CanCalculateOrderPay: 'CanCalculateOrderPay',
  CanGenerateInvoice: 'CanGenerateInvoice',
  CanApproveInvoice: 'CanApproveInvoice',
  CanMarkInvoicePaid: 'CanMarkInvoicePaid',
  CanCancelInvoice: 'CanCancelInvoice',
  CanClosePayPeriod: 'CanClosePayPeriod',
  CanUpdateInvoiceAmounts: 'CanUpdateInvoiceAmounts',
  CanDisputeInvoice: 'CanDisputeInvoice',
  CanRejectInvoice: 'CanRejectInvoice',

  // Employee Payroll — Pay Periods
  CanViewPayPeriods: 'CanViewPayPeriods',
  CanViewPayPeriod: 'CanViewPayPeriod',
  CanViewPayPeriodsAdmin: 'CanViewPayPeriodsAdmin',
  CanViewPayPeriodAdmin: 'CanViewPayPeriodAdmin',
  CanCreatePayPeriod: 'CanCreatePayPeriod',
  CanUpdatePayPeriod: 'CanUpdatePayPeriod',
  CanOpenPayPeriod: 'CanOpenPayPeriod',
  CanDeletePayPeriod: 'CanDeletePayPeriod',
  CanMarkPayPeriodPaid: 'CanMarkPayPeriodPaid',
  CanReopenPayPeriod: 'CanReopenPayPeriod',

  // Employee Payroll — Pay Config
  CanViewPayConfigs: 'CanViewPayConfigs',
  CanViewPayConfig: 'CanViewPayConfig',
  CanCreatePayConfig: 'CanCreatePayConfig',
  CanUpdatePayConfig: 'CanUpdatePayConfig',
  CanDeletePayConfig: 'CanDeletePayConfig',

  // Dispute
  CanCreateDispute: 'CanCreateDispute',
  CanViewDispute: 'CanViewDispute',
  CanViewDisputeList: 'CanViewDisputeList',
  CanAddDisputeMessage: 'CanAddDisputeMessage',
  CanRespondToDispute: 'CanRespondToDispute',
  CanResolveDispute: 'CanResolveDispute',
  CanUpdateDisputeStatus: 'CanUpdateDisputeStatus',
  CanUploadDisputeEvidence: 'CanUploadDisputeEvidence',
  CanViewDisputeAdmin: 'CanViewDisputeAdmin',
  CanViewDisputeListAdmin: 'CanViewDisputeListAdmin',

  // Reports
  CanViewRevenueReport: 'CanViewRevenueReport',
  CanViewPayrollReport: 'CanViewPayrollReport',

  // Fiscal
  CanManageFiscalFailures: 'CanManageFiscalFailures',

  // Services
  CanViewServices: 'CanViewServices',
  CanCreateService: 'CanCreateService',
  CanUpdateService: 'CanUpdateService',
  CanDeleteService: 'CanDeleteService',

  // Packages
  CanViewPackages: 'CanViewPackages',
  CanCreatePackage: 'CanCreatePackage',
  CanUpdatePackage: 'CanUpdatePackage',
  CanDeletePackage: 'CanDeletePackage',

  // Extras
  CanViewExtras: 'CanViewExtras',
  CanCreateExtra: 'CanCreateExtra',
  CanUpdateExtra: 'CanUpdateExtra',
  CanDeleteExtra: 'CanDeleteExtra',

  // Languages
  CanViewLanguages: 'CanViewLanguages',
  CanCreateLanguage: 'CanCreateLanguage',
  CanUpdateLanguage: 'CanUpdateLanguage',
  CanDeleteLanguage: 'CanDeleteLanguage',

  // Countries
  CanViewCountries: 'CanViewCountries',
  CanCreateCountry: 'CanCreateCountry',
  CanUpdateCountry: 'CanUpdateCountry',
  CanDeleteCountry: 'CanDeleteCountry',

  // Service areas
  CanViewServiceCities: 'CanViewServiceCities',
  CanManageServiceCities: 'CanManageServiceCities',

  // Currencies
  CanViewCurrencies: 'CanViewCurrencies',
  CanCreateCurrency: 'CanCreateCurrency',
  CanUpdateCurrency: 'CanUpdateCurrency',
  CanDeleteCurrency: 'CanDeleteCurrency',

  // Admin Users
  CanViewAdminUsers: 'CanViewAdminUsers',
  CanCreateAdminUser: 'CanCreateAdminUser',
  CanUpdateAdminUser: 'CanUpdateAdminUser',
  CanDeactivateAdminUser: 'CanDeactivateAdminUser',
  CanActivateAdminUser: 'CanActivateAdminUser',
  CanSetAdminRole: 'CanSetAdminRole',

  // Company Info
  CanViewCompanyInfo: 'CanViewCompanyInfo',
  CanCreateCompanyInfo: 'CanCreateCompanyInfo',
  CanUpdateCompanyInfo: 'CanUpdateCompanyInfo',
  CanDeleteCompanyInfo: 'CanDeleteCompanyInfo',

  // Email Templates
  CanViewEmailTemplates: 'CanViewEmailTemplates',
  CanUpdateEmailTemplate: 'CanUpdateEmailTemplate',

  // Country Configuration
  CanViewCountryConfigurations: 'CanViewCountryConfigurations',
  CanCreateCountryConfiguration: 'CanCreateCountryConfiguration',
  CanUpdateCountryConfiguration: 'CanUpdateCountryConfiguration',
  CanDeleteCountryConfiguration: 'CanDeleteCountryConfiguration',

  // Legal documents
  CanViewLegalDocuments: 'CanViewLegalDocuments',

  // Tenant Configuration
  CanViewTenantConfigurations: 'CanViewTenantConfigurations',
  CanCreateTenantConfiguration: 'CanCreateTenantConfiguration',
  CanUpdateTenantConfiguration: 'CanUpdateTenantConfiguration',
  CanDeleteTenantConfiguration: 'CanDeleteTenantConfiguration',

  // Company lifecycle
  CanViewCompanyLifecycle: 'CanViewCompanyLifecycle',
  CanDeactivateCompany: 'CanDeactivateCompany',
  CanReactivateCompany: 'CanReactivateCompany',
  CanWindDownCompany: 'CanWindDownCompany',
  CanArchiveCompany: 'CanArchiveCompany',

  // Device
  Authenticated: 'Authenticated',

  // GDPR
  CanExportOwnData: 'CanExportOwnData',
  CanDeleteOwnAccount: 'CanDeleteOwnAccount',
  CanGrantConsent: 'CanGrantConsent',
  CanWithdrawConsent: 'CanWithdrawConsent',
  CanViewOwnConsents: 'CanViewOwnConsents',

  // Admin GDPR
  CanAdminExportUserData: 'CanAdminExportUserData',
  CanAdminDeleteUserAccount: 'CanAdminDeleteUserAccount',
  CanAdminViewUserConsents: 'CanAdminViewUserConsents',
  CanViewGdprRequests: 'CanViewGdprRequests',

  // Loyalty
  CanViewMyLoyalty: 'CanViewMyLoyalty',
  CanViewMyCredit: 'CanViewMyCredit',

  // Promo codes
  CanRedeemPromoCode: 'CanRedeemPromoCode',

  // Referrals
  CanViewMyReferral: 'CanViewMyReferral',

  // Admin Promo Codes
  CanViewPromoCodes: 'CanViewPromoCodes',
  CanCreatePromoCode: 'CanCreatePromoCode',
  CanUpdatePromoCode: 'CanUpdatePromoCode',
  CanDeactivatePromoCode: 'CanDeactivatePromoCode',

  // Admin Loyalty Tier Configs
  CanViewLoyaltyTierConfigs: 'CanViewLoyaltyTierConfigs',
  CanUpdateLoyaltyTierConfig: 'CanUpdateLoyaltyTierConfig',

  // Admin Loyalty
  CanGrantLoyaltyPoints: 'CanGrantLoyaltyPoints',
  CanViewUserLoyalty: 'CanViewUserLoyalty',

  // Admin Membership Plans
  CanViewMembershipPlans: 'CanViewMembershipPlans',
  CanCreateMembershipPlan: 'CanCreateMembershipPlan',
  CanUpdateMembershipPlan: 'CanUpdateMembershipPlan',
  CanDeactivateMembershipPlan: 'CanDeactivateMembershipPlan',

  // Admin Referrals
  CanViewReferrals: 'CanViewReferrals',
  CanInterveneReferral: 'CanInterveneReferral',

  // Marketing
  CanSendSitewidePromo: 'CanSendSitewidePromo',

  // Refunds and credit
  CanIssueRefund: 'CanIssueRefund',
  CanIssueCustomerCredit: 'CanIssueCustomerCredit',
  CanViewUserCredit: 'CanViewUserCredit',
  CanExpireCustomerCredit: 'CanExpireCustomerCredit',

  // Admin Action Audit Log
  CanViewAuditLog: 'CanViewAuditLog',

  // Admin notifications feed
  CanViewAdminNotifications: 'CanViewAdminNotifications',
} as const;

export type PolicyName = (typeof Policy)[keyof typeof Policy];

/**
 * Maps each Policy to its required PhysicalPolicy. Mirrors backend
 * `PolicyBuilder.Map`. Unknown policies fall back to `Authenticated`
 * (matching backend `ToPhysicalPolicy` behavior).
 */
export const POLICY_MAP: Record<PolicyName, PhysicalPolicy> = {
  // Order
  CanViewPagedOrder: PhysicalPolicy.EmployeeOrAdmin,
  CanViewPagedUserOrder: PhysicalPolicy.Authenticated,
  CanViewOrderDetail: PhysicalPolicy.Authenticated,
  CanViewOrderCustomer: PhysicalPolicy.SupportOrAbove,
  CanUpdateOrder: PhysicalPolicy.EmployeeOrAdmin,
  CanTakeOrder: PhysicalPolicy.EmployeeOrAdmin,
  CanStartOrder: PhysicalPolicy.EmployeeOrAdmin,
  CanCompleteOrder: PhysicalPolicy.EmployeeOrAdmin,
  CanUploadOrderPhoto: PhysicalPolicy.EmployeeOrAdmin,
  CanViewOrderPhotos: PhysicalPolicy.Authenticated,
  CanDeleteOrderPhoto: PhysicalPolicy.EmployeeOrAdmin,
  CanAddOrderNote: PhysicalPolicy.EmployeeOrAdmin,
  CanUpdateOrderNote: PhysicalPolicy.EmployeeOrAdmin,
  CanDeleteOrderNote: PhysicalPolicy.EmployeeOrAdmin,
  CanReportOrderIssue: PhysicalPolicy.Authenticated,
  CanUpdateOrderIssue: PhysicalPolicy.EmployeeOrAdmin,
  CanDeleteOrderIssue: PhysicalPolicy.EmployeeOrAdmin,
  CanSubmitOrderReview: PhysicalPolicy.CustomerOnly,
  CanViewOrderReview: PhysicalPolicy.Authenticated,
  CanCancelOrder: PhysicalPolicy.CustomerOnly,
  CanAdminCancelOrder: PhysicalPolicy.SupportOrAbove,
  CanOverrideOrderStatus: PhysicalPolicy.SupportOrAbove,
  CanReassignOrder: PhysicalPolicy.SupportOrAbove,
  CanRefundOrder: PhysicalPolicy.SupportOrAbove,
  CanViewPagedOrderAdmin: PhysicalPolicy.SupportOrAbove,
  CanViewOrderDetailAdmin: PhysicalPolicy.SupportOrAbove,
  CanViewOrderPhotosAdmin: PhysicalPolicy.SupportOrAbove,

  // Saved addresses
  CanManageSavedAddresses: PhysicalPolicy.CustomerOnly,

  // Cleansia Plus membership (customer)
  CanManageMembership: PhysicalPolicy.CustomerOnly,

  // Recurring bookings (customer)
  CanManageRecurringBookings: PhysicalPolicy.CustomerOnly,

  // User
  CanViewPagedUser: PhysicalPolicy.EmployeeOrAdmin,
  CanViewUserDetail: PhysicalPolicy.OwnerOrElevated,
  CanGetCurrentUser: PhysicalPolicy.Authenticated,
  CanChangeOwnPassword: PhysicalPolicy.Authenticated,
  CanUpdateCurrentUser: PhysicalPolicy.Authenticated,
  CanAddPhoneNumber: PhysicalPolicy.Authenticated,

  // Employee
  CanGetCurrentEmployee: PhysicalPolicy.Authenticated,
  CanCheckCurrentEmployee: PhysicalPolicy.Authenticated,
  CanUpdateCurrentEmployee: PhysicalPolicy.Authenticated,
  CanViewPagedEmployee: PhysicalPolicy.AdminOnly,
  CanApproveEmployee: PhysicalPolicy.SupportOrAbove,
  CanRejectEmployee: PhysicalPolicy.SupportOrAbove,
  CanAdminUpdateEmployee: PhysicalPolicy.SupportOrAbove,
  CanViewEmployeePayoutDetails: PhysicalPolicy.EmployeeOrAdmin,
  CanViewEmployeePayoutDetailsAdmin: PhysicalPolicy.AccountantOrAbove,
  CanRevealEmployeePayoutDetails: PhysicalPolicy.ManagerOrAbove,
  CanRevealOrderAccessInstructions: PhysicalPolicy.SupportOrAbove,

  // Employee Documents
  CanViewEmployeeDocuments: PhysicalPolicy.EmployeeOrAdmin,
  CanUploadEmployeeDocument: PhysicalPolicy.EmployeeOrAdmin,
  CanDownloadEmployeeDocument: PhysicalPolicy.EmployeeOrAdmin,
  CanApproveEmployeeDocument: PhysicalPolicy.SupportOrAbove,
  CanRejectEmployeeDocument: PhysicalPolicy.SupportOrAbove,
  CanDeleteEmployeeDocument: PhysicalPolicy.EmployeeOrAdmin,
  CanViewEmployeeDocumentsAdmin: PhysicalPolicy.SupportOrAbove,

  // Employee Payroll — Invoices
  CanViewPagedInvoices: PhysicalPolicy.EmployeeOrAdmin,
  CanViewPeriodPays: PhysicalPolicy.EmployeeOrAdmin,
  CanViewPagedInvoicesAdmin: PhysicalPolicy.AccountantOrAbove,
  CanCalculateOrderPay: PhysicalPolicy.AdminOnly,
  CanGenerateInvoice: PhysicalPolicy.AccountantOrAbove,
  CanApproveInvoice: PhysicalPolicy.AccountantOrAbove,
  CanMarkInvoicePaid: PhysicalPolicy.AccountantOrAbove,
  CanCancelInvoice: PhysicalPolicy.AccountantOrAbove,
  CanClosePayPeriod: PhysicalPolicy.AccountantOrAbove,
  CanUpdateInvoiceAmounts: PhysicalPolicy.AccountantOrAbove,
  CanDisputeInvoice: PhysicalPolicy.AccountantOrAbove,
  CanRejectInvoice: PhysicalPolicy.AccountantOrAbove,

  // Employee Payroll — Pay Periods
  CanViewPayPeriods: PhysicalPolicy.EmployeeOrAdmin,
  CanViewPayPeriod: PhysicalPolicy.EmployeeOrAdmin,
  CanViewPayPeriodsAdmin: PhysicalPolicy.AccountantOrAbove,
  CanViewPayPeriodAdmin: PhysicalPolicy.AccountantOrAbove,
  CanCreatePayPeriod: PhysicalPolicy.AccountantOrAbove,
  CanUpdatePayPeriod: PhysicalPolicy.AccountantOrAbove,
  CanOpenPayPeriod: PhysicalPolicy.AccountantOrAbove,
  CanDeletePayPeriod: PhysicalPolicy.AccountantOrAbove,
  CanMarkPayPeriodPaid: PhysicalPolicy.AccountantOrAbove,
  CanReopenPayPeriod: PhysicalPolicy.AccountantOrAbove,

  // Employee Payroll — Pay Config
  CanViewPayConfigs: PhysicalPolicy.AccountantOrAbove,
  CanViewPayConfig: PhysicalPolicy.AccountantOrAbove,
  CanCreatePayConfig: PhysicalPolicy.ManagerOrAbove,
  CanUpdatePayConfig: PhysicalPolicy.ManagerOrAbove,
  CanDeletePayConfig: PhysicalPolicy.ManagerOrAbove,

  // Dispute
  CanCreateDispute: PhysicalPolicy.CustomerOnly,
  CanViewDispute: PhysicalPolicy.CustomerOnly,
  CanViewDisputeList: PhysicalPolicy.CustomerOnly,
  CanAddDisputeMessage: PhysicalPolicy.CustomerOnly,
  CanRespondToDispute: PhysicalPolicy.SupportOrAbove,
  CanResolveDispute: PhysicalPolicy.SupportOrAbove,
  CanUpdateDisputeStatus: PhysicalPolicy.SupportOrAbove,
  CanUploadDisputeEvidence: PhysicalPolicy.CustomerOnly,
  CanViewDisputeAdmin: PhysicalPolicy.SupportOrAbove,
  CanViewDisputeListAdmin: PhysicalPolicy.SupportOrAbove,

  // Reports
  CanViewRevenueReport: PhysicalPolicy.AccountantOrAbove,
  CanViewPayrollReport: PhysicalPolicy.AccountantOrAbove,

  // Fiscal
  CanManageFiscalFailures: PhysicalPolicy.AccountantOrAbove,

  // Services
  CanViewServices: PhysicalPolicy.AdminOnly,
  CanCreateService: PhysicalPolicy.ManagerOrAbove,
  CanUpdateService: PhysicalPolicy.ManagerOrAbove,
  CanDeleteService: PhysicalPolicy.ManagerOrAbove,

  // Packages
  CanViewPackages: PhysicalPolicy.AdminOnly,
  CanCreatePackage: PhysicalPolicy.ManagerOrAbove,
  CanUpdatePackage: PhysicalPolicy.ManagerOrAbove,
  CanDeletePackage: PhysicalPolicy.ManagerOrAbove,

  // Extras
  CanViewExtras: PhysicalPolicy.AdminOnly,
  CanCreateExtra: PhysicalPolicy.ManagerOrAbove,
  CanUpdateExtra: PhysicalPolicy.ManagerOrAbove,
  CanDeleteExtra: PhysicalPolicy.ManagerOrAbove,

  // Languages
  CanViewLanguages: PhysicalPolicy.AdminOnly,
  CanCreateLanguage: PhysicalPolicy.ManagerOrAbove,
  CanUpdateLanguage: PhysicalPolicy.ManagerOrAbove,
  CanDeleteLanguage: PhysicalPolicy.ManagerOrAbove,

  // Countries
  CanViewCountries: PhysicalPolicy.AdminOnly,
  CanCreateCountry: PhysicalPolicy.ManagerOrAbove,
  CanUpdateCountry: PhysicalPolicy.ManagerOrAbove,
  CanDeleteCountry: PhysicalPolicy.ManagerOrAbove,

  // Service areas
  CanViewServiceCities: PhysicalPolicy.AdminOnly,
  CanManageServiceCities: PhysicalPolicy.ManagerOrAbove,

  // Currencies
  CanViewCurrencies: PhysicalPolicy.AdminOnly,
  CanCreateCurrency: PhysicalPolicy.ManagerOrAbove,
  CanUpdateCurrency: PhysicalPolicy.ManagerOrAbove,
  CanDeleteCurrency: PhysicalPolicy.ManagerOrAbove,

  // Admin Users
  CanViewAdminUsers: PhysicalPolicy.ManagerOrAbove,
  CanCreateAdminUser: PhysicalPolicy.AdministratorOnly,
  CanUpdateAdminUser: PhysicalPolicy.AdministratorOnly,
  CanDeactivateAdminUser: PhysicalPolicy.AdministratorOnly,
  CanActivateAdminUser: PhysicalPolicy.AdministratorOnly,
  CanSetAdminRole: PhysicalPolicy.AdministratorOnly,

  // Company Info
  CanViewCompanyInfo: PhysicalPolicy.AdminOnly,
  CanCreateCompanyInfo: PhysicalPolicy.ManagerOrAbove,
  CanUpdateCompanyInfo: PhysicalPolicy.ManagerOrAbove,
  CanDeleteCompanyInfo: PhysicalPolicy.ManagerOrAbove,

  // Email Templates
  CanViewEmailTemplates: PhysicalPolicy.AdminOnly,
  CanUpdateEmailTemplate: PhysicalPolicy.ManagerOrAbove,

  // Country Configuration
  CanViewCountryConfigurations: PhysicalPolicy.AdminOnly,
  CanCreateCountryConfiguration: PhysicalPolicy.ManagerOrAbove,
  CanUpdateCountryConfiguration: PhysicalPolicy.ManagerOrAbove,
  CanDeleteCountryConfiguration: PhysicalPolicy.ManagerOrAbove,

  // Legal documents
  CanViewLegalDocuments: PhysicalPolicy.AdministratorOnly,

  // Tenant Configuration
  CanViewTenantConfigurations: PhysicalPolicy.AdministratorOnly,
  CanCreateTenantConfiguration: PhysicalPolicy.AdministratorOnly,
  CanUpdateTenantConfiguration: PhysicalPolicy.AdministratorOnly,
  CanDeleteTenantConfiguration: PhysicalPolicy.AdministratorOnly,

  // Company lifecycle
  CanViewCompanyLifecycle: PhysicalPolicy.AdministratorOnly,
  CanDeactivateCompany: PhysicalPolicy.AdministratorOnly,
  CanReactivateCompany: PhysicalPolicy.AdministratorOnly,
  CanWindDownCompany: PhysicalPolicy.AdministratorOnly,
  CanArchiveCompany: PhysicalPolicy.AdministratorOnly,

  // Device
  Authenticated: PhysicalPolicy.Authenticated,

  // GDPR
  CanExportOwnData: PhysicalPolicy.Authenticated,
  CanDeleteOwnAccount: PhysicalPolicy.Authenticated,
  CanGrantConsent: PhysicalPolicy.Authenticated,
  CanWithdrawConsent: PhysicalPolicy.Authenticated,
  CanViewOwnConsents: PhysicalPolicy.Authenticated,

  // Admin GDPR
  CanAdminExportUserData: PhysicalPolicy.SupportOrAbove,
  CanAdminDeleteUserAccount: PhysicalPolicy.ManagerOrAbove,
  CanAdminViewUserConsents: PhysicalPolicy.SupportOrAbove,
  CanViewGdprRequests: PhysicalPolicy.SupportOrAbove,

  // Loyalty
  CanViewMyLoyalty: PhysicalPolicy.CustomerOnly,
  CanViewMyCredit: PhysicalPolicy.CustomerOnly,

  // Promo codes
  CanRedeemPromoCode: PhysicalPolicy.CustomerOnly,

  // Referrals
  CanViewMyReferral: PhysicalPolicy.CustomerOnly,

  // Admin Promo Codes
  CanViewPromoCodes: PhysicalPolicy.AdminOnly,
  CanCreatePromoCode: PhysicalPolicy.ManagerOrAbove,
  CanUpdatePromoCode: PhysicalPolicy.ManagerOrAbove,
  CanDeactivatePromoCode: PhysicalPolicy.ManagerOrAbove,

  // Admin Loyalty Tier Configs
  CanViewLoyaltyTierConfigs: PhysicalPolicy.AdminOnly,
  CanUpdateLoyaltyTierConfig: PhysicalPolicy.ManagerOrAbove,

  // Admin Loyalty
  CanGrantLoyaltyPoints: PhysicalPolicy.SupportOrAbove,
  CanViewUserLoyalty: PhysicalPolicy.SupportOrAbove,

  // Admin Membership Plans
  CanViewMembershipPlans: PhysicalPolicy.AdminOnly,
  CanCreateMembershipPlan: PhysicalPolicy.ManagerOrAbove,
  CanUpdateMembershipPlan: PhysicalPolicy.ManagerOrAbove,
  CanDeactivateMembershipPlan: PhysicalPolicy.ManagerOrAbove,

  // Admin Referrals
  CanViewReferrals: PhysicalPolicy.AdminOnly,
  CanInterveneReferral: PhysicalPolicy.SupportOrAbove,

  // Marketing
  CanSendSitewidePromo: PhysicalPolicy.ManagerOrAbove,

  // Refunds and credit
  CanIssueRefund: PhysicalPolicy.SupportOrAbove,
  CanIssueCustomerCredit: PhysicalPolicy.SupportOrAbove,
  CanViewUserCredit: PhysicalPolicy.AdminOnly,
  CanExpireCustomerCredit: PhysicalPolicy.ManagerOrAbove,

  // Admin Action Audit Log
  CanViewAuditLog: PhysicalPolicy.SupportOrAbove,

  // Admin notifications feed
  CanViewAdminNotifications: PhysicalPolicy.AdminOnly,
};

export function resolvePhysicalPolicy(policy: PolicyName | string): PhysicalPolicy {
  return (POLICY_MAP as Record<string, PhysicalPolicy | undefined>)[policy] ?? PhysicalPolicy.Authenticated;
}
