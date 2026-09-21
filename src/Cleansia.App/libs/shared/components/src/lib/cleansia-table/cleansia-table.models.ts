import { TemplateRef } from '@angular/core';

export interface TableColumn<T = unknown> {
  id: string;
  field: string;
  header: string;
  sortable?: boolean;
  width?: string;
  align?: 'left' | 'center' | 'right';
  customTemplate?: TemplateRef<unknown>;
  getValue?: (row: T) => unknown;
}

export interface TableAction<T = unknown> {
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
