import { TemplateRef } from '@angular/core';

export interface TableColumn<T = any> {
  id: string;
  field: string;
  header: string;
  sortable?: boolean;
  width?: string;
  align?: 'left' | 'center' | 'right';
  customTemplate?: TemplateRef<any>;
  getValue?: (row: T) => any;
}

export interface TableAction<T = any> {
  icon: string;
  tooltip?: string;
  color?: 'warning' | 'danger' | 'success' | 'info' | 'primary';
  visible?: (row: T) => boolean;
  disabled?: (row: T) => boolean;
  onClick: (row: T) => void;
}

export interface TableConfig {
  selectable?: boolean;
  hover?: boolean;
  paginator?: boolean;
  rows?: number;
  rowsPerPageOptions?: number[];
  emptyMessage?: string;
  loading?: boolean;
  sortField?: string;
  sortOrder?: 1 | -1;
  /**
   * Enable lazy loading (server-side pagination).
   * When true, the table will not paginate data locally.
   * Instead, it will emit pageChange events for you to fetch new data.
   */
  lazy?: boolean;
  /**
   * Total number of records (required for lazy loading).
   * Used to calculate total pages for server-side pagination.
   */
  totalRecords?: number;
}

export interface PaginationState {
  first: number;
  rows: number;
  page: number;
  totalRecords: number;
}

export interface SortEvent {
  field: string;
  order: 1 | -1;
}

// Legacy support
export interface CleansiaTableColumn {
  field: string;
  header: string;
  sortable?: boolean;
  pipe?: 'date' | 'number' | 'currency';
  pipeArgs?: unknown[];
  width?: string;
  class?: string;
  headerClass?: string;
}

export interface CleansiaTableAction<T = unknown> {
  label: string;
  icon?: string;
  class?: string;
  action: (item: T) => void;
  visible?: (item: T) => boolean;
  disabled?: (item: T) => boolean;
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
