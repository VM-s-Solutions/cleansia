export interface SidebarMenuItem {
  label: string;
  icon?: string;
  route?: string;
  children?: SidebarMenuItem[];
  expanded?: boolean;
  badge?: number | string;
  onClickFn?: () => void;
}
