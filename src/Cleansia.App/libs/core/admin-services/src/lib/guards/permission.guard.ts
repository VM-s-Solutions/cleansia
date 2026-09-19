import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { CleansiaAdminRoute, PermissionService } from '@cleansia/services';

/**
 * The route-level half of the role hint: a page whose `data.permission` the session's role lacks
 * lands on /unauthorized instead of rendering a shell that 403s on its first request. The server
 * is the gate; this only spares the user the empty page. A route that names no permission passes,
 * so the app's route spec is what makes every area carry one.
 */
export const permissionGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const permission = route.data['permission'] as string | undefined;
  if (!permission || inject(PermissionService).hasPolicy(permission)) {
    return true;
  }
  return inject(Router).createUrlTree([`/${CleansiaAdminRoute.UNAUTHORIZED}`]);
};
