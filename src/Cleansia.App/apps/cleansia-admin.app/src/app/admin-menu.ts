import { isSidebarItemAllowed, SidebarMenuItem } from '@cleansia/components';
import { CleansiaAdminRoute, PermissionService, Policy } from '@cleansia/services';

export const NOTIFICATIONS_ROUTE = `/${CleansiaAdminRoute.NOTIFICATIONS}`;

/**
 * Where a signed-in administrator lands: the oversight list when the role can see it — every
 * role can today — and otherwise the first sidebar page the role can open, so no role starts on
 * a page it lacks.
 */
export const PREFERRED_LANDING_ROUTE = `/${CleansiaAdminRoute.EMPLOYEE_MANAGEMENT}`;

/**
 * Every entry carries a permission — the app's route spec walks this array — so the sidebar is
 * the one place a role learns what it has. The server is the gate; a hidden entry is a courtesy.
 */
export const ADMIN_MENU_ITEMS: readonly SidebarMenuItem[] = [
  {
    label: 'sidebar.notifications',
    icon: 'pi pi-bell',
    route: NOTIFICATIONS_ROUTE,
    permission: Policy.CanViewAdminNotifications,
  },
  {
    label: 'sidebar.employees',
    icon: 'pi pi-users',
    route: '/employee-management',
    permission: Policy.CanViewPagedEmployee,
  },
  {
    label: 'sidebar.pay_periods',
    icon: 'pi pi-calendar',
    route: '/pay-periods',
    permission: Policy.CanViewPayPeriodsAdmin,
  },
  {
    label: 'sidebar.orders',
    icon: 'pi pi-shopping-cart',
    route: '/order-management',
    permission: Policy.CanViewPagedOrderAdmin,
  },
  {
    label: 'sidebar.disputes',
    icon: 'pi pi-flag',
    route: '/dispute-management',
    permission: Policy.CanViewDisputeListAdmin,
  },
  {
    label: 'sidebar.invoices',
    icon: 'pi pi-file',
    route: '/invoice-management',
    permission: Policy.CanViewPagedInvoicesAdmin,
  },
  {
    label: 'sidebar.reports',
    icon: 'pi pi-chart-bar',
    route: '/reports',
    permission: Policy.CanViewRevenueReport,
  },
  {
    label: 'sidebar.services',
    icon: 'pi pi-wrench',
    route: '/service-management',
    permission: Policy.CanViewServices,
  },
  {
    label: 'sidebar.packages',
    icon: 'pi pi-box',
    route: '/package-management',
    permission: Policy.CanViewPackages,
  },
  {
    label: 'sidebar.extras',
    icon: 'pi pi-plus-circle',
    route: '/extra-management',
    permission: Policy.CanViewExtras,
  },
  {
    label: 'sidebar.global_rates',
    icon: 'pi pi-money-bill',
    route: '/pay-config-management',
    permission: Policy.CanViewPayConfigs,
  },
  {
    label: 'sidebar.admin_users',
    icon: 'pi pi-user-plus',
    route: '/admin-user-management',
    permission: Policy.CanViewAdminUsers,
  },
  {
    label: 'sidebar.languages',
    icon: 'pi pi-globe',
    route: '/language-management',
    permission: Policy.CanViewLanguages,
  },
  {
    label: 'sidebar.countries',
    icon: 'pi pi-map',
    route: '/country-management',
    permission: Policy.CanViewCountries,
  },
  {
    label: 'sidebar.service_area',
    icon: 'pi pi-map-marker',
    route: '/service-area-management',
    permission: Policy.CanViewServiceCities,
  },
  {
    label: 'sidebar.currencies',
    icon: 'pi pi-dollar',
    route: '/currency-management',
    permission: Policy.CanViewCurrencies,
  },
  {
    label: 'sidebar.legal_documents',
    icon: 'pi pi-file-check',
    route: '/legal-documents',
    permission: Policy.CanViewLegalDocuments,
  },
  {
    label: 'sidebar.employee_documents',
    icon: 'pi pi-id-card',
    route: '/employee-documents',
    permission: Policy.CanViewEmployeeDocumentsAdmin,
    children: [
      {
        label: 'sidebar.employee_document_requirements',
        icon: 'pi pi-list-check',
        route: '/employee-documents',
        permission: Policy.CanViewEmployeeDocumentsAdmin,
      },
      {
        label: 'sidebar.employee_document_deletion_requests',
        icon: 'pi pi-trash',
        route: '/employee-documents/deletion-requests',
        permission: Policy.CanViewEmployeeDocumentsAdmin,
      },
    ],
  },
  {
    label: 'sidebar.company_info',
    icon: 'pi pi-building',
    route: '/company-info',
    permission: Policy.CanViewCompanyInfo,
  },
  {
    label: 'sidebar.company_settings',
    icon: 'pi pi-sliders-h',
    route: '/company-settings',
    permission: Policy.CanViewTenantConfigurations,
  },
  {
    label: 'sidebar.company_lifecycle',
    icon: 'pi pi-power-off',
    route: '/company-lifecycle',
    permission: Policy.CanViewCompanyLifecycle,
  },
  {
    label: 'sidebar.templates',
    icon: 'pi pi-file-edit',
    route: '/template-management',
    permission: Policy.CanViewEmailTemplates,
  },
  {
    label: 'sidebar.fiscal_failures',
    icon: 'pi pi-exclamation-triangle',
    route: '/fiscal-failures',
    permission: Policy.CanManageFiscalFailures,
  },
  {
    label: 'sidebar.loyalty',
    icon: 'pi pi-star',
    permission: [Policy.CanViewPromoCodes, Policy.CanViewLoyaltyTierConfigs, Policy.CanViewReferrals],
    children: [
      {
        label: 'sidebar.loyalty_promo_codes',
        icon: 'pi pi-tag',
        route: '/loyalty/promos',
        permission: Policy.CanViewPromoCodes,
      },
      {
        label: 'sidebar.loyalty_tiers',
        icon: 'pi pi-chart-line',
        route: '/loyalty/tiers',
        permission: Policy.CanViewLoyaltyTierConfigs,
      },
      {
        label: 'sidebar.loyalty_referrals',
        icon: 'pi pi-share-alt',
        route: '/loyalty/referrals',
        permission: Policy.CanViewReferrals,
      },
    ],
  },
  {
    label: 'sidebar.memberships',
    icon: 'pi pi-id-card',
    route: '/membership-plan-management',
    permission: Policy.CanViewMembershipPlans,
  },
  {
    label: 'sidebar.marketing',
    icon: 'pi pi-megaphone',
    permission: Policy.CanSendSitewidePromo,
    children: [
      {
        label: 'sidebar.marketing_sitewide_push',
        icon: 'pi pi-send',
        route: '/marketing/sitewide-push',
        permission: Policy.CanSendSitewidePromo,
      },
    ],
  },
  {
    label: 'sidebar.data_protection',
    icon: 'pi pi-shield',
    route: '/data-protection',
    permission: [
      Policy.CanViewGdprRequests,
      Policy.CanAdminViewUserConsents,
      Policy.CanAdminExportUserData,
      Policy.CanAdminDeleteUserAccount,
    ],
  },
  {
    label: 'sidebar.audit_log',
    icon: 'pi pi-history',
    route: '/audit-log',
    permission: Policy.CanViewAuditLog,
  },
  {
    label: 'sidebar.profile',
    icon: 'pi pi-user',
    route: '/profile',
    permission: Policy.Authenticated,
  },
];

export function resolveLandingRoute(
  items: readonly SidebarMenuItem[],
  permissions: PermissionService
): string {
  const visible = items.filter((item) => item.route && isSidebarItemAllowed(item, permissions));
  const preferred = visible.find((item) => item.route === PREFERRED_LANDING_ROUTE);
  return (preferred ?? visible[0])?.route ?? `/${CleansiaAdminRoute.UNAUTHORIZED}`;
}
