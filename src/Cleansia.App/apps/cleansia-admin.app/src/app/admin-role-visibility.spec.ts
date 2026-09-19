import { EnvironmentInjector, runInInjectionContext } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, UrlTree } from '@angular/router';
import { permissionGuard } from '@cleansia/admin-services';
import { isSidebarItemAllowed } from '@cleansia/components';
import { AdminRoleName, AUTH_COOKIE_KEYS, PermissionService, Role } from '@cleansia/services';
import { ADMIN_MENU_ITEMS, resolveLandingRoute } from './admin-menu';
import { appRoutes } from './app.routes';
import { ADMIN_AUTH_COOKIE_KEYS } from './auth/admin-auth-cookie-keys';

/**
 * What a Support and an Accountant session actually see, end to end: the stored role, the real
 * `PermissionService`, the mirror map, the sidebar array and the route table composed, so a policy
 * moved on any one of them shows up as a page gained or lost by a named role — not as four green
 * specs that only agree in the reviewer's head.
 */
const SUPPORT_SIDEBAR = [
  'sidebar.notifications',
  'sidebar.employees',
  'sidebar.orders',
  'sidebar.disputes',
  'sidebar.services',
  'sidebar.packages',
  'sidebar.extras',
  'sidebar.languages',
  'sidebar.countries',
  'sidebar.service_area',
  'sidebar.currencies',
  'sidebar.employee_documents',
  'sidebar.company_info',
  'sidebar.templates',
  'sidebar.loyalty',
  'sidebar.memberships',
  'sidebar.data_protection',
  'sidebar.audit_log',
  'sidebar.profile',
];

const ACCOUNTANT_SIDEBAR = [
  'sidebar.notifications',
  'sidebar.employees',
  'sidebar.pay_periods',
  'sidebar.invoices',
  'sidebar.reports',
  'sidebar.services',
  'sidebar.packages',
  'sidebar.extras',
  'sidebar.global_rates',
  'sidebar.languages',
  'sidebar.countries',
  'sidebar.service_area',
  'sidebar.currencies',
  'sidebar.company_info',
  'sidebar.templates',
  'sidebar.fiscal_failures',
  'sidebar.loyalty',
  'sidebar.memberships',
  'sidebar.profile',
];

const NEITHER_ROLE_SEES = [
  'sidebar.admin_users',
  'sidebar.legal_documents',
  'sidebar.company_settings',
  'sidebar.company_lifecycle',
  'sidebar.marketing',
];

describe('admin role visibility', () => {
  let permissions: PermissionService;

  function signIn(adminRole: AdminRoleName): void {
    localStorage.setItem(ADMIN_AUTH_COOKIE_KEYS.role, Role.ADMINISTRATOR);
    localStorage.setItem(ADMIN_AUTH_COOKIE_KEYS.adminRole as string, adminRole);
  }

  function visibleSidebar(): string[] {
    return ADMIN_MENU_ITEMS.filter((item) => isSidebarItemAllowed(item, permissions)).map(
      (item) => item.label
    );
  }

  function guard(path: string): true | UrlTree {
    const route = appRoutes.find((r) => r.path === path);
    if (!route) throw new Error(`no top-level route '${path}'`);
    return runInInjectionContext(TestBed.inject(EnvironmentInjector), () =>
      permissionGuard({ data: route.data } as ActivatedRouteSnapshot, null as never)
    ) as true | UrlTree;
  }

  function landsOn(result: true | UrlTree): string {
    return result === true ? 'the page' : result.toString();
  }

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AUTH_COOKIE_KEYS, useValue: ADMIN_AUTH_COOKIE_KEYS }],
    });
    permissions = TestBed.inject(PermissionService);
  });

  afterEach(() => localStorage.clear());

  describe('a Support session', () => {
    beforeEach(() => signIn(AdminRoleName.SUPPORT));

    it('sees the order-ops, cleaner, data-protection, audit and catalogue entries and no money or company entry', () => {
      expect(visibleSidebar()).toEqual(SUPPORT_SIDEBAR);
    });

    it('is turned away from /reports and admitted to a customer', () => {
      expect(landsOn(guard('reports'))).toBe('/unauthorized');
      expect(landsOn(guard('customers'))).toBe('the page');
    });

    it('lands on a page it can open', () => {
      const landing = resolveLandingRoute(ADMIN_MENU_ITEMS, permissions);

      expect(ADMIN_MENU_ITEMS.find((item) => item.route === landing)?.label).toBe(
        'sidebar.employees'
      );
    });
  });

  describe('an Accountant session', () => {
    beforeEach(() => signIn(AdminRoleName.ACCOUNTANT));

    it('sees the money, fiscal, company-info, employee-list and catalogue entries and no order, customer or document entry', () => {
      expect(visibleSidebar()).toEqual(ACCOUNTANT_SIDEBAR);
    });

    it('is admitted to /reports and turned away from a customer and the employee documents', () => {
      expect(landsOn(guard('reports'))).toBe('the page');
      expect(landsOn(guard('customers'))).toBe('/unauthorized');
      expect(landsOn(guard('employee-documents'))).toBe('/unauthorized');
      expect(landsOn(guard('order-management'))).toBe('/unauthorized');
    });

    it('lands on a page it can open', () => {
      const landing = resolveLandingRoute(ADMIN_MENU_ITEMS, permissions);

      expect(ADMIN_MENU_ITEMS.find((item) => item.route === landing)?.label).toBe(
        'sidebar.employees'
      );
    });
  });

  it('keeps the administrator-only and manager-only entries from both branches', () => {
    for (const adminRole of [AdminRoleName.SUPPORT, AdminRoleName.ACCOUNTANT]) {
      signIn(adminRole);

      expect(visibleSidebar()).toEqual(expect.not.arrayContaining(NEITHER_ROLE_SEES));
    }
  });

  it('names every sidebar entry exactly once across the three lists', () => {
    const named = [...new Set([...SUPPORT_SIDEBAR, ...ACCOUNTANT_SIDEBAR, ...NEITHER_ROLE_SEES])];

    expect(named.sort()).toEqual(ADMIN_MENU_ITEMS.map((item) => item.label).sort());
  });
});
