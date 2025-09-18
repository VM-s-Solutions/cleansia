export interface SidebarMenuItem {
  label: string;
  icon?: string;
  route?: string;
  children?: SidebarMenuItem[];
  expanded?: boolean;
  onClickFn?: () => void;
}
