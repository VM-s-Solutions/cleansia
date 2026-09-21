import { inject } from '@angular/core';
import { Route } from '@angular/router';
import { adminGuard, guestGuard, permissionGuard } from '@cleansia/admin-services';
import { CleansiaNotFoundComponent } from '@cleansia/components';
import { CleansiaAdminRoute, CommonRoute, PermissionService, Policy } from '@cleansia/services';
import { ADMIN_MENU_ITEMS, resolveLandingRoute } from './admin-menu';

export const appRoutes: Route[] = [
  {
    path: '',
    redirectTo: () => resolveLandingRoute(ADMIN_MENU_ITEMS, inject(PermissionService)),
    pathMatch: 'full',
  },
  {
    path: CleansiaAdminRoute.LOGIN,
    loadChildren: () =>
      import('@cleansia/admin-features/admin-login').then(
        (m) => m.adminLoginRoutes
      ),
    canActivate: [guestGuard],
  },
  {
    path: CleansiaAdminRoute.EMPLOYEE_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPagedEmployee },
    loadChildren: () =>
      import('@cleansia/admin-features/employee-management').then(
        (m) => m.employeeManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.PAY_PERIODS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPayPeriodsAdmin },
    loadChildren: () =>
      import('@cleansia/admin-features/pay-periods').then((m) => m.payPeriodsRoutes),
  },
  {
    path: CleansiaAdminRoute.ORDER_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPagedOrderAdmin },
    loadChildren: () =>
      import('@cleansia/admin-features/order-management').then(
        (m) => m.orderManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.DISPUTE_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewDisputeListAdmin },
    loadChildren: () =>
      import('@cleansia/admin-features/disputes-management').then(
        (m) => m.disputesManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.INVOICE_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPagedInvoicesAdmin },
    loadChildren: () =>
      import('@cleansia/admin-features/invoice-management').then(
        (m) => m.invoiceManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.REPORTS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewRevenueReport },
    loadChildren: () =>
      import('@cleansia/admin-features/reports').then((m) => m.reportsRoutes),
  },
  {
    path: CleansiaAdminRoute.SERVICE_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewServices },
    loadChildren: () =>
      import('@cleansia/admin-features/service-management').then(
        (m) => m.serviceManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.PACKAGE_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPackages },
    loadChildren: () =>
      import('@cleansia/admin-features/package-management').then(
        (m) => m.packageManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.EXTRA_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewExtras },
    loadChildren: () =>
      import('@cleansia/admin-features/extra-management').then(
        (m) => m.extraManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.ADMIN_USER_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewAdminUsers },
    loadChildren: () =>
      import('@cleansia/admin-features/admin-user-management').then(
        (m) => m.adminUserManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.LANGUAGE_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewLanguages },
    loadChildren: () =>
      import('@cleansia/admin-features/language-management').then(
        (m) => m.languageManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.COUNTRY_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewCountries },
    loadChildren: () =>
      import('@cleansia/admin-features/country-management').then(
        (m) => m.countryManagementRoutes
      ),
  },
  {
    // Shares the country-management lib but is a sibling top-level route —
    // the two features are conceptually distinct (catalog vs operational
    // service area), and sidebar finding is easier this way.
    path: CleansiaAdminRoute.SERVICE_AREA_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewServiceCities },
    loadChildren: () =>
      import('@cleansia/admin-features/country-management').then(
        (m) => m.serviceAreaManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.CURRENCY_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewCurrencies },
    loadChildren: () =>
      import('@cleansia/admin-features/currency-management').then(
        (m) => m.currencyManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.LEGAL_DOCUMENTS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewLegalDocuments },
    loadChildren: () =>
      import('@cleansia/admin-features/legal-documents').then(
        (m) => m.legalDocumentsRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.EMPLOYEE_DOCUMENTS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewEmployeeDocumentsAdmin },
    loadChildren: () =>
      import('@cleansia/admin-features/employee-document-config').then(
        (m) => m.employeeDocumentConfigRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.COMPANY_INFO,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewCompanyInfo },
    loadChildren: () =>
      import('@cleansia/admin-features/company-management').then(
        (m) => m.companyManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.COMPANY_SETTINGS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewTenantConfigurations },
    loadChildren: () =>
      import('@cleansia/admin-features/company-settings').then(
        (m) => m.companySettingsRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.COMPANY_LIFECYCLE,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewCompanyLifecycle },
    loadChildren: () =>
      import('@cleansia/admin-features/company-lifecycle').then(
        (m) => m.companyLifecycleRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPayConfigs },
    loadChildren: () =>
      import('@cleansia/admin-features/pay-config-management').then(
        (m) => m.payConfigManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.TEMPLATE_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewEmailTemplates },
    loadChildren: () =>
      import('@cleansia/admin-features/template-management').then(
        (m) => m.templateManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.FISCAL_FAILURES,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanManageFiscalFailures },
    loadChildren: () =>
      import('@cleansia/admin-features/fiscal-failures').then(
        (m) => m.fiscalFailuresRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.LOYALTY_PROMOS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPromoCodes },
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-promo-codes').then(
        (m) => m.promoCodesRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.LOYALTY_TIERS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewLoyaltyTierConfigs },
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-tier-configs').then(
        (m) => m.loyaltyTiersRoutes
      ),
  },
  {
    // The per-CUSTOMER screen, not a loyalty screen. It began as one and kept the name while it grew
    // a credit balance, a credit ledger and an issue-credit action, at which point the URL an admin
    // reads in their address bar was describing a third of the page. The Nx library behind it is
    // still called loyalty-user-detail: renaming that touches the path alias, the project graph and
    // every import for no reader-visible gain, so it is deliberately left where it is.
    path: CleansiaAdminRoute.CUSTOMERS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewOrderCustomer },
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-user-detail').then(
        (m) => m.customerDetailRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.LOYALTY_REFERRALS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewReferrals },
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-referrals').then(
        (m) => m.loyaltyReferralsRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.MEMBERSHIP_PLAN_MANAGEMENT,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewMembershipPlans },
    loadChildren: () =>
      import('@cleansia/admin-features/membership-plan-management').then(
        (m) => m.membershipPlanManagementRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.MARKETING,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanSendSitewidePromo },
    loadChildren: () =>
      import('@cleansia/admin-features/marketing').then(
        (m) => m.marketingRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.DATA_PROTECTION,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewGdprRequests },
    loadChildren: () =>
      import('@cleansia/admin-features/data-protection').then(
        (m) => m.dataProtectionRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.AUDIT_LOG,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewAuditLog },
    loadChildren: () =>
      import('@cleansia/admin-features/audit-log').then(
        (m) => m.auditLogRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.NOTIFICATIONS,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewAdminNotifications },
    loadChildren: () =>
      import('@cleansia/admin-features/notifications').then(
        (m) => m.notificationsRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.PROFILE,
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.Authenticated },
    loadChildren: () =>
      import('@cleansia/admin-features/admin-profile').then(
        (m) => m.adminProfileRoutes
      ),
  },
  {
    path: CleansiaAdminRoute.UNAUTHORIZED,
    loadComponent: () =>
      import('./unauthorized/unauthorized.component').then(
        (m) => m.UnauthorizedComponent
      ),
  },
  {
    path: CommonRoute.NOT_FOUND,
    component: CleansiaNotFoundComponent,
    data: { title: 'page_titles.admin.not_found' },
  },
  {
    path: '**',
    redirectTo: CommonRoute.NOT_FOUND,
  },
];
