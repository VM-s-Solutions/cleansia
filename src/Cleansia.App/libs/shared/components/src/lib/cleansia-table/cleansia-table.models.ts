export interface CleansiaTableColumn {
  field: string;
  header: string;
  sortable?: boolean;
  pipe?: 'date' | 'number' | 'currency';
  pipeArgs?: any[];
  width?: string;
  class?: string;
  headerClass?: string;
}

export interface CleansiaTableAction {
  label: string;
  icon?: string;
  class?: string;
  action: (item: any) => void;
  visible?: (item: any) => boolean;
  disabled?: (item: any) => boolean;
}
