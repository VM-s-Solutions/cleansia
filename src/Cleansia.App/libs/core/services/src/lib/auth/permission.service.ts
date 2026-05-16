import { Injectable, inject } from '@angular/core';
import { Role } from '../enums/role.enum';
import { AUTH_COOKIE_KEYS } from './auth-cookie-keys';
import { PhysicalPolicy } from './physical-policy';
import { PolicyName, resolvePhysicalPolicy } from './policy';

/**
 * Resolves whether the currently signed-in user satisfies a given Policy.
 * Defense-in-depth UI gate that mirrors the backend's `[Permission]`
 * attribute — the backend is authoritative; this directive's purpose is to
 * keep users from seeing actions they'll just get a 403 for.
 *
 * Reads the role from localStorage (persisted by the auth service from the
 * server's login/refresh response). With HttpOnly cookies the JWT itself is
 * no longer JS-readable, so the explicit `role` field on `JwtTokenResponse`
 * is the source for this UI hint. Stale data is the cost of UI-only gating;
 * the backend remains authoritative.
 */
@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly cookieKeys = inject(AUTH_COOKIE_KEYS);

  /** Returns true if the current user satisfies the policy's role gate. */
  hasPolicy(policy: PolicyName | string): boolean {
    const physical = resolvePhysicalPolicy(policy);
    return this.satisfies(physical);
  }

  /** Returns true if the current user satisfies the physical role gate directly. */
  satisfies(physical: PhysicalPolicy): boolean {
    if (physical === PhysicalPolicy.Anonymous) return true;

    const role = this.currentRole();
    const isAuthenticated = role !== null;

    switch (physical) {
      case PhysicalPolicy.Authenticated:
        return isAuthenticated;
      case PhysicalPolicy.CustomerOnly:
        return role === Role.CUSTOMER;
      case PhysicalPolicy.EmployeeOrAdmin:
        return role === Role.EMPLOYEE || role === Role.ADMINISTRATOR;
      case PhysicalPolicy.AdminOnly:
        return role === Role.ADMINISTRATOR;
      case PhysicalPolicy.OwnerOrElevated:
        // Frontend can't compute "owner" without context — admins/employees
        // always pass; for owner-checks the caller must scope the directive
        // to a specific entity and check ownership server-side. Fail safe:
        // hide for plain customers without their own ownership context.
        return role === Role.EMPLOYEE || role === Role.ADMINISTRATOR;
      default:
        return false;
    }
  }

  /** Returns the role attached to the most recent login/refresh response,
   *  or null if not signed in. */
  currentRole(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem(this.cookieKeys.role);
  }
}
