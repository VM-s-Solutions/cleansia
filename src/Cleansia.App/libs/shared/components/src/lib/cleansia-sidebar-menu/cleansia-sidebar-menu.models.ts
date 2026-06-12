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
