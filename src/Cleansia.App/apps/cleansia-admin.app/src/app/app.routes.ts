import { inject } from '@angular/core';
import { Route } from '@angular/router';
import { adminGuard, guestGuard, permissionGuard } from '@cleansia/admin-services';
import { CleansiaNotFoundComponent } from '@cleansia/components';
import { CommonRoute, PermissionService, Policy } from '@cleansia/services';
import { ADMIN_MENU_ITEMS, resolveLandingRoute } from './admin-menu';

export const appRoutes: Route[] = [
  {
    path: '',
    redirectTo: () => resolveLandingRoute(ADMIN_MENU_ITEMS, inject(PermissionService)),
    pathMatch: 'full',
  },
  {
    path: 'login',
    loadChildren: () =>
      import('@cleansia/admin-features/admin-login').then(
        (m) => m.adminLoginRoutes
      ),
    canActivate: [guestGuard],
  },
  {
    path: 'employee-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPagedEmployee },
    loadChildren: () =>
      import('@cleansia/admin-features/employee-management').then(
        (m) => m.employeeManagementRoutes
      ),
  },
  {
    path: 'pay-periods',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPayPeriodsAdmin },
    loadChildren: () =>
      import('@cleansia.app/pay-periods').then((m) => m.payPeriodsRoutes),
  },
  {
    path: 'order-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPagedOrderAdmin },
    loadChildren: () =>
      import('@cleansia/admin-features/order-management').then(
        (m) => m.orderManagementRoutes
      ),
  },
  {
    path: 'dispute-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewDisputeListAdmin },
    loadChildren: () =>
      import('@cleansia/admin-features/disputes-management').then(
        (m) => m.disputesManagementRoutes
      ),
  },
  {
    path: 'invoice-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPagedInvoicesAdmin },
    loadChildren: () =>
      import('@cleansia/admin-features/invoice-management').then(
        (m) => m.invoiceManagementRoutes
      ),
  },
  {
    path: 'reports',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewRevenueReport },
    loadChildren: () =>
      import('@cleansia/admin-features/reports').then((m) => m.reportsRoutes),
  },
  {
    path: 'service-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewServices },
    loadChildren: () =>
      import('@cleansia/admin-features/service-management').then(
        (m) => m.serviceManagementRoutes
      ),
  },
  {
    path: 'package-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPackages },
    loadChildren: () =>
      import('@cleansia/admin-features/package-management').then(
        (m) => m.packageManagementRoutes
      ),
  },
  {
    path: 'extra-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewExtras },
    loadChildren: () =>
      import('@cleansia/admin-features/extra-management').then(
        (m) => m.extraManagementRoutes
      ),
  },
  {
    path: 'admin-user-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewAdminUsers },
    loadChildren: () =>
      import('@cleansia/admin-features/admin-user-management').then(
        (m) => m.adminUserManagementRoutes
      ),
  },
  {
    path: 'language-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewLanguages },
    loadChildren: () =>
      import('@cleansia/admin-features/language-management').then(
        (m) => m.languageManagementRoutes
      ),
  },
  {
    path: 'country-management',
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
    path: 'service-area-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewServiceCities },
    loadChildren: () =>
      import('@cleansia/admin-features/country-management').then(
        (m) => m.serviceAreaManagementRoutes
      ),
  },
  {
    path: 'currency-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewCurrencies },
    loadChildren: () =>
      import('@cleansia/admin-features/currency-management').then(
        (m) => m.currencyManagementRoutes
      ),
  },
  {
    path: 'legal-documents',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewLegalDocuments },
    loadChildren: () =>
      import('@cleansia/admin-features/legal-documents').then(
        (m) => m.legalDocumentsRoutes
      ),
  },
  {
    path: 'employee-documents',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewEmployeeDocumentsAdmin },
    loadChildren: () =>
      import('@cleansia/admin-features/employee-document-config').then(
        (m) => m.employeeDocumentConfigRoutes
      ),
  },
  {
    path: 'company-info',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewCompanyInfo },
    loadChildren: () =>
      import('@cleansia/admin-features/company-management').then(
        (m) => m.companyManagementRoutes
      ),
  },
  {
    path: 'company-settings',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewTenantConfigurations },
    loadChildren: () =>
      import('@cleansia/admin-features/company-settings').then(
        (m) => m.companySettingsRoutes
      ),
  },
  {
    path: 'company-lifecycle',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewCompanyLifecycle },
    loadChildren: () =>
      import('@cleansia/admin-features/company-lifecycle').then(
        (m) => m.companyLifecycleRoutes
      ),
  },
  {
    path: 'pay-config-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPayConfigs },
    loadChildren: () =>
      import('@cleansia/admin-features/pay-config-management').then(
        (m) => m.payConfigManagementRoutes
      ),
  },
  {
    path: 'template-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewEmailTemplates },
    loadChildren: () =>
      import('@cleansia/admin-features/template-management').then(
        (m) => m.templateManagementRoutes
      ),
  },
  {
    path: 'fiscal-failures',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanManageFiscalFailures },
    loadChildren: () =>
      import('@cleansia/admin-features/fiscal-failures').then(
        (m) => m.fiscalFailuresRoutes
      ),
  },
  {
    path: 'loyalty/promos',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewPromoCodes },
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-promo-codes').then(
        (m) => m.promoCodesRoutes
      ),
  },
  {
    path: 'loyalty/tiers',
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
    path: 'customers',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewOrderCustomer },
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-user-detail').then(
        (m) => m.customerDetailRoutes
      ),
  },
  {
    path: 'loyalty/referrals',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewReferrals },
    loadChildren: () =>
      import('@cleansia/admin-features/loyalty-referrals').then(
        (m) => m.loyaltyReferralsRoutes
      ),
  },
  {
    path: 'membership-plan-management',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewMembershipPlans },
    loadChildren: () =>
      import('@cleansia/admin-features/membership-plan-management').then(
        (m) => m.membershipPlanManagementRoutes
      ),
  },
  {
    path: 'marketing',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanSendSitewidePromo },
    loadChildren: () =>
      import('@cleansia/admin-features/marketing').then(
        (m) => m.marketingRoutes
      ),
  },
  {
    path: 'data-protection',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewGdprRequests },
    loadChildren: () =>
      import('@cleansia/admin-features/data-protection').then(
        (m) => m.dataProtectionRoutes
      ),
  },
  {
    path: 'audit-log',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewAuditLog },
    loadChildren: () =>
      import('@cleansia/admin-features/audit-log').then(
        (m) => m.auditLogRoutes
      ),
  },
  {
    path: 'notifications',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.CanViewAdminNotifications },
    loadChildren: () =>
      import('@cleansia/admin-features/notifications').then(
        (m) => m.notificationsRoutes
      ),
  },
  {
    path: 'profile',
    canActivate: [adminGuard, permissionGuard],
    data: { permission: Policy.Authenticated },
    loadChildren: () =>
      import('@cleansia/admin-features/admin-profile').then(
        (m) => m.adminProfileRoutes
      ),
  },
  {
    path: 'unauthorized',
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
