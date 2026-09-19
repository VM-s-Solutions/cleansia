import { PermissionService } from '@cleansia/services';

export interface SidebarMenuItem {
  label: string;
  icon?: string;
  route?: string;
  children?: SidebarMenuItem[];
  expanded?: boolean;
  badge?: number | string;
  onClickFn?: () => void;
  /**
   * Optional Policy name(s) gating this entry. When set, the item renders
   * only if the current user satisfies at least one policy — same engine as
   * `*cleansiaPermission` (PermissionService), applied to the data-driven
   * menu where a structural directive cannot attach.
   */
  permission?: string | string[];
}

export function isSidebarItemAllowed(
  item: SidebarMenuItem,
  permissions: PermissionService
): boolean {
  if (!item.permission) return true;
  const policies = Array.isArray(item.permission) ? item.permission : [item.permission];
  return policies.some((p) => permissions.hasPolicy(p));
}

/**
 * The widest viewport the signed-in shell treats as mobile. The stylesheets collapse the desktop
 * rail with `max-width: 768px`, which includes 768 itself, so the script that decides whether the
 * mobile toolbar renders must include it too — one predicate, or 768 px has no navigation at all.
 */
export const MOBILE_VIEWPORT_MAX_PX = 768;

export function isMobileViewport(innerWidth: number): boolean {
  return innerWidth <= MOBILE_VIEWPORT_MAX_PX;
}
