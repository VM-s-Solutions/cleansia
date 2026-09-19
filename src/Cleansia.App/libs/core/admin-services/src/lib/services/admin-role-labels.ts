import { AdminRole } from '../client/admin-client';

/**
 * One label key per administrator role, read by every surface that prints one (the
 * administrators list and form, the audit log) so the four names are translated once.
 */
export const ADMIN_ROLE_LABEL_KEYS: Readonly<Record<AdminRole, string>> = {
  [AdminRole.Administrator]: 'admin_roles.administrator',
  [AdminRole.Manager]: 'admin_roles.manager',
  [AdminRole.Support]: 'admin_roles.support',
  [AdminRole.Accountant]: 'admin_roles.accountant',
};

export const ADMIN_ROLES: readonly AdminRole[] = [
  AdminRole.Administrator,
  AdminRole.Manager,
  AdminRole.Support,
  AdminRole.Accountant,
];

export function getAdminRoleLabelKey(role: AdminRole | undefined): string {
  return role === undefined ? '' : (ADMIN_ROLE_LABEL_KEYS[role] ?? '');
}
