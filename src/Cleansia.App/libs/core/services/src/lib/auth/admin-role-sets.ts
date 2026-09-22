import { AdminRoleName } from '../enums/admin-role.enum';
import { PhysicalPolicy } from './physical-policy';

export type AdminSetPolicy =
  | PhysicalPolicy.AdministratorOnly
  | PhysicalPolicy.ManagerOrAbove
  | PhysicalPolicy.SupportOrAbove
  | PhysicalPolicy.AccountantOrAbove;

/**
 * Frontend mirror of `Cleansia.Core.AppServices.Authentication.AdminRoleSets` — the one place a
 * set is spelled. `AdminOnly` is any administrator and therefore not listed.
 */
export const ADMIN_ROLE_SETS: Readonly<Record<AdminSetPolicy, readonly AdminRoleName[]>> = {
  [PhysicalPolicy.AdministratorOnly]: [AdminRoleName.ADMINISTRATOR],
  [PhysicalPolicy.ManagerOrAbove]: [AdminRoleName.ADMINISTRATOR, AdminRoleName.MANAGER],
  [PhysicalPolicy.SupportOrAbove]: [
    AdminRoleName.ADMINISTRATOR,
    AdminRoleName.MANAGER,
    AdminRoleName.SUPPORT,
  ],
  [PhysicalPolicy.AccountantOrAbove]: [
    AdminRoleName.ADMINISTRATOR,
    AdminRoleName.MANAGER,
    AdminRoleName.ACCOUNTANT,
  ],
};

export function isAdminSetPolicy(physical: PhysicalPolicy): physical is AdminSetPolicy {
  return physical in ADMIN_ROLE_SETS;
}
