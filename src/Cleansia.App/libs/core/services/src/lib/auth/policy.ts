import { PhysicalPolicy } from './physical-policy';

/**
 * Frontend mirror of `Cleansia.Core.AppServices.Authentication.Policy`.
 * Each constant names a logical permission; the directive resolves it to a
 * PhysicalPolicy via {@link POLICY_MAP} and checks the current user's role
 * against that gate.
 *
 * Keep in sync with the backend `Policy.cs` + `PolicyBuilder.cs` map. When
 * the backend adds or moves a policy, regenerate-or-edit this file by hand.
 */
export const Policy = {
  // Order
  CanViewPagedOrder: 'CanViewPagedOrder',
  CanViewPagedUserOrder: 'CanViewPagedUserOrder',
  CanViewOrderDetail: 'CanViewOrderDetail',
  CanUpdateOrder: 'CanUpdateOrder',
  CanTakeOrder: 'CanTakeOrder',
  CanStartOrder: 'CanStartOrder',
  CanCompleteOrder: 'CanCompleteOrder',
  CanUploadOrderPhoto: 'CanUploadOrderPhoto',
  CanViewOrderPhotos: 'CanViewOrderPhotos',
  CanDeleteOrderPhoto: 'CanDeleteOrderPhoto',
  CanAddOrderNote: 'CanAddOrderNote',
  CanReportOrderIssue: 'CanReportOrderIssue',
  CanSubmitOrderReview: 'CanSubmitOrderReview',
  CanViewOrderReview: 'CanViewOrderReview',
  CanCancelOrder: 'CanCancelOrder',

  // Saved addresses / Membership / Recurring bookings (customer)
  CanManageSavedAddresses: 'CanManageSavedAddresses',
  CanManageMembership: 'CanManageMembership',
  CanManageRecurringBookings: 'CanManageRecurringBookings',

  // User
  CanViewPagedUser: 'CanViewPagedUser',
  CanViewUserDetail: 'CanViewUserDetail',
  CanGetCurrentUser: 'CanGetCurrentUser',
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

  // Employee Documents
  CanViewEmployeeDocuments: 'CanViewEmployeeDocuments',
  CanUploadEmployeeDocument: 'CanUploadEmployeeDocument',
  CanDownloadEmployeeDocument: 'CanDownloadEmployeeDocument',
  CanApproveEmployeeDocument: 'CanApproveEmployeeDocument',
  CanRejectEmployeeDocument: 'CanRejectEmployeeDocument',
  CanDeleteEmployeeDocument: 'CanDeleteEmployeeDocument',

  // Employee Payroll
  CanViewPagedInvoices: 'CanViewPagedInvoices',
  CanViewPeriodPays: 'CanViewPeriodPays',
  CanCalculateOrderPay: 'CanCalculateOrderPay',
  CanGenerateInvoice: 'CanGenerateInvoice',
  CanApproveInvoice: 'CanApproveInvoice',
  CanMarkInvoicePaid: 'CanMarkInvoicePaid',
  CanCancelInvoice: 'CanCancelInvoice',
  CanClosePayPeriod: 'CanClosePayPeriod',

  // Dispute
  CanCreateDispute: 'CanCreateDispute',
  CanViewDispute: 'CanViewDispute',
  CanViewDisputeList: 'CanViewDisputeList',
  CanRespondToDispute: 'CanRespondToDispute',
  CanResolveDispute: 'CanResolveDispute',
  CanUpdateDisputeStatus: 'CanUpdateDisputeStatus',
  CanUploadDisputeEvidence: 'CanUploadDisputeEvidence',

  // Reports / Fiscal
  CanViewRevenueReport: 'CanViewRevenueReport',
  CanViewPayrollReport: 'CanViewPayrollReport',
  CanManageFiscalFailures: 'CanManageFiscalFailures',

  // Catalog
  CanViewServices: 'CanViewServices',
  CanCreateService: 'CanCreateService',
  CanUpdateService: 'CanUpdateService',
  CanDeleteService: 'CanDeleteService',
  CanViewPackages: 'CanViewPackages',
  CanCreatePackage: 'CanCreatePackage',
  CanUpdatePackage: 'CanUpdatePackage',
  CanDeletePackage: 'CanDeletePackage',

  // i18n
  CanViewLanguages: 'CanViewLanguages',
  CanCreateLanguage: 'CanCreateLanguage',
  CanUpdateLanguage: 'CanUpdateLanguage',
  CanDeleteLanguage: 'CanDeleteLanguage',
  CanViewCountries: 'CanViewCountries',
  CanCreateCountry: 'CanCreateCountry',
  CanUpdateCountry: 'CanUpdateCountry',
  CanDeleteCountry: 'CanDeleteCountry',
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

  // Company
  CanViewCompanyInfo: 'CanViewCompanyInfo',
  CanCreateCompanyInfo: 'CanCreateCompanyInfo',
  CanUpdateCompanyInfo: 'CanUpdateCompanyInfo',
  CanDeleteCompanyInfo: 'CanDeleteCompanyInfo',

  // Email Templates
  CanViewEmailTemplates: 'CanViewEmailTemplates',
  CanUpdateEmailTemplate: 'CanUpdateEmailTemplate',

  // Feature Flags
  CanViewFeatureFlags: 'CanViewFeatureFlags',
  CanCreateFeatureFlag: 'CanCreateFeatureFlag',
  CanToggleFeatureFlag: 'CanToggleFeatureFlag',
  CanDeleteFeatureFlag: 'CanDeleteFeatureFlag',
  CanCheckFeatureFlag: 'CanCheckFeatureFlag',

  // Country / Tenant Configuration
  CanViewCountryConfigurations: 'CanViewCountryConfigurations',
  CanCreateCountryConfiguration: 'CanCreateCountryConfiguration',
  CanUpdateCountryConfiguration: 'CanUpdateCountryConfiguration',
  CanDeleteCountryConfiguration: 'CanDeleteCountryConfiguration',
  CanViewTenantConfigurations: 'CanViewTenantConfigurations',
  CanCreateTenantConfiguration: 'CanCreateTenantConfiguration',
  CanUpdateTenantConfiguration: 'CanUpdateTenantConfiguration',
  CanDeleteTenantConfiguration: 'CanDeleteTenantConfiguration',

  // GDPR (own)
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

  // Loyalty / Promo / Referrals (customer)
  CanViewMyLoyalty: 'CanViewMyLoyalty',
  CanRedeemPromoCode: 'CanRedeemPromoCode',
  CanViewMyReferral: 'CanViewMyReferral',

  // Admin Promo / Loyalty / Referrals
  CanViewPromoCodes: 'CanViewPromoCodes',
  CanCreatePromoCode: 'CanCreatePromoCode',
  CanUpdatePromoCode: 'CanUpdatePromoCode',
  CanDeactivatePromoCode: 'CanDeactivatePromoCode',
  CanViewLoyaltyTierConfigs: 'CanViewLoyaltyTierConfigs',
  CanUpdateLoyaltyTierConfig: 'CanUpdateLoyaltyTierConfig',
  CanGrantLoyaltyPoints: 'CanGrantLoyaltyPoints',
  CanViewUserLoyalty: 'CanViewUserLoyalty',
  CanViewReferrals: 'CanViewReferrals',
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
  CanUpdateOrder: PhysicalPolicy.EmployeeOrAdmin,
  CanTakeOrder: PhysicalPolicy.EmployeeOrAdmin,
  CanStartOrder: PhysicalPolicy.EmployeeOrAdmin,
  CanCompleteOrder: PhysicalPolicy.EmployeeOrAdmin,
  CanUploadOrderPhoto: PhysicalPolicy.EmployeeOrAdmin,
  CanViewOrderPhotos: PhysicalPolicy.Authenticated,
  CanDeleteOrderPhoto: PhysicalPolicy.EmployeeOrAdmin,
  CanAddOrderNote: PhysicalPolicy.EmployeeOrAdmin,
  CanReportOrderIssue: PhysicalPolicy.Authenticated,
  CanSubmitOrderReview: PhysicalPolicy.CustomerOnly,
  CanViewOrderReview: PhysicalPolicy.Authenticated,
  CanCancelOrder: PhysicalPolicy.CustomerOnly,

  CanManageSavedAddresses: PhysicalPolicy.CustomerOnly,
  CanManageMembership: PhysicalPolicy.CustomerOnly,
  CanManageRecurringBookings: PhysicalPolicy.CustomerOnly,

  CanViewPagedUser: PhysicalPolicy.EmployeeOrAdmin,
  CanViewUserDetail: PhysicalPolicy.OwnerOrElevated,
  CanGetCurrentUser: PhysicalPolicy.Authenticated,
  CanUpdateCurrentUser: PhysicalPolicy.Authenticated,
  CanAddPhoneNumber: PhysicalPolicy.Authenticated,

  CanGetCurrentEmployee: PhysicalPolicy.Authenticated,
  CanCheckCurrentEmployee: PhysicalPolicy.Authenticated,
  CanUpdateCurrentEmployee: PhysicalPolicy.Authenticated,
  CanViewPagedEmployee: PhysicalPolicy.AdminOnly,
  CanApproveEmployee: PhysicalPolicy.AdminOnly,
  CanRejectEmployee: PhysicalPolicy.AdminOnly,
  CanAdminUpdateEmployee: PhysicalPolicy.AdminOnly,

  CanViewEmployeeDocuments: PhysicalPolicy.EmployeeOrAdmin,
  CanUploadEmployeeDocument: PhysicalPolicy.EmployeeOrAdmin,
  CanDownloadEmployeeDocument: PhysicalPolicy.EmployeeOrAdmin,
  CanApproveEmployeeDocument: PhysicalPolicy.AdminOnly,
  CanRejectEmployeeDocument: PhysicalPolicy.AdminOnly,
  CanDeleteEmployeeDocument: PhysicalPolicy.EmployeeOrAdmin,

  CanViewPagedInvoices: PhysicalPolicy.AdminOnly,
  CanViewPeriodPays: PhysicalPolicy.EmployeeOrAdmin,
  CanCalculateOrderPay: PhysicalPolicy.AdminOnly,
  CanGenerateInvoice: PhysicalPolicy.AdminOnly,
  CanApproveInvoice: PhysicalPolicy.AdminOnly,
  CanMarkInvoicePaid: PhysicalPolicy.AdminOnly,
  CanCancelInvoice: PhysicalPolicy.AdminOnly,
  CanClosePayPeriod: PhysicalPolicy.AdminOnly,

  CanCreateDispute: PhysicalPolicy.CustomerOnly,
  CanViewDispute: PhysicalPolicy.CustomerOnly,
  CanViewDisputeList: PhysicalPolicy.CustomerOnly,
  CanRespondToDispute: PhysicalPolicy.Authenticated,
  CanResolveDispute: PhysicalPolicy.AdminOnly,
  CanUpdateDisputeStatus: PhysicalPolicy.AdminOnly,
  CanUploadDisputeEvidence: PhysicalPolicy.CustomerOnly,

  CanViewRevenueReport: PhysicalPolicy.AdminOnly,
  CanViewPayrollReport: PhysicalPolicy.AdminOnly,
  CanManageFiscalFailures: PhysicalPolicy.AdminOnly,

  CanViewServices: PhysicalPolicy.AdminOnly,
  CanCreateService: PhysicalPolicy.AdminOnly,
  CanUpdateService: PhysicalPolicy.AdminOnly,
  CanDeleteService: PhysicalPolicy.AdminOnly,
  CanViewPackages: PhysicalPolicy.AdminOnly,
  CanCreatePackage: PhysicalPolicy.AdminOnly,
  CanUpdatePackage: PhysicalPolicy.AdminOnly,
  CanDeletePackage: PhysicalPolicy.AdminOnly,

  CanViewLanguages: PhysicalPolicy.AdminOnly,
  CanCreateLanguage: PhysicalPolicy.AdminOnly,
  CanUpdateLanguage: PhysicalPolicy.AdminOnly,
  CanDeleteLanguage: PhysicalPolicy.AdminOnly,
  CanViewCountries: PhysicalPolicy.AdminOnly,
  CanCreateCountry: PhysicalPolicy.AdminOnly,
  CanUpdateCountry: PhysicalPolicy.AdminOnly,
  CanDeleteCountry: PhysicalPolicy.AdminOnly,
  CanViewCurrencies: PhysicalPolicy.AdminOnly,
  CanCreateCurrency: PhysicalPolicy.AdminOnly,
  CanUpdateCurrency: PhysicalPolicy.AdminOnly,
  CanDeleteCurrency: PhysicalPolicy.AdminOnly,

  CanViewAdminUsers: PhysicalPolicy.AdminOnly,
  CanCreateAdminUser: PhysicalPolicy.AdminOnly,
  CanUpdateAdminUser: PhysicalPolicy.AdminOnly,
  CanDeactivateAdminUser: PhysicalPolicy.AdminOnly,
  CanActivateAdminUser: PhysicalPolicy.AdminOnly,

  CanViewCompanyInfo: PhysicalPolicy.AdminOnly,
  CanCreateCompanyInfo: PhysicalPolicy.AdminOnly,
  CanUpdateCompanyInfo: PhysicalPolicy.AdminOnly,
  CanDeleteCompanyInfo: PhysicalPolicy.AdminOnly,

  CanViewEmailTemplates: PhysicalPolicy.AdminOnly,
  CanUpdateEmailTemplate: PhysicalPolicy.AdminOnly,

  CanViewFeatureFlags: PhysicalPolicy.AdminOnly,
  CanCreateFeatureFlag: PhysicalPolicy.AdminOnly,
  CanToggleFeatureFlag: PhysicalPolicy.AdminOnly,
  CanDeleteFeatureFlag: PhysicalPolicy.AdminOnly,
  CanCheckFeatureFlag: PhysicalPolicy.Authenticated,

  CanViewCountryConfigurations: PhysicalPolicy.AdminOnly,
  CanCreateCountryConfiguration: PhysicalPolicy.AdminOnly,
  CanUpdateCountryConfiguration: PhysicalPolicy.AdminOnly,
  CanDeleteCountryConfiguration: PhysicalPolicy.AdminOnly,
  CanViewTenantConfigurations: PhysicalPolicy.AdminOnly,
  CanCreateTenantConfiguration: PhysicalPolicy.AdminOnly,
  CanUpdateTenantConfiguration: PhysicalPolicy.AdminOnly,
  CanDeleteTenantConfiguration: PhysicalPolicy.AdminOnly,

  CanExportOwnData: PhysicalPolicy.Authenticated,
  CanDeleteOwnAccount: PhysicalPolicy.Authenticated,
  CanGrantConsent: PhysicalPolicy.Authenticated,
  CanWithdrawConsent: PhysicalPolicy.Authenticated,
  CanViewOwnConsents: PhysicalPolicy.Authenticated,

  CanAdminExportUserData: PhysicalPolicy.AdminOnly,
  CanAdminDeleteUserAccount: PhysicalPolicy.AdminOnly,
  CanAdminViewUserConsents: PhysicalPolicy.AdminOnly,
  CanViewGdprRequests: PhysicalPolicy.AdminOnly,

  CanViewMyLoyalty: PhysicalPolicy.CustomerOnly,
  CanRedeemPromoCode: PhysicalPolicy.CustomerOnly,
  CanViewMyReferral: PhysicalPolicy.CustomerOnly,

  CanViewPromoCodes: PhysicalPolicy.AdminOnly,
  CanCreatePromoCode: PhysicalPolicy.AdminOnly,
  CanUpdatePromoCode: PhysicalPolicy.AdminOnly,
  CanDeactivatePromoCode: PhysicalPolicy.AdminOnly,
  CanViewLoyaltyTierConfigs: PhysicalPolicy.AdminOnly,
  CanUpdateLoyaltyTierConfig: PhysicalPolicy.AdminOnly,
  CanGrantLoyaltyPoints: PhysicalPolicy.AdminOnly,
  CanViewUserLoyalty: PhysicalPolicy.AdminOnly,
  CanViewReferrals: PhysicalPolicy.AdminOnly,
};

export function resolvePhysicalPolicy(policy: PolicyName | string): PhysicalPolicy {
  return (POLICY_MAP as Record<string, PhysicalPolicy | undefined>)[policy] ?? PhysicalPolicy.Authenticated;
}
