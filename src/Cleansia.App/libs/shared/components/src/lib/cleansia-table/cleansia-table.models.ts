import { TemplateRef } from '@angular/core';

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

export interface TableColumn<T = any> {
  id: string;
  headerName: string;
  value?: string | ((row?: T) => any);
  template?: TemplateRef<T>;
  sortable?: boolean;
  columnClass?: string;
  columnActions?: TableColumnAction<T>[];
}

export interface TableColumnAction<T = any> {
  icon: string;
  onClick: (row: T) => void;
  buttonPalette?: string;
  tooltip?: {
    title: string;
    position: 'above' | 'below' | 'left' | 'right';
  };
  visible?: (row: T) => boolean;
  disabled?: (row: T) => boolean;
}

export interface TableDefinition<T = any> {
  columns: TableColumn<T>[];
  onRowClick?: (row: T) => void;
}
