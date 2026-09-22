import { CleansiaAdminRoute, CommonRoute } from '@cleansia/services';
import { ADMIN_MENU_ITEMS } from './admin-menu';
import { appRoutes } from './app.routes';

/**
 * Every admin path is spelled once, in `CleansiaAdminRoute`: the route table, the sidebar and the
 * 120-odd navigations in the feature libs all read the same member, so a renamed path cannot
 * leave a sidebar entry pointing at a page that no longer answers.
 */
describe('admin routes', () => {
  const members = new Set<string>([
    ...Object.values(CleansiaAdminRoute),
    ...Object.values(CommonRoute),
  ]);

  const menuRoutes = (items: typeof ADMIN_MENU_ITEMS): string[] =>
    items.flatMap((item) => [
      ...(item.route ? [item.route] : []),
      ...(item.children ? menuRoutes(item.children) : []),
    ]);

  it('declares every route table path as a route enum member', () => {
    const strangers = appRoutes
      .map((route) => route.path ?? '')
      .filter((path) => path !== '**' && !members.has(path));

    expect(strangers).toEqual([]);
  });

  it('points every sidebar entry at a route enum member, optionally into its children', () => {
    const strangers = menuRoutes(ADMIN_MENU_ITEMS).filter(
      (route) =>
        ![...members].some(
          (member) => member !== '' && (route === `/${member}` || route.startsWith(`/${member}/`))
        )
    );

    expect(strangers).toEqual([]);
  });

  it('opens every sidebar entry on a route the table serves', () => {
    const served = appRoutes.map((route) => route.path ?? '');
    const unserved = menuRoutes(ADMIN_MENU_ITEMS).filter(
      (route) => !served.some((path) => path !== '' && (route === `/${path}` || route.startsWith(`/${path}/`)))
    );

    expect(unserved).toEqual([]);
  });
});
