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
    route: `/${CleansiaAdminRoute.EMPLOYEE_MANAGEMENT}`,
    permission: Policy.CanViewPagedEmployee,
  },
  {
    label: 'sidebar.pay_periods',
    icon: 'pi pi-calendar',
    route: `/${CleansiaAdminRoute.PAY_PERIODS}`,
    permission: Policy.CanViewPayPeriodsAdmin,
  },
  {
    label: 'sidebar.orders',
    icon: 'pi pi-shopping-cart',
    route: `/${CleansiaAdminRoute.ORDER_MANAGEMENT}`,
    permission: Policy.CanViewPagedOrderAdmin,
  },
  {
    label: 'sidebar.disputes',
    icon: 'pi pi-flag',
    route: `/${CleansiaAdminRoute.DISPUTE_MANAGEMENT}`,
    permission: Policy.CanViewDisputeListAdmin,
  },
  {
    label: 'sidebar.invoices',
    icon: 'pi pi-file',
    route: `/${CleansiaAdminRoute.INVOICE_MANAGEMENT}`,
    permission: Policy.CanViewPagedInvoicesAdmin,
  },
  {
    label: 'sidebar.reports',
    icon: 'pi pi-chart-bar',
    route: `/${CleansiaAdminRoute.REPORTS}`,
    permission: Policy.CanViewRevenueReport,
  },
  {
    label: 'sidebar.services',
    icon: 'pi pi-wrench',
    route: `/${CleansiaAdminRoute.SERVICE_MANAGEMENT}`,
    permission: Policy.CanViewServices,
  },
  {
    label: 'sidebar.packages',
    icon: 'pi pi-box',
    route: `/${CleansiaAdminRoute.PACKAGE_MANAGEMENT}`,
    permission: Policy.CanViewPackages,
  },
  {
    label: 'sidebar.extras',
    icon: 'pi pi-plus-circle',
    route: `/${CleansiaAdminRoute.EXTRA_MANAGEMENT}`,
    permission: Policy.CanViewExtras,
  },
  {
    label: 'sidebar.global_rates',
    icon: 'pi pi-money-bill',
    route: `/${CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT}`,
    permission: Policy.CanViewPayConfigs,
  },
  {
    label: 'sidebar.admin_users',
    icon: 'pi pi-user-plus',
    route: `/${CleansiaAdminRoute.ADMIN_USER_MANAGEMENT}`,
    permission: Policy.CanViewAdminUsers,
  },
  {
    label: 'sidebar.languages',
    icon: 'pi pi-globe',
    route: `/${CleansiaAdminRoute.LANGUAGE_MANAGEMENT}`,
    permission: Policy.CanViewLanguages,
  },
  {
    label: 'sidebar.countries',
    icon: 'pi pi-map',
    route: `/${CleansiaAdminRoute.COUNTRY_MANAGEMENT}`,
    permission: Policy.CanViewCountries,
  },
  {
    label: 'sidebar.service_area',
    icon: 'pi pi-map-marker',
    route: `/${CleansiaAdminRoute.SERVICE_AREA_MANAGEMENT}`,
    permission: Policy.CanViewServiceCities,
  },
  {
    label: 'sidebar.currencies',
    icon: 'pi pi-dollar',
    route: `/${CleansiaAdminRoute.CURRENCY_MANAGEMENT}`,
    permission: Policy.CanViewCurrencies,
  },
  {
    label: 'sidebar.legal_documents',
    icon: 'pi pi-file-check',
    route: `/${CleansiaAdminRoute.LEGAL_DOCUMENTS}`,
    permission: Policy.CanViewLegalDocuments,
  },
  {
    label: 'sidebar.employee_documents',
    icon: 'pi pi-id-card',
    route: `/${CleansiaAdminRoute.EMPLOYEE_DOCUMENTS}`,
    permission: Policy.CanViewEmployeeDocumentsAdmin,
    children: [
      {
        label: 'sidebar.employee_document_requirements',
        icon: 'pi pi-list-check',
        route: `/${CleansiaAdminRoute.EMPLOYEE_DOCUMENTS}`,
        permission: Policy.CanViewEmployeeDocumentsAdmin,
      },
      {
        label: 'sidebar.employee_document_deletion_requests',
        icon: 'pi pi-trash',
        route: `/${CleansiaAdminRoute.EMPLOYEE_DOCUMENTS}/deletion-requests`,
        permission: Policy.CanViewEmployeeDocumentsAdmin,
      },
    ],
  },
  {
    label: 'sidebar.company_info',
    icon: 'pi pi-building',
    route: `/${CleansiaAdminRoute.COMPANY_INFO}`,
    permission: Policy.CanViewCompanyInfo,
  },
  {
    label: 'sidebar.company_settings',
    icon: 'pi pi-sliders-h',
    route: `/${CleansiaAdminRoute.COMPANY_SETTINGS}`,
    permission: Policy.CanViewTenantConfigurations,
  },
  {
    label: 'sidebar.company_lifecycle',
    icon: 'pi pi-power-off',
    route: `/${CleansiaAdminRoute.COMPANY_LIFECYCLE}`,
    permission: Policy.CanViewCompanyLifecycle,
  },
  {
    label: 'sidebar.templates',
    icon: 'pi pi-file-edit',
    route: `/${CleansiaAdminRoute.TEMPLATE_MANAGEMENT}`,
    permission: Policy.CanViewEmailTemplates,
  },
  {
    label: 'sidebar.fiscal_failures',
    icon: 'pi pi-exclamation-triangle',
    route: `/${CleansiaAdminRoute.FISCAL_FAILURES}`,
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
        route: `/${CleansiaAdminRoute.LOYALTY_PROMOS}`,
        permission: Policy.CanViewPromoCodes,
      },
      {
        label: 'sidebar.loyalty_tiers',
        icon: 'pi pi-chart-line',
        route: `/${CleansiaAdminRoute.LOYALTY_TIERS}`,
        permission: Policy.CanViewLoyaltyTierConfigs,
      },
      {
        label: 'sidebar.loyalty_referrals',
        icon: 'pi pi-share-alt',
        route: `/${CleansiaAdminRoute.LOYALTY_REFERRALS}`,
        permission: Policy.CanViewReferrals,
      },
    ],
  },
  {
    label: 'sidebar.memberships',
    icon: 'pi pi-id-card',
    route: `/${CleansiaAdminRoute.MEMBERSHIP_PLAN_MANAGEMENT}`,
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
        route: `/${CleansiaAdminRoute.MARKETING}/sitewide-push`,
        permission: Policy.CanSendSitewidePromo,
      },
    ],
  },
  {
    label: 'sidebar.data_protection',
    icon: 'pi pi-shield',
    route: `/${CleansiaAdminRoute.DATA_PROTECTION}`,
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
    route: `/${CleansiaAdminRoute.AUDIT_LOG}`,
    permission: Policy.CanViewAuditLog,
  },
  {
    label: 'sidebar.profile',
    icon: 'pi pi-user',
    route: `/${CleansiaAdminRoute.PROFILE}`,
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
