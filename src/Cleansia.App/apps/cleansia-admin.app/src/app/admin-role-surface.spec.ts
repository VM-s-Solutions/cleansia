import { Route } from '@angular/router';
import { adminGuard, permissionGuard } from '@cleansia/admin-services';
import { SidebarMenuItem } from '@cleansia/components';
import { CommonRoute, PermissionService, Policy, POLICY_MAP } from '@cleansia/services';
import { ADMIN_MENU_ITEMS, PREFERRED_LANDING_ROUTE, resolveLandingRoute } from './admin-menu';
import { appRoutes } from './app.routes';

/**
 * The role hint has two surfaces — the sidebar and the route table — and each is only as complete
 * as its least-gated entry: one item without a permission is a page every role can open into a
 * 403. Both arrays are walked here so a new area cannot ship ungated.
 */
const UNGATED_PATHS = new Set(['', 'login', 'unauthorized', CommonRoute.NOT_FOUND, '**']);

function flatten(items: readonly SidebarMenuItem[]): SidebarMenuItem[] {
  return items.flatMap((item) => [item, ...flatten(item.children ?? [])]);
}

function policiesOf(permission: string | string[] | undefined): string[] {
  return permission === undefined ? [] : Array.isArray(permission) ? permission : [permission];
}

describe('admin role surface', () => {
  const knownPolicies = new Set(Object.keys(POLICY_MAP));

  describe('routes', () => {
    const gated = appRoutes.filter((route) => !UNGATED_PATHS.has(route.path ?? ''));

    it('covers every top-level area', () => {
      expect(gated.length).toBeGreaterThanOrEqual(30);
    });

    it.each(gated.map((route): [string, Route] => [route.path ?? '', route]))(
      '%s carries a mapped permission behind the admin and permission guards',
      (_path, route) => {
        const permission = route.data?.['permission'];
        expect(typeof permission).toBe('string');
        expect(knownPolicies.has(permission)).toBe(true);
        expect(route.canActivate).toEqual([adminGuard, permissionGuard]);
      }
    );

    it('lands the empty path on the role-resolved home', () => {
      const home = appRoutes.find((route) => route.path === '');
      expect(typeof home?.redirectTo).toBe('function');
      expect(home?.pathMatch).toBe('full');
    });
  });

  describe('sidebar', () => {
    const entries = flatten(ADMIN_MENU_ITEMS);

    it.each(entries.map((item): [string, SidebarMenuItem] => [item.label, item]))(
      '%s carries at least one mapped permission',
      (_label, item) => {
        const policies = policiesOf(item.permission);
        expect(policies.length).toBeGreaterThan(0);
        expect(policies.every((p) => knownPolicies.has(p))).toBe(true);
      }
    );

    it('gates every routed entry with the permission its route carries', () => {
      const routePermission = new Map(
        appRoutes.map((route) => [`/${route.path}`, route.data?.['permission'] as string | undefined])
      );
      const mismatched = entries
        .filter((item) => item.route)
        .map((item) => {
          const top = '/' + (item.route as string).split('/').filter(Boolean).slice(0, 2).join('/');
          const expected =
            routePermission.get(top) ?? routePermission.get('/' + (item.route as string).split('/')[1]);
          return { route: item.route, expected, actual: policiesOf(item.permission) };
        })
        .filter(({ expected, actual }) => expected !== undefined && !actual.includes(expected));

      expect(mismatched).toEqual([]);
    });
  });

  describe('landing', () => {
    function permissions(allowed: (policy: string) => boolean): PermissionService {
      return { hasPolicy: jest.fn(allowed) } as unknown as PermissionService;
    }

    it('prefers the oversight list whenever the role can open it', () => {
      expect(resolveLandingRoute(ADMIN_MENU_ITEMS, permissions(() => true))).toBe(
        PREFERRED_LANDING_ROUTE
      );
    });

    it('falls back to the first page the role can open', () => {
      const accountantLike = permissions((p) => p === Policy.CanViewPayPeriodsAdmin);

      expect(resolveLandingRoute(ADMIN_MENU_ITEMS, accountantLike)).toBe('/pay-periods');
    });

    it('sends a role that can open nothing to /unauthorized', () => {
      expect(resolveLandingRoute(ADMIN_MENU_ITEMS, permissions(() => false))).toBe('/unauthorized');
    });
  });
});
